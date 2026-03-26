using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 범용 NPC 클래스. 대화 시작을 담당한다.
/// 퀘스트 액션이 필요한 경우 같은 GameObject에 NPCQuestHandler를 추가한다.
///
/// 대화 방식:
///   - dialogueID가 설정된 경우 → Yarn 대화 (핵심 NPC)
///   - dialogueLines가 설정된 경우 → 말풍선 출력 (일반 NPC)
/// </summary>
public class NPC : MonoBehaviour, IInteractable
{
    [Header("Dialogue (Yarn — 핵심 NPC)")]
    [Tooltip("Yarn 대화 노드 이름 (예: DLG-001). 비우면 말풍선 모드 사용.")]
    [SerializeField] private string dialogueID;

    [Header("Dialogue (Speech Bubble — 일반 NPC)")]
    [Tooltip("E키를 누를 때마다 순서대로 출력할 대사. 마지막 대사 이후 말풍선이 닫힘.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] dialogueLines;

    [Header("Post-Quest Dialogue (퀘스트 완료 후 고정 대사)")]
    [Tooltip("이 퀘스트가 완료된 이후에는 아래 대사를 반복 출력함.")]
    [SerializeField] private string completedQuestID;
    [TextArea(2, 4)]
    [SerializeField] private string[] postQuestLines;

    [Header("Options")]
    [Tooltip("한 번만 상호작용 가능")]
    [SerializeField] private bool onceOnly = false;

    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartDialogueEvent;

    private bool hasInteracted = false;
    private int _currentLineIndex = 0;
    private int _postQuestLineIndex = 0;
    private SpeechBubbleView _speechBubble;
    private NPCQuestHandler _questHandler;

    // 말풍선 활성 중에만 사용하는 로컬 InputAction (Enter, Space, 클릭)
    private InputAction _bubbleAdvanceAction;

    public InteractionType InteractionType => InteractionType.TalkNPC;

    private void Awake()
    {
        _speechBubble = GetComponentInChildren<SpeechBubbleView>(true);
        _questHandler = GetComponent<NPCQuestHandler>();

        _bubbleAdvanceAction = new InputAction("SpeechBubbleAdvance", InputActionType.Button);
        _bubbleAdvanceAction.AddBinding("<Keyboard>/enter");
        _bubbleAdvanceAction.AddBinding("<Keyboard>/space");
        _bubbleAdvanceAction.AddBinding("<Mouse>/leftButton");
        _bubbleAdvanceAction.performed += OnBubbleAdvance;
    }

    private void OnDestroy()
    {
        _bubbleAdvanceAction.performed -= OnBubbleAdvance;
        _bubbleAdvanceAction.Dispose();
    }

    private void OnBubbleAdvance(InputAction.CallbackContext ctx)
    {
        if (IsPostQuestMode())
            AdvancePostQuestBubble();
        else
            AdvanceSpeechBubble();
    }

    private void AdvanceSpeechBubble()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;

        if (_currentLineIndex < dialogueLines.Length)
        {
            if (_speechBubble != null)
                _speechBubble.Show(dialogueLines[_currentLineIndex]);
            _currentLineIndex++;
        }
        else
        {
            if (_speechBubble != null)
                _speechBubble.Hide();
            _bubbleAdvanceAction.Disable();
            _currentLineIndex = 0;

            if (onceOnly)
                hasInteracted = true;
        }
    }

    public string GetActionLabel()
    {
        if (onceOnly && hasInteracted) return "";
        return promptData != null ? promptData.ActionLabel : "";
    }

    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;

    private void AdvancePostQuestBubble()
    {
        if (postQuestLines == null || postQuestLines.Length == 0) return;

        if (_postQuestLineIndex < postQuestLines.Length)
        {
            if (_speechBubble != null)
                _speechBubble.Show(postQuestLines[_postQuestLineIndex]);
            _postQuestLineIndex++;
        }
        else
        {
            if (_speechBubble != null)
                _speechBubble.Hide();
            _bubbleAdvanceAction.Disable();
            _postQuestLineIndex = 0;
        }
    }

    private bool IsPostQuestMode()
    {
        return !string.IsNullOrEmpty(completedQuestID)
            && postQuestLines != null
            && postQuestLines.Length > 0
            && Managers.Quest != null
            && Managers.Quest.IsQuestCompleted(completedQuestID);
    }

    public void Interact()
    {
        if (onceOnly && hasInteracted) return;

        // 퀘스트 완료 후 고정 대사 모드
        if (IsPostQuestMode())
        {
            _bubbleAdvanceAction.Enable();
            AdvancePostQuestBubble();
            return;
        }

        // Yarn 모드
        if (!string.IsNullOrEmpty(dialogueID))
        {
            hasInteracted = true;
            if (requestStartDialogueEvent != null)
                requestStartDialogueEvent.Raise(dialogueID);
            else
                Debug.LogError($"[NPC] {gameObject.name}: requestStartDialogueEvent가 null입니다!");

            if (_questHandler != null) _questHandler.Execute();
            return;
        }

        // 말풍선 모드 — 첫 E키 입력으로 시작, 이후 Enter/Space/클릭으로 진행
        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            _bubbleAdvanceAction.Enable();
            AdvanceSpeechBubble();
            return;
        }

        Debug.LogWarning($"[NPC] {gameObject.name}: dialogueID도 dialogueLines도 설정되지 않았습니다.");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = hasInteracted ? Color.green : Color.blue;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
#endif
}