using UnityEngine;
using Yarn.Unity;

/// <summary>
/// 대화 시스템 관리자
/// Yarn Spinner 3.x의 DialogueRunner를 래핑하여 기존 API를 유지
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("Yarn Spinner")]
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Dialogue UI")]
    [Tooltip("대화 시작 시 활성화, 종료 시 비활성화할 Canvas GameObject")]
    [SerializeField] private GameObject dialogueCanvas;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartDialogueEvent;
    [SerializeField] private GameEvent onDialogueStartedEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;
    [Tooltip("대화 시작 시 HUD 숨김 요청 (선택)")]
    [SerializeField] private GameEvent requestHideHUDEvent;
    [Tooltip("대화 종료 시 HUD 표시 요청 (선택)")]
    [SerializeField] private GameEvent requestShowHUDEvent;

    private void Awake()
    {
        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponentInChildren<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            Debug.LogError("[DialogueManager] DialogueRunner가 연결되지 않았습니다!");
            return;
        }

        dialogueRunner.onDialogueStart.AddListener(HandleDialogueStart);
        dialogueRunner.onDialogueComplete.AddListener(HandleDialogueComplete);
    }

    private void OnEnable()
    {
        if (requestStartDialogueEvent != null)
            requestStartDialogueEvent.Register(StartDialogue);
    }

    private void OnDisable()
    {
        if (requestStartDialogueEvent != null)
            requestStartDialogueEvent.Unregister(StartDialogue);
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
        {
            dialogueRunner.onDialogueStart.RemoveListener(HandleDialogueStart);
            dialogueRunner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
        }
    }

    /// <summary>
    /// 대화 시작 (Yarn node 이름으로)
    /// QuestManager의 contentID가 Yarn node 이름과 매핑됨
    /// 예: "DLG-001" → "DLG_001" (하이픈→언더스코어)
    /// </summary>
    public void StartDialogue(string dialogueID)
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("[DialogueManager] DialogueRunner가 없습니다!");
            return;
        }

        if (dialogueRunner.IsDialogueRunning)
        {
            Debug.LogWarning("[DialogueManager] 대화가 이미 진행 중입니다.");
            return;
        }

        dialogueCanvas?.SetActive(true);
        requestHideHUDEvent?.Raise();

        string nodeName = dialogueID.Replace('-', '_');
        RunDialogueAsync(nodeName);
    }

    private async void RunDialogueAsync(string nodeName)
    {
        try
        {
            await dialogueRunner.StartDialogue(nodeName);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueManager] 대화 예외: {e.Message}\n{e.StackTrace}");
        }
    }

    public bool IsPlaying() => dialogueRunner != null && dialogueRunner.IsDialogueRunning;

    public void SkipDialogue()
    {
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
            dialogueRunner.Stop();
    }

    private void HandleDialogueStart() => onDialogueStartedEvent?.Raise();

    private void HandleDialogueComplete()
    {
        dialogueCanvas?.SetActive(false);
        requestShowHUDEvent?.Raise();
        onDialogueCompletedEvent?.Raise();
    }
}