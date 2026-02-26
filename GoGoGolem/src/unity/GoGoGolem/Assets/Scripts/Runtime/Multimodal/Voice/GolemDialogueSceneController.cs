using UnityEngine;
using Unity.Cinemachine;
using Multimodal.Voice;

/// <summary>
/// 골렘 음성 대화 씬 컨트롤러 (RealtimeVoiceManager 기반)
///
/// 흐름:
/// 1. EnterDialogueMode()  → 대화 카메라 전환, 플레이어 조작 차단, 초기 UI 표시
/// 2. Space (첫 번째)      → StartVoice() → 서버 Server VAD로 발화 자동 감지
/// 3. OnSpeechDetected     → StatusUI "듣는 중..." 표시
/// 4. OnTranscript         → 플레이어 대화창에 인식 텍스트 표시, 골렘 말풍선 초기화
/// 5. OnStreamingText      → delta 조각을 골렘 말풍선에 바로 이어붙이기 (실시간)
/// 6. OnAIResponse         → 응답 완료 (이미 스트리밍으로 표시 완료)
/// 7. Space (두 번째)      → StopVoice() → 음성 중지
/// 8. Esc                  → ExitDialogueMode() → 전체 복구
///
/// 핵심: RealtimeVoiceManager는 서버 VAD 사용
///       - Space는 VoiceSession 시작/종료 토글
///       - 멀티턴 대화는 서버가 자동 처리 (EndSession 불필요)
///       - 텍스트는 OnStreamingText delta를 직접 append → 타이프라이터 코루틴 불필요
///
/// 주의: Time.timeScale = 0 사용 불가
///       RealtimeVoiceManager의 StreamAudioCoroutine이 WaitForSeconds 기반
///       → PlayerController / PlayerInteraction 컴포넌트 비활성화로 대체
/// </summary>
public class GolemDialogueSceneController : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("골렘 정면을 바라보는 고정 Cinemachine Virtual Camera")]
    [SerializeField] private CinemachineCamera dialogueVirtualCamera;
    [SerializeField] private int dialogueCamPriority = 20;
    [SerializeField] private int inactiveCamPriority = 0;

    [Header("플레이어")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteraction playerInteraction;
    [Tooltip("대화 중 숨길 플레이어 모델 (3D 메쉬 루트)")]
    [SerializeField] private GameObject playerModel;

    [Header("Voice")]
    [SerializeField] private RealtimeVoiceManager voiceManager;

    [Header("UI")]
    [SerializeField] private GolemDialogueUIView uiView;

    private bool _isInDialogueMode = false;
    private bool _isVoiceSessionActive = false;

    private void OnEnable()
    {
        if (voiceManager == null) return;
        voiceManager.OnSpeechDetected  += HandleSpeechDetected;
        voiceManager.OnTranscript      += HandleTranscript;
        voiceManager.OnStreamingText   += HandleStreamingText;
        voiceManager.OnAIResponse      += HandleAIResponse;
    }

    private void OnDisable()
    {
        if (voiceManager == null) return;
        voiceManager.OnSpeechDetected  -= HandleSpeechDetected;
        voiceManager.OnTranscript      -= HandleTranscript;
        voiceManager.OnStreamingText   -= HandleStreamingText;
        voiceManager.OnAIResponse      -= HandleAIResponse;
    }

    /// <summary>GolemInteractable에서 E키 입력 시 호출</summary>
    public void EnterDialogueMode()
    {
        if (_isInDialogueMode) return;
        _isInDialogueMode = true;

        // 1. 대화 카메라로 전환
        if (dialogueVirtualCamera != null)
            dialogueVirtualCamera.Priority = dialogueCamPriority;

        // 2. 플레이어 조작 차단 + 모델 숨김
        if (playerController != null) playerController.enabled = false;
        if (playerInteraction != null) playerInteraction.enabled = false;
        if (playerModel != null) playerModel.SetActive(false);

        // 3. 초기 UI 표시 ("Space를 눌러 대화를 시작해보세요")
        uiView.ShowInitialState();
    }

    private void Update()
    {
        if (!_isInDialogueMode) return;

        if (Input.GetKeyDown(KeyCode.Space))
            ToggleVoiceSession();
        else if (Input.GetKeyDown(KeyCode.Escape))
            ExitDialogueMode();
    }

    /// <summary>Space 키: 음성 세션 시작 ↔ 중지 토글</summary>
    private async void ToggleVoiceSession()
    {
        if (!_isVoiceSessionActive)
        {
            _isVoiceSessionActive = true;
            uiView.ShowVoiceActiveState();      // StatusUI 표시, Space → "중지"
            await voiceManager.StartVoice();    // 서버 연결 + 마이크 시작
        }
        else
        {
            _isVoiceSessionActive = false;
            voiceManager.StopVoice();           // 마이크 + 서버 연결 종료
            uiView.ShowVoiceInactiveState();    // StatusUI 숨김, Space → "말하기"
        }
    }

    /// <summary>Esc 키 또는 외부에서 대화 완전 종료 시 호출</summary>
    public void ExitDialogueMode()
    {
        if (!_isInDialogueMode) return;

        // 음성 세션 활성 중이면 중지
        if (_isVoiceSessionActive)
        {
            voiceManager.StopVoice();
            _isVoiceSessionActive = false;
        }

        // 카메라 복구
        if (dialogueVirtualCamera != null)
            dialogueVirtualCamera.Priority = inactiveCamPriority;

        // 플레이어 조작 복구 + 모델 표시
        if (playerController != null) playerController.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerModel != null) playerModel.SetActive(true);

        uiView.Hide();
        _isInDialogueMode = false;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    /// <summary>서버 VAD가 사용자 발화를 감지했을 때</summary>
    private void HandleSpeechDetected()
    {
        uiView.ShowSpeechDetectedIndicator();
    }

    /// <summary>사용자 발화 인식 완료 → 플레이어 대화창 업데이트 + 골렘 말풍선 초기화</summary>
    private void HandleTranscript(string transcript)
    {
        uiView.UpdatePlayerText(transcript);
        uiView.ClearGolemText();            // 새 응답을 위해 골렘 말풍선 초기화
    }

    /// <summary>골렘 응답 텍스트 조각 실시간 수신 → 말풍선에 바로 이어붙이기</summary>
    private void HandleStreamingText(string delta)
    {
        uiView.AppendGolemText(delta);
    }

    /// <summary>골렘 응답 완료 (이미 스트리밍으로 표시됨 - 추가 UI 처리 불필요)</summary>
    private void HandleAIResponse(string fullText)
    {
        // 응답 완료 후 필요한 처리 있으면 여기에 추가
        // 예: 다음 발화를 위한 상태 초기화 등
    }
}
