using System.Linq;
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

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartDialogueEvent;
    [SerializeField] private GameEvent onDialogueStartedEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

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
        {
            requestStartDialogueEvent.Register(StartDialogue);
            Debug.Log("[DialogueManager] OnEnable: 이벤트 등록 완료");
        }
        else
        {
            Debug.LogError("[DialogueManager] OnEnable: requestStartDialogueEvent가 null입니다!");
        }
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
        Debug.Log($"[DialogueManager] StartDialogue 호출됨! dialogueID={dialogueID}");

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

        // QuestData의 contentID는 "DLG-001" 형식, Yarn node는 "DLG_001" 형식
        string nodeName = dialogueID.Replace('-', '_');

        // YarnProject에 노드가 있는지 확인
        var yarnProject = dialogueRunner.YarnProject;
        if (yarnProject == null)
        {
            Debug.LogError("[DialogueManager] DialogueRunner에 YarnProject가 설정되지 않았습니다!");
            return;
        }

        Debug.Log($"[DialogueManager] YarnProject: {yarnProject.name}");
        Debug.Log($"[DialogueManager] DialogueViews 개수: {dialogueRunner.DialogueViews.Count()}");

        Debug.Log($"[DialogueManager] DialogueRunner.StartDialogue 호출: {nodeName}");
        dialogueRunner.StartDialogue(nodeName);
        Debug.Log($"[DialogueManager] DialogueRunner.StartDialogue 호출 완료");
        Debug.Log($"[DialogueManager] IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
    }

    /// <summary>
    /// 대화 중인지 확인
    /// </summary>
    public bool IsPlaying()
    {
        return dialogueRunner != null && dialogueRunner.IsDialogueRunning;
    }

    /// <summary>
    /// 대화 스킵/중단
    /// </summary>
    public void SkipDialogue()
    {
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.Stop();
        }
    }

    private void HandleDialogueStart()
    {
        onDialogueStartedEvent?.Raise();
    }

    private void HandleDialogueComplete()
    {
        Debug.Log("[DialogueManager] 대화 종료.");
        onDialogueCompletedEvent?.Raise();
    }
}
