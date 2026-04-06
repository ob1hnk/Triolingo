using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn 파일에서 퀘스트/인벤토리 관련 동작을 실행하기 위한 커맨드 모음.
///
/// Yarn 파일에서 호출 가능한 커맨드:
///   <<start_quest questID>>
///   <<complete_phase questID objectiveID phaseID>>
///   <<give_item itemID>>
///
/// DialogueRunner와 같은 GameObject에 부착하고, 이벤트 채널 SO를 Inspector에서 연결.
/// </summary>
[RequireComponent(typeof(DialogueRunner))]
public class QuestYarnCommands : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    private DialogueRunner _runner;

    private void Awake()
    {
        _runner = GetComponent<DialogueRunner>();
    }

    private void OnEnable()
    {
        if (_runner == null) return;
        _runner.AddCommandHandler<string>("start_quest", OnStartQuest);
        _runner.AddCommandHandler<string, string, string>("complete_phase", OnCompletePhase);
        _runner.AddCommandHandler<string>("give_item", OnGiveItem);
    }

    private void OnDisable()
    {
        if (_runner == null) return;
        _runner.RemoveCommandHandler("start_quest");
        _runner.RemoveCommandHandler("complete_phase");
        _runner.RemoveCommandHandler("give_item");
    }

    private void OnStartQuest(string questID)
    {
        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogError("[QuestYarnCommands] start_quest: questID가 비어있습니다.");
            return;
        }
        if (requestStartQuestEvent == null)
        {
            Debug.LogError("[QuestYarnCommands] requestStartQuestEvent가 연결되지 않았습니다.");
            return;
        }
        requestStartQuestEvent.Raise(questID);
    }

    private void OnCompletePhase(string questID, string objectiveID, string phaseID)
    {
        if (requestCompletePhaseEvent == null)
        {
            Debug.LogError("[QuestYarnCommands] requestCompletePhaseEvent가 연결되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(questID) || string.IsNullOrEmpty(objectiveID) || string.IsNullOrEmpty(phaseID))
        {
            Debug.LogError($"[QuestYarnCommands] complete_phase 인자가 비어있습니다: questID={questID}, objectiveID={objectiveID}, phaseID={phaseID}");
            return;
        }

        requestCompletePhaseEvent.Raise(new CompletePhaseRequest(questID, objectiveID, phaseID));
    }

    private void OnGiveItem(string itemID)
    {
        if (requestAcquireItemEvent == null)
        {
            Debug.LogError("[QuestYarnCommands] requestAcquireItemEvent가 연결되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(itemID))
        {
            Debug.LogError("[QuestYarnCommands] give_item의 itemID가 비어있습니다.");
            return;
        }

        requestAcquireItemEvent.Raise(itemID);
    }
}
