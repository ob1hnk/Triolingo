using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 런타임에서 실행되는 Quest 인스턴스
/// </summary>
[System.Serializable]
public class Quest
{
    public string QuestID { get; private set; }
    public string QuestName { get; private set; }
    public string Description { get; private set; }
    public QuestType QuestType { get; private set; }
    public QuestStatus Status { get; private set; }
    public QuestReward Reward { get; private set; }

    private List<QuestObjective> objectives;

    /// <summary>
    /// QuestData로부터 Quest 생성
    /// </summary>
    public Quest(QuestData questData)
    {
        if (!questData.Validate())
        {
            Debug.LogError($"Invalid QuestData: {questData.name}");
            return;
        }

        QuestID = questData.questID;
        QuestName = questData.questName;
        Description = questData.summary;
        QuestType = questData.questType;
        Status = QuestStatus.InProgress;
        Reward = questData.reward;

        objectives = new List<QuestObjective>();
        foreach (var objData in questData.objectives)
        {
            objectives.Add(new QuestObjective(objData));
        }

        Debug.Log($"[Quest] Created: {QuestID} - {QuestName}");
    }

    /// <summary>
    /// Phase 완료
    /// </summary>
    public void CompletePhase(string objectiveID, string phaseID)
    {
        var objective = GetObjective(objectiveID);
        if (objective == null)
        {
            Debug.LogWarning($"Objective {objectiveID} not found in Quest {QuestID}");
            return;
        }

        objective.CompletePhase(phaseID);
        CheckCompletion();
    }

    /// <summary>
    /// Quest 완료 처리
    /// </summary>
    public void Complete()
    {
        Status = QuestStatus.Completed;
        Debug.Log($"[Quest] Completed: {QuestID} - {QuestName}");
        
        if (Reward != null && !Reward.IsEmpty())
        {
            Debug.Log($"[Quest] Reward: {Reward}");
        }
    }

    /// <summary>
    /// Objective 가져오기
    /// </summary>
    public QuestObjective GetObjective(string objectiveID)
    {
        return objectives.FirstOrDefault(o => o.ObjectiveID == objectiveID);
    }

    /// <summary>
    /// Phase 가져오기
    /// </summary>
    public QuestPhase GetPhase(string objectiveID, string phaseID)
    {
        return GetObjective(objectiveID)?.GetPhase(phaseID);
    }

    /// <summary>
    /// 현재 진행중인 Objective 가져오기
    /// </summary>
    public QuestObjective GetCurrentObjective()
    {
        return objectives.FirstOrDefault(o => !o.IsCompleted);
    }

    /// <summary>
    /// 모든 Objective 가져오기
    /// </summary>
    public List<QuestObjective> GetAllObjectives()
    {
        return objectives;
    }

    /// <summary>
    /// Quest 전체 진행도 계산 (0.0 ~ 1.0)
    /// </summary>
    public float GetProgress()
    {
        if (objectives.Count == 0) return 0f;
        
        float totalProgress = objectives.Sum(o => o.GetProgress());
        return totalProgress / objectives.Count;
    }

    /// <summary>
    /// Quest가 완료되었는지 확인
    /// </summary>
    public bool IsCompleted()
    {
        return objectives.All(o => o.IsCompleted);
    }

    /// <summary>
    /// Objective가 완료되었는지 확인
    /// </summary>
    public bool IsObjectiveCompleted(string objectiveID)
    {
        return GetObjective(objectiveID)?.IsCompleted ?? false;
    }

    /// <summary>
    /// 완료 상태 체크
    /// </summary>
    private void CheckCompletion()
    {
        if (IsCompleted() && Status != QuestStatus.Completed)
        {
            Complete();
        }
    }

    public override string ToString()
    {
        int completedObjectives = objectives.Count(o => o.IsCompleted);
        return $"[Quest {QuestID}] {QuestName} ({completedObjectives}/{objectives.Count}) - Status: {Status}";
    }
}
