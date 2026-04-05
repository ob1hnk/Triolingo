using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Yarn 스크립트에서 퀘스트/인벤토리에 영향을 주는 커맨드 핸들러.
///
/// DialogueRunner가 있는 GameObject에 컴포넌트로 붙이면, 아래 커맨드가 등록된다:
///   <<complete_phase {questID} {objectiveID} {phaseID}>>
///   <<give_item {itemID}>>
///
/// 예) 할아버지의 첫 대화 노드 끝에:
///   <<complete_phase MQ-02 MQ-02-OBJ-01 MQ-02-P02>>
///   <<give_item ITEM-002>>
///
/// Interact 시점이 아니라 "대화 노드가 끝나는 순간"에 실행된다.
/// </summary>
[RequireComponent(typeof(DialogueRunner))]
public class QuestYarnCommands : MonoBehaviour
{
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
        _runner.AddCommandHandler<string, string, string>("complete_phase", OnCompletePhase);
        _runner.AddCommandHandler<string>("give_item", OnGiveItem);
    }

    private void OnDisable()
    {
        if (_runner == null) return;
        _runner.RemoveCommandHandler("complete_phase");
        _runner.RemoveCommandHandler("give_item");
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