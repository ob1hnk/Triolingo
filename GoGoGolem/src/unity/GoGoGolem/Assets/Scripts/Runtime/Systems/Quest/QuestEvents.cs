using System;

/// <summary>
/// 퀘스트 시스템 이벤트 정의
/// </summary>
public static class QuestEvents
{
    // Quest 이벤트
    public static event Action<Quest> OnQuestStarted;
    public static event Action<Quest> OnQuestCompleted;
    public static event Action<Quest> OnQuestFailed;

    // Objective 이벤트
    public static event Action<QuestObjective> OnObjectiveCompleted;

    // Phase 이벤트
    public static event Action<QuestPhase> OnPhaseCompleted;

    /// <summary>
    /// Quest 시작 이벤트 발생
    /// </summary>
    public static void TriggerQuestStarted(Quest quest)
    {
        OnQuestStarted?.Invoke(quest);
    }

    /// <summary>
    /// Quest 완료 이벤트 발생
    /// </summary>
    public static void TriggerQuestCompleted(Quest quest)
    {
        OnQuestCompleted?.Invoke(quest);
    }

    /// <summary>
    /// Quest 실패 이벤트 발생
    /// </summary>
    public static void TriggerQuestFailed(Quest quest)
    {
        OnQuestFailed?.Invoke(quest);
    }

    /// <summary>
    /// Objective 완료 이벤트 발생
    /// </summary>
    public static void TriggerObjectiveCompleted(QuestObjective objective)
    {
        OnObjectiveCompleted?.Invoke(objective);
    }

    /// <summary>
    /// Phase 완료 이벤트 발생
    /// </summary>
    public static void TriggerPhaseCompleted(QuestPhase phase)
    {
        OnPhaseCompleted?.Invoke(phase);
    }

    /// <summary>
    /// 모든 이벤트 구독 해제
    /// </summary>
    public static void ClearAllEvents()
    {
        OnQuestStarted = null;
        OnQuestCompleted = null;
        OnQuestFailed = null;
        OnObjectiveCompleted = null;
        OnPhaseCompleted = null;
    }
}
