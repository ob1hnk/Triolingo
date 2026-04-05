using System;
using UnityEngine;

/// <summary>
/// NPC GameObject에 추가하는 퀘스트 액션 컴포넌트.
/// NPC.Interact() 이후 호출되며 퀘스트 시작/완료 등을 처리한다.
///
/// entries 배열을 순회하며 퀘스트 상태 조건에 부합하는 **첫 번째** 엔트리를 실행한다.
/// 같은 NPC가 상황에 따라 다른 퀘스트 액션을 수행할 수 있다.
/// 예) 할아버지: [NotStarted → StartQuest], [Active → CompleteQuest]
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

    /// <summary>엔트리 실행 조건. 퀘스트의 현재 상태를 기준으로 판정.</summary>
    public enum QuestCondition
    {
        Always,
        NotStarted, // 아직 시작하지 않음 (active도 completed도 아님)
        Active,     // 진행 중
        Completed,  // 완료됨
    }

    [Serializable]
    public struct QuestActionEntry
    {
        [Tooltip("이 엔트리가 실행될 조건")]
        public QuestCondition condition;

        [Tooltip("실행할 액션")]
        public NPCQuestAction action;

        [Tooltip("CompletePhase 액션 전용")]
        public string objectiveID;
        [Tooltip("CompletePhase 액션 전용")]
        public string phaseID;

        [Header("Gate (선행 조건)")]
        [Tooltip("(선택) 이 phase가 이미 완료되어 있을 때만 엔트리가 실행됨. 선행 조건 걸고 싶을 때 사용.")]
        public string requiredCompletedObjectiveID;
        public string requiredCompletedPhaseID;
    }

    [Header("Quest Info")]
    [SerializeField] private string questID;

    [Tooltip("위에서부터 조건에 맞는 첫 번째 엔트리 하나만 실행된다.")]
    [SerializeField] private QuestActionEntry[] entries;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

    public void Execute()
    {
        if (entries == null || entries.Length == 0) return;

        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogWarning($"[NPCQuestHandler] {gameObject.name}: Quest ID가 설정되지 않았습니다!");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (MatchesCondition(entries[i].condition) && GatePassed(entries[i]))
            {
                RunAction(entries[i]);
                return;
            }
        }
    }

    private bool GatePassed(QuestActionEntry entry)
    {
        if (string.IsNullOrEmpty(entry.requiredCompletedPhaseID)) return true;
        if (string.IsNullOrEmpty(entry.requiredCompletedObjectiveID))
        {
            Debug.LogWarning($"[NPCQuestHandler] {gameObject.name}: requiredCompletedPhaseID는 설정됐지만 requiredCompletedObjectiveID가 비었습니다.");
            return false;
        }

        var quest = Managers.Quest?.GetActiveQuest(questID);
        if (quest == null) return false;

        var phase = quest.GetPhase(entry.requiredCompletedObjectiveID, entry.requiredCompletedPhaseID);
        return phase != null && phase.IsCompleted;
    }

    private bool MatchesCondition(QuestCondition condition)
    {
        var quest = Managers.Quest;
        if (quest == null) return condition == QuestCondition.Always;

        switch (condition)
        {
            case QuestCondition.Always: return true;
            case QuestCondition.Active: return quest.IsQuestActive(questID);
            case QuestCondition.Completed: return quest.IsQuestCompleted(questID);
            case QuestCondition.NotStarted:
                return !quest.IsQuestActive(questID) && !quest.IsQuestCompleted(questID);
            default: return false;
        }
    }

    private void RunAction(QuestActionEntry entry)
    {
        switch (entry.action)
        {
            case NPCQuestAction.None:
                break;

            case NPCQuestAction.StartQuest:
                requestStartQuestEvent?.Raise(questID);
                break;

            case NPCQuestAction.CompletePhase:
                if (string.IsNullOrEmpty(entry.objectiveID) || string.IsNullOrEmpty(entry.phaseID))
                {
                    Debug.LogError($"[NPCQuestHandler] {gameObject.name}: ObjectiveID 또는 PhaseID가 설정되지 않았습니다!");
                    return;
                }
                requestCompletePhaseEvent?.Raise(new CompletePhaseRequest(questID, entry.objectiveID, entry.phaseID));
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
        if (entries == null || entries.Length == 0 || string.IsNullOrEmpty(questID)) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(questID);
        for (int i = 0; i < entries.Length; i++)
            sb.AppendLine($"[{entries[i].condition}] → {entries[i].action}");

        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, sb.ToString());
    }
#endif
}