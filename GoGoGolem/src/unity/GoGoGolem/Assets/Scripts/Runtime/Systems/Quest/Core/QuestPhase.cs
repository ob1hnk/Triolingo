using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 런타임에서 실행되는 Phase 인스턴스
/// </summary>
[System.Serializable]
public class QuestPhase
{
    public string PhaseID { get; private set; }
    public PhaseType PhaseType { get; private set; }
    public string ContentID { get; private set; }
    public string Description { get; private set; }
    public bool IsCompleted { get; private set; }

    private List<string> conditionIDs;
    private List<string> actionIDs;

    /// <summary>
    /// PhaseData로부터 Phase 생성
    /// </summary>
    public QuestPhase(PhaseData phaseData)
    {
        PhaseID = phaseData.phaseID;
        PhaseType = phaseData.phaseType;
        ContentID = phaseData.contentID;
        Description = phaseData.description;
        IsCompleted = false;

        conditionIDs = phaseData.conditionIDs != null 
            ? new List<string>(phaseData.conditionIDs) 
            : new List<string>();
        
        actionIDs = phaseData.actionIDs != null 
            ? new List<string>(phaseData.actionIDs) 
            : new List<string>();
    }

    /// <summary>
    /// Phase 완료 처리
    /// </summary>
    public void Complete()
    {
        if (IsCompleted)
        {
            Debug.LogWarning($"Phase {PhaseID} is already completed.");
            return;
        }

        IsCompleted = true;
        Debug.Log($"[Quest Phase] Completed: {PhaseID} ({Description})");
    }

    /// <summary>
    /// Phase 정보를 문자열로 반환
    /// </summary>
    public override string ToString()
    {
        return $"[Phase {PhaseID}] Type: {PhaseType}, Content: {ContentID}, Completed: {IsCompleted}";
    }
}
