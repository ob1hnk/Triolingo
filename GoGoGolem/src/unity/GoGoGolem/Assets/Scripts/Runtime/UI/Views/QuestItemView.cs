using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 로그 내 개별 퀘스트 항목.
/// 퀘스트 이름과 그 퀘스트에 속한 모든 Objective를 표시한다.
/// </summary>
public class QuestItemView : MonoBehaviour
{
    [Header("Quest Header")]
    [SerializeField] private TextMeshProUGUI questHeaderText;

    [Header("Objective List")]
    [SerializeField] private Transform objectiveListParent;
    [SerializeField] private GameObject objectiveItemPrefab;

    private string questId;
    private Dictionary<string, ObjectiveItemView> objectiveViews = new Dictionary<string, ObjectiveItemView>();
    private List<string> objectiveOrder = new List<string>();

    public void Initialize(string questId, QuestType questType, string questName, List<QuestObjective> objectives)
    {
        this.questId = questId;

        string typeLabel = questType == QuestType.MainQuest ? "<메인>" : "<서브>";
        questHeaderText.text = $"{typeLabel} {questName}";

        for (int i = 0; i < objectives.Count; i++)
            AddObjective(objectives[i], i == 0);
    }

    private void AddObjective(QuestObjective objective, bool visible)
    {
        var obj = Instantiate(objectiveItemPrefab, objectiveListParent);
        var objectiveView = obj.GetComponent<ObjectiveItemView>();

        string displayText = string.IsNullOrEmpty(objective.Description) ? objective.ObjectiveID : objective.Description;
        objectiveView.Initialize(objective.ObjectiveID, displayText, objective.IsCompleted);
        objectiveView.SetVisible(visible);
        objectiveViews[objective.ObjectiveID] = objectiveView;
        objectiveOrder.Add(objective.ObjectiveID);
    }

    public void SetObjectiveCompleted(string objectiveId)
    {
        if (!objectiveViews.TryGetValue(objectiveId, out var objectiveView)) return;

        objectiveView.SetCompleted(true);

        int idx = objectiveOrder.IndexOf(objectiveId);
        if (idx >= 0 && idx + 1 < objectiveOrder.Count)
        {
            var nextId = objectiveOrder[idx + 1];
            if (objectiveViews.TryGetValue(nextId, out var nextView))
                nextView.SetVisible(true);
        }
    }
}
