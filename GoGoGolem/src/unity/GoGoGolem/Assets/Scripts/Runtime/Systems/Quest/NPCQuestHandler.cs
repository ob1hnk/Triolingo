using UnityEngine;

/// <summary>
/// NPC GameObject에 추가하는 퀘스트 액션 컴포넌트.
/// NPC.Interact() 이후 호출되며 퀘스트 시작/완료 등을 처리한다.
/// </summary>
public class NPCQuestHandler : MonoBehaviour
{
    public enum NPCQuestAction
    {
        None,
        StartQuest,
        CompletePhase,
        CompleteQuest,
    }

    [Header("Quest Info")]
    [SerializeField] private string questID;
    [SerializeField] private NPCQuestAction questAction = NPCQuestAction.None;

    [Header("Phase Info (CompletePhase 전용)")]
    [SerializeField] private string objectiveID;
    [SerializeField] private string phaseID;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

    public void Execute()
    {
        if (questAction == NPCQuestAction.None) return;

        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogWarning($"[NPCQuestHandler] {gameObject.name}: Quest ID가 설정되지 않았습니다!");
            return;
        }

        switch (questAction)
        {
            case NPCQuestAction.StartQuest:
                requestStartQuestEvent?.Raise(questID);
                break;

            case NPCQuestAction.CompletePhase:
                if (string.IsNullOrEmpty(objectiveID) || string.IsNullOrEmpty(phaseID))
                {
                    Debug.LogError($"[NPCQuestHandler] {gameObject.name}: ObjectiveID 또는 PhaseID가 설정되지 않았습니다!");
                    return;
                }
                requestCompletePhaseEvent?.Raise(new CompletePhaseRequest(questID, objectiveID, phaseID));
                break;

            case NPCQuestAction.CompleteQuest:
                CompleteAllPhases();
                break;
        }
    }

    private void CompleteAllPhases()
    {
        var quest = Managers.Quest?.GetActiveQuest(questID);
        if (quest == null)
        {
            Debug.LogWarning($"[NPCQuestHandler] {gameObject.name}: Quest {questID}가 활성화되어 있지 않습니다.");
            return;
        }

        foreach (var objective in quest.GetAllObjectives())
        {
            foreach (var phase in objective.GetAllPhases())
            {
                if (!phase.IsCompleted)
                {
                    requestCompletePhaseEvent?.Raise(
                        new CompletePhaseRequest(questID, objective.ObjectiveID, phase.PhaseID));
                }
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (questAction == NPCQuestAction.None || string.IsNullOrEmpty(questID)) return;

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.5f,
            $"[{questAction}]\n{questID}"
        );
    }
#endif
}
