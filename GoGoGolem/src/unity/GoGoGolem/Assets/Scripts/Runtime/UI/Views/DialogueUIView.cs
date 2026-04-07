using TMPro;
using UnityEngine;

/// <summary>
/// 대화 UI View — 게임 상태 전환 + 발화자 이름 치환
///
/// LinePresenter(Yarn Spinner 패키지)가 characterNameText에 Yarn 발화자 이름을 세팅한 뒤,
/// LateUpdate에서 저장된 플레이어/골렘 이름으로 치환한다.
/// (구조 참고: BarkView / BarkPresenter의 ResolveSpeaker 패턴)
///
/// Inspector 설정:
///   Character Name Text  → LinePresenter와 동일한 TMP_Text를 드래그
///   Player Yarn Key      → Yarn 파일에서 플레이어를 나타내는 발화자 이름 (기본: "주인공")
///   Golem Yarn Key       → Yarn 파일에서 골렘을 나타내는 발화자 이름 (기본: "골렘")
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private GameEvent onDialogueStartedEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

    [Header("발화자 이름 치환")]
    [Tooltip("LinePresenter의 characterNameText와 동일한 TMP_Text")]
    [SerializeField] private TMP_Text characterNameText;

    [Tooltip("Yarn 파일에서 플레이어를 나타내는 발화자 이름")]
    [SerializeField] private string playerYarnKey = "주인공";

    [Tooltip("Yarn 파일에서 골렘을 나타내는 발화자 이름")]
    [SerializeField] private string golemYarnKey = "골렘";

    private bool _isDialogueActive;

    private void OnEnable()
    {
        if (onDialogueStartedEvent != null)
            onDialogueStartedEvent.Register(OnDialogueStarted);

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Register(OnDialogueCompleted);
    }

    private void OnDisable()
    {
        if (onDialogueStartedEvent != null)
            onDialogueStartedEvent.Unregister(OnDialogueStarted);

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Unregister(OnDialogueCompleted);
    }

    private void LateUpdate()
    {
        if (!_isDialogueActive || characterNameText == null) return;

        string current = characterNameText.text;
        if (string.IsNullOrEmpty(current)) return;

        string resolved = ResolveSpeaker(current);
        if (resolved != current)
            characterNameText.text = resolved;
    }

    private string ResolveSpeaker(string speaker)
    {
        var gm = GameManager.Instance;
        if (gm == null) return speaker;

        if (speaker == playerYarnKey && gm.HasPlayerName)
            return gm.PlayerName;

        if (speaker == golemYarnKey && gm.HasGolemName)
            return gm.GolemName;

        return speaker;
    }

    private void OnDialogueStarted()
    {
        _isDialogueActive = true;
        GameStateManager.Instance.ChangeState(GameState.Dialogue);
    }

    private void OnDialogueCompleted()
    {
        _isDialogueActive = false;
        GameStateManager.Instance.ChangeState(GameState.Gameplay);
    }
}
