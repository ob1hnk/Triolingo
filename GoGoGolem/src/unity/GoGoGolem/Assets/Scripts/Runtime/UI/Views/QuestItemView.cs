using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 로그 내 개별 퀘스트 항목.
/// 퀘스트 이름과 그 퀘스트에 속한 모든 Phase를 표시한다.
/// </summary>
public class QuestItemView : MonoBehaviour
{
    [Header("Quest Header")]
    [SerializeField] private TextMeshProUGUI questTypeText;
    [SerializeField] private TextMeshProUGUI questNameText;

    [Header("Objective List")]
    [SerializeField] private Transform objectiveListParent;
    [SerializeField] private GameObject objectiveItemPrefab;

    private string questId;
    private Dictionary<string, ObjectiveItemView> objectiveViews = new Dictionary<string, ObjectiveItemView>();

    public void Initialize(string questId, QuestType questType, string questName, List<QuestObjective> objectives)
    {
        this.questId = questId;

        if (questType == QuestType.MainQuest)
        {
            questTypeText.text = "<메인퀘스트>";
            questTypeText.color = new Color(1f, 0.86f, 0f);
        }
        else
        {
            questTypeText.text = "<서브퀘스트>";
            questTypeText.color = new Color(0.39f, 0.78f, 1f);
        }

        questNameText.text = questName;

        foreach (var objective in objectives)
            AddObjective(objective);
    }

    private void AddObjective(QuestObjective objective)
    {
        var obj = Instantiate(objectiveItemPrefab, objectiveListParent);
        var objectiveView = obj.GetComponent<ObjectiveItemView>();

        string displayText = string.IsNullOrEmpty(objective.Description) ? objective.ObjectiveID : objective.Description;
        objectiveView.Initialize(objective.ObjectiveID, displayText, objective.IsCompleted);
        objectiveViews[objective.ObjectiveID] = objectiveView;
    }

    public void SetObjectiveCompleted(string objectiveId)
    {
        if (objectiveViews.TryGetValue(objectiveId, out var objectiveView))
            objectiveView.SetCompleted(true);
    }
}
