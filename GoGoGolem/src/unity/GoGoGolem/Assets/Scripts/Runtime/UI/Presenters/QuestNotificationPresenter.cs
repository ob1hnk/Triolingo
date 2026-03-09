using UnityEngine;

/// <summary>
/// 퀘스트 알림 Presenter.
/// - 퀘스트 시작: "&lt;메인&gt; 퀘스트명"
/// - 새로운 Objective 해금: "&lt;메인&gt; 퀘스트명 - 새로운 목표!"
/// - 퀘스트 완료: "&lt;메인&gt; 퀘스트명 - 완료!"
/// </summary>
public class QuestNotificationPresenter : MonoBehaviour
{
    [SerializeField] private QuestNotificationView view;

    [Header("Event Channels")]
    [SerializeField] private QuestGameEvent onQuestStartedEvent;
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private QuestObjectiveGameEvent onObjectiveCompletedEvent;

    private QuestManager questManager;

    private void Start()
    {
        questManager = FindObjectOfType<QuestManager>();

        onQuestStartedEvent?.Register(OnQuestStarted);
        onQuestCompletedEvent?.Register(OnQuestCompleted);
        onObjectiveCompletedEvent?.Register(OnObjectiveCompleted);
    }

    private void OnDestroy()
    {
        onQuestStartedEvent?.Unregister(OnQuestStarted);
        onQuestCompletedEvent?.Unregister(OnQuestCompleted);
        onObjectiveCompletedEvent?.Unregister(OnObjectiveCompleted);
    }

    private void OnQuestStarted(Quest quest)
    {
        view.ShowNotification(FormatQuestHeader(quest));
    }

    private void OnObjectiveCompleted(QuestObjective objective)
    {
        // 퀘스트가 완료된 경우 OnQuestCompleted에서 "완료!" 알림을 처리함
        Quest quest = FindQuestContainingObjective(objective.ObjectiveID);
        if (quest == null || quest.IsCompleted()) return;

        view.ShowNotification(FormatQuestHeader(quest), "새로운 목표!");
    }

    private void OnQuestCompleted(Quest quest)
    {
        view.ShowNotification(FormatQuestHeader(quest), "완료!");
    }

    private string FormatQuestHeader(Quest quest)
    {
        string label = quest.QuestType == QuestType.MainQuest ? "<메인>" : "<서브>";
        return $"{label} {quest.QuestName}";
    }

    private Quest FindQuestContainingObjective(string objectiveID)
    {
        if (questManager == null) return null;
        foreach (var quest in questManager.GetAllActiveQuests())
        {
            if (quest.GetObjective(objectiveID) != null)
                return quest;
        }
        return null;
    }
}