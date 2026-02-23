using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 로그 패널 View.
/// QuestUIPresenter에 의해 Show/Hide되며, CanvasGroup으로 오버레이 방식으로 표시된다.
/// </summary>
public class QuestUIView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform questListParent;
    [SerializeField] private GameObject questItemPrefab;

    private Dictionary<string, QuestItemView> questItemViews = new Dictionary<string, QuestItemView>();

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        Hide();
    }

    public void Show()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void AddQuestEntry(string questId, QuestType questType, string questName, List<QuestObjective> objectives)
    {
        if (questItemViews.ContainsKey(questId))
        {
            Debug.LogWarning($"[QuestUIView] Quest {questId} already in view.");
            return;
        }

        var obj = Instantiate(questItemPrefab, questListParent);
        var questItemView = obj.GetComponent<QuestItemView>();
        questItemView.Initialize(questId, questType, questName, objectives);
        questItemViews.Add(questId, questItemView);
    }

    public void RemoveQuestEntry(string questId)
    {
        if (questItemViews.TryGetValue(questId, out var view))
        {
            Destroy(view.gameObject);
            questItemViews.Remove(questId);
        }
    }

    public void UpdateObjectiveCompleted(string objectiveId)
    {
        foreach (var questItemView in questItemViews.Values)
            questItemView.SetObjectiveCompleted(objectiveId);
    }

    public void ClearAllQuests()
    {
        foreach (var view in questItemViews.Values)
            if (view != null) Destroy(view.gameObject);
        questItemViews.Clear();
    }
}
