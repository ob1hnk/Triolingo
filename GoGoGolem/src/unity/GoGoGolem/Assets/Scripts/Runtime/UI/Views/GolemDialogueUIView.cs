using TMPro;
using UnityEngine;

/// <summary>
/// 골렘 대화 UI 뷰 (RealtimeVoiceManager 스트리밍 방식)
///
/// 상태 전환:
///   초기        → ShowInitialState()         골렘 말풍선: "Space를 눌러 시작", StatusUI 숨김
///   음성 활성   → ShowVoiceActiveState()     StatusUI 표시, Space = "중지"
///   음성 비활성 → ShowVoiceInactiveState()   StatusUI 숨김, Space = "말하기"
///   발화 감지   → ShowSpeechDetectedIndicator() (서버 VAD 감지 시)
///
/// 스트리밍 텍스트 처리:
///   HandleTranscript   → UpdatePlayerText() + ClearGolemText()
///   HandleStreamingText → AppendGolemText(delta)  ← delta를 바로 이어붙이기
///   (타이프라이터 코루틴 없음 - 서버가 보내는 속도가 곧 표시 속도)
///
/// 디자이너 PNG 매핑:
///   conversation_cutscene.png         → 전체 Canvas 배경 Image (선택)
///   conversation_golem.png            → GolemSpeechBubble Image Source
///   conversation_text.png             → PlayerDialogueBox Image Source
///   conversation_text_name.png        → PlayerNameBadge Image Source
///   conversation_select_background.png → KeyHintsBar Image Source
///   conversation_select_line.png      → 구분선 Image Source
///   conversation_select_arrow.png     → 화살표 Image Source
/// </summary>
public class GolemDialogueUIView : MonoBehaviour
{
    [Header("Canvas Root (비활성 상태로 시작)")]
    [Tooltip("GolemDialogueUI Canvas를 여기에 드래그")]
    [SerializeField] private GameObject dialogueCanvas;

    [Header("골렘 말풍선")]
    [SerializeField] private GameObject golemSpeechBubble;
    [SerializeField] private TMP_Text golemSpeechText;

    [Header("플레이어 대화창")]
    [SerializeField] private GameObject playerDialogueBox;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerSpeechText;

    [Header("상태 UI (듣는 중... / 말하는 중...)")]
    [SerializeField] private GameObject statusUI;
    [SerializeField] private TMP_Text statusText;

    [Header("하단 키 힌트")]
    [Tooltip("Space 옆 텍스트 — '말하기' 또는 '중지'로 코드에서 전환됨")]
    [SerializeField] private TMP_Text spaceActionText;

    private const string INITIAL_PROMPT = "Space를 눌러 대화를 시작해보세요";

    // ── 상태 전환 ─────────────────────────────────

    /// <summary>대화 씬 진입 직후 초기 화면</summary>
    public void ShowInitialState()
    {
        if (dialogueCanvas != null) dialogueCanvas.SetActive(true);

        golemSpeechBubble.SetActive(true);
        golemSpeechText.text = INITIAL_PROMPT;

        playerDialogueBox.SetActive(false);
        statusUI.SetActive(false);

        spaceActionText.text = "말하기";
    }

    /// <summary>Space 눌러 StartVoice() 호출 직후 — 서버 연결 + 마이크 시작</summary>
    public void ShowVoiceActiveState()
    {
        statusUI.SetActive(true);
        if (statusText != null) statusText.text = "듣는 중...";
        spaceActionText.text = "중지";
    }

    /// <summary>Space 다시 눌러 StopVoice() 호출 직후</summary>
    public void ShowVoiceInactiveState()
    {
        statusUI.SetActive(false);
        spaceActionText.text = "말하기";
    }

    /// <summary>서버 VAD가 발화를 감지했을 때 (OnSpeechDetected)</summary>
    public void ShowSpeechDetectedIndicator()
    {
        statusUI.SetActive(true);
        if (statusText != null) statusText.text = "듣는 중...";
    }

    // ── RealtimeVoiceManager 이벤트 수신 ──────────

    /// <summary>
    /// OnTranscript 수신 - 사용자 발화 인식 텍스트 표시
    /// 동시에 골렘 말풍선 초기화 (새 응답 스트리밍 준비)
    /// </summary>
    public void UpdatePlayerText(string transcript)
    {
        playerDialogueBox.SetActive(true);
        playerSpeechText.text = transcript;
    }

    /// <summary>새 응답 스트리밍 시작 전 말풍선 초기화</summary>
    public void ClearGolemText()
    {
        golemSpeechBubble.SetActive(true);
        golemSpeechText.text = "";
    }

    /// <summary>
    /// OnStreamingText(delta) 수신 — delta를 바로 이어붙이기
    /// 서버가 조각을 보내는 속도 = 화면에 글자가 나타나는 속도
    /// 타이프라이터 코루틴 불필요
    /// </summary>
    public void AppendGolemText(string delta)
    {
        golemSpeechBubble.SetActive(true);
        golemSpeechText.text += delta;
    }

    // ── 대화 종료 ─────────────────────────────────

    public void Hide()
    {
        if (dialogueCanvas != null) dialogueCanvas.SetActive(false);
    }
}
