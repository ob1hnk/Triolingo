using UnityEngine;
using TMPro;
using System.Collections.Generic;
using MyAssets.Runtime.Data.Quest;

public class QuestItemView : MonoBehaviour
{
    [Header("Quest Header")]
    [SerializeField] private TextMeshProUGUI questTypeText;
    [SerializeField] private TextMeshProUGUI questNameText;
    
    [Header("Objectives")]
    [SerializeField] private Transform objectiveListParent;
    [SerializeField] private GameObject objectiveItemPrefab;
    
    private string questId;
    private Dictionary<string, ObjectiveItemView> objectiveViews = new Dictionary<string, ObjectiveItemView>();
    
    /// <summary>
    /// 퀘스트 아이템 초기화
    /// </summary>
    public void Initialize(string questId, QuestType questType, string questName)
    {
        this.questId = questId;
        
        // 퀘스트 타입 설정
        if (questType == QuestType.MainQuest)
        {
            questTypeText.text = "<메인퀘스트>";
            questTypeText.color = new Color(1f, 0.86f, 0f); // 노란색
        }
        else
        {
            questTypeText.text = "<서브퀘스트>";
            questTypeText.color = new Color(0.39f, 0.78f, 1f); // 하늘색
        }
        
        // 퀘스트 이름 설정
        questNameText.text = questName;
    }
    
    /// <summary>
    /// 목표 추가
    /// </summary>
    public void AddObjective(string objectiveId, string objectiveText)
    {
        if (objectiveViews.ContainsKey(objectiveId))
        {
            Debug.LogWarning($"Objective {objectiveId} already exists.");
            return;
        }
        
        GameObject objectiveObj = Instantiate(objectiveItemPrefab, objectiveListParent);
        ObjectiveItemView objectiveView = objectiveObj.GetComponent<ObjectiveItemView>();
        
        if (objectiveView != null)
        {
            objectiveView.Initialize(objectiveId, objectiveText, false);
            objectiveViews.Add(objectiveId, objectiveView);
        }
    }
    
    /// <summary>
    /// 목표 완료 처리
    /// </summary>
    public void CompleteObjective(string objectiveId)
    {
        if (objectiveViews.TryGetValue(objectiveId, out ObjectiveItemView objectiveView))
        {
            objectiveView.SetCompleted(true);
        }
    }
    
    /// <summary>
    /// 모든 목표 제거
    /// </summary>
    public void ClearObjectives()
    {
        foreach (var objectiveView in objectiveViews.Values)
        {
            if (objectiveView != null)
                Destroy(objectiveView.gameObject);
        }
        objectiveViews.Clear();
    }
}