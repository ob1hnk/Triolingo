using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using Multimodal.Voice;

/// <summary>
/// 골렘 음성 대화 씬 컨트롤러 (RealtimeVoiceManager 기반)
///
/// 흐름:
/// 1. EnterDialogueMode()  → 플레이어 조작 차단, 골렘 회전 → 완료 후 카메라 전환, 초기 UI 표시
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

    [Header("골렘")]
    [Tooltip("골렘 Transform — 플레이어 방향으로 회전시킬 대상")]
    [SerializeField] private Transform golemTransform;
    [Tooltip("회전 속도 (높을수록 빠르게 돌아봄)")]
    [SerializeField] private float golemRotationSpeed = 5f;
    [Tooltip("이 각도 이내로 들어오면 회전 완료로 판정 (degrees)")]
    [SerializeField] private float golemRotationThreshold = 5f;

    [Header("플레이어")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInteraction playerInteraction;
    [Tooltip("대화 중 숨길 플레이어 모델 (3D 메쉬 루트)")]
    [SerializeField] private GameObject playerModel;
    [Tooltip("골렘이 바라볼 대상 (플레이어 Transform)")]
    [SerializeField] private Transform playerTransform;

    [Header("Voice")]
    [SerializeField] private RealtimeVoiceManager voiceManager;

    [Header("UI")]
    [SerializeField] private GolemDialogueUIView uiView;
    [SerializeField] private GameObject hudCanvas;

    [Header("Event Channels")]
    [SerializeField] private GameEvent requestEnterDialogueEvent;

    private bool _isInDialogueMode = false;
    private bool _isVoiceSessionActive = false;

    private void OnEnable()
    {
        requestEnterDialogueEvent?.Register(EnterDialogueMode);

        if (voiceManager == null) return;
        voiceManager.OnConnected       += HandleVoiceConnected;
        voiceManager.OnSpeechDetected  += HandleSpeechDetected;
        voiceManager.OnTranscript      += HandleTranscript;
        voiceManager.OnStreamingText   += HandleStreamingText;
        voiceManager.OnAIResponse      += HandleAIResponse;
    }

    private void OnDisable()
    {
        requestEnterDialogueEvent?.Unregister(EnterDialogueMode);

        if (voiceManager == null) return;
        voiceManager.OnConnected       -= HandleVoiceConnected;
        voiceManager.OnSpeechDetected  -= HandleSpeechDetected;
        voiceManager.OnTranscript      -= HandleTranscript;
        voiceManager.OnStreamingText   -= HandleStreamingText;
        voiceManager.OnAIResponse      -= HandleAIResponse;
    }

    /// <summary>GolemInteractable의 requestEnterDialogueEvent SO를 통해 호출</summary>
    public void EnterDialogueMode()
    {
        if (_isInDialogueMode) return;
        _isInDialogueMode = true;

        // 게임 상태 알림 (InputModeController가 Q키 등 불필요한 입력을 차단하도록)
        GameStateManager.Instance?.ChangeState(GameState.Dialogue);

        // 1. 플레이어 조작만 차단 (모델은 카메라 전환 시점에 숨김)
        if (playerController != null) playerController.enabled = false;
        if (playerInteraction != null) playerInteraction.enabled = false;
        if (hudCanvas != null) hudCanvas.SetActive(false);

        // 2. 골렘 회전 시작 → 완료 후 카메라 전환 + UI 표시
        StartCoroutine(RotateGolemThenEnter());
    }

    /// <summary>
    /// 골렘이 플레이어를 바라볼 때까지 Slerp 회전 후 대화 모드 진입 완료
    /// Y축만 회전 (고개를 끄덕이는 것처럼 보이지 않도록)
    /// </summary>
    private IEnumerator RotateGolemThenEnter()
    {
        if (golemTransform != null && playerTransform != null)
        {
            Vector3 direction = playerTransform.position - golemTransform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                while (Quaternion.Angle(golemTransform.rotation, targetRotation) > golemRotationThreshold)
                {
                    golemTransform.rotation = Quaternion.Slerp(
                        golemTransform.rotation,
                        targetRotation,
                        golemRotationSpeed * Time.deltaTime
                    );
                    yield return null;
                }

                golemTransform.rotation = targetRotation;
            }
        }

        // 3. 카메라 전환 + 플레이어 모델 숨김 동시 처리
        if (dialogueVirtualCamera != null)
            dialogueVirtualCamera.Priority = dialogueCamPriority;
        if (playerModel != null) playerModel.SetActive(false);

        // 4. 초기 UI 표시 ("Space를 눌러 대화를 시작해보세요")
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
            uiView.ShowVoiceConnectingState();
            var questContext = BuildQuestContext();
            await voiceManager.StartVoice(questContext: questContext);
        }
        else
        {
            _isVoiceSessionActive = false;
            voiceManager.StopVoice();
            uiView.ShowVoiceInactiveState();
        }
    }

    /// <summary>현재 퀘스트 진행 상황을 수집하여 API 전송용 페이로드로 변환</summary>
    private QuestContextPayload BuildQuestContext()
    {
        var payload = new QuestContextPayload();

        var activeQuests = Managers.Quest?.GetAllActiveQuests() ?? new List<Quest>();
        foreach (var quest in activeQuests)
        {
            var qp = new ActiveQuestPayload
            {
                quest_id   = quest.QuestID,
                quest_name = quest.QuestName,
                quest_type = quest.QuestType.ToString(),
                status     = quest.Status.ToString(),
                progress   = quest.GetProgress(),
            };

            foreach (var obj in quest.GetAllObjectives())
            {
                var op = new QuestObjectivePayload
                {
                    objective_id = obj.ObjectiveID,
                    description  = obj.Description,
                    is_completed = obj.IsCompleted,
                    progress     = obj.GetProgress(),
                };

                foreach (var phase in obj.GetAllPhases())
                {
                    op.phases.Add(new QuestPhasePayload
                    {
                        phase_id     = phase.PhaseID,
                        phase_type   = phase.PhaseType.ToString(),
                        content_id   = phase.ContentID,
                        is_completed = phase.IsCompleted,
                    });
                }

                qp.objectives.Add(op);
            }

            payload.active_quests.Add(qp);
        }

        var completedQuests = Managers.Quest?.GetAllCompletedQuests() ?? new List<Quest>();
        foreach (var quest in completedQuests)
            payload.completed_quest_ids.Add(quest.QuestID);

        return payload;
    }

    /// <summary>Esc 키 또는 외부에서 대화 완전 종료 시 호출</summary>
    public void ExitDialogueMode()
    {
        if (!_isInDialogueMode) return;

        if (_isVoiceSessionActive)
        {
            voiceManager.StopVoice();
            _isVoiceSessionActive = false;
        }

        if (dialogueVirtualCamera != null)
            dialogueVirtualCamera.Priority = inactiveCamPriority;

        if (playerController != null) playerController.enabled = true;
        if (playerInteraction != null) playerInteraction.enabled = true;
        if (playerModel != null) playerModel.SetActive(true);
        if (hudCanvas != null) hudCanvas.SetActive(true);

        uiView.Hide();
        _isInDialogueMode = false;

        GameStateManager.Instance?.ChangeState(GameState.Gameplay);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    /// <summary>WebSocket 연결 완료 → 듣는 중으로 전환</summary>
    private void HandleVoiceConnected()
    {
        uiView.ShowVoiceActiveState();
    }

    /// <summary>서버 VAD가 사용자 발화를 감지했을 때</summary>
    private void HandleSpeechDetected()
    {
        uiView.ShowSpeechDetectedIndicator();
    }

    /// <summary>사용자 발화 인식 완료 → 플레이어 대화창 업데이트 + 골렘 말풍선 초기화</summary>
    private void HandleTranscript(string transcript)
    {
        uiView.UpdatePlayerText(transcript);
        uiView.ClearGolemText();
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
    }
}