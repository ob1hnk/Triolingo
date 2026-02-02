using System.Collections.Generic;
using UnityEngine;
using MyAssets.Runtime.Data.Quest;
using MyAssets.Runtime.Systems.Quest;

/// <summary>
/// 모든 퀘스트의 진행 상태를 관리하는 중앙 매니저
/// Managers.cs에 통합되어 사용됩니다.
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private QuestDatabase questDatabase;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Dictionary<string, Quest> activeQuests = new Dictionary<string, Quest>();
    private Dictionary<string, Quest> completedQuests = new Dictionary<string, Quest>();

    private bool isInitialized = false;

    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// QuestManager 초기화
    /// </summary>
    private void Initialize()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[QuestManager] Already initialized.");
            return;
        }

        if (questDatabase == null)
        {
            Debug.LogError("[QuestManager] QuestDatabase is not assigned!");
            return;
        }

        questDatabase.Initialize();
        
        // 이벤트 구독
        QuestEvents.OnQuestCompleted += OnQuestCompleted;
        QuestEvents.OnObjectiveCompleted += OnObjectiveCompleted;
        QuestEvents.OnPhaseCompleted += OnPhaseCompleted;

        isInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log("[QuestManager] Initialized successfully.");
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        QuestEvents.OnQuestCompleted -= OnQuestCompleted;
        QuestEvents.OnObjectiveCompleted -= OnObjectiveCompleted;
        QuestEvents.OnPhaseCompleted -= OnPhaseCompleted;
    }

    /// <summary>
    /// 퀘스트 시작
    /// </summary>
    public void StartQuest(string questID)
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Not initialized yet!");
            return;
        }

        if (activeQuests.ContainsKey(questID))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is already active.");
            return;
        }

        if (completedQuests.ContainsKey(questID))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is already completed.");
            return;
        }

        QuestData questData = questDatabase.GetQuestData(questID);
        if (questData == null)
        {
            Debug.LogError($"[QuestManager] Quest {questID} not found in database.");
            return;
        }

        Quest quest = new Quest(questData);
        activeQuests.Add(questID, quest);
        QuestEvents.TriggerQuestStarted(quest);

        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Quest Started: {quest.QuestName} ({questID})");
        }
    }

    /// <summary>
    /// Phase 완료 처리
    /// </summary>
    public void CompletePhase(string questID, string objectiveID, string phaseID)
    {
        if (!activeQuests.TryGetValue(questID, out var quest))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is not active.");
            return;
        }

        quest.CompletePhase(objectiveID, phaseID);
        
        var phase = quest.GetPhase(objectiveID, phaseID);
        if (phase != null && phase.IsCompleted)
        {
            QuestEvents.TriggerPhaseCompleted(phase);
        }

        if (quest.IsObjectiveCompleted(objectiveID))
        {
            var objective = quest.GetObjective(objectiveID);
            QuestEvents.TriggerObjectiveCompleted(objective);
        }

        if (quest.IsCompleted())
        {
            QuestEvents.TriggerQuestCompleted(quest);
        }
    }

    public Quest GetActiveQuest(string questID)
    {
        activeQuests.TryGetValue(questID, out var quest);
        return quest;
    }

    public List<Quest> GetAllActiveQuests()
    {
        return new List<Quest>(activeQuests.Values);
    }

    public Quest GetCompletedQuest(string questID)
    {
        completedQuests.TryGetValue(questID, out var quest);
        return quest;
    }

    public bool IsQuestActive(string questID)
    {
        return activeQuests.ContainsKey(questID);
    }

    public bool IsQuestCompleted(string questID)
    {
        return completedQuests.ContainsKey(questID);
    }

    #region Event Handlers

    private void OnQuestCompleted(Quest quest)
    {
        if (activeQuests.ContainsKey(quest.QuestID))
        {
            activeQuests.Remove(quest.QuestID);
            completedQuests.Add(quest.QuestID, quest);

            if (showDebugLogs)
            {
                Debug.Log($"[QuestManager] Quest Completed: {quest.QuestName}");
            }
        }
    }

    private void OnObjectiveCompleted(QuestObjective objective)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Objective Completed: {objective.Description}");
        }
    }

    private void OnPhaseCompleted(QuestPhase phase)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Phase Completed: {phase.PhaseID}");
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Print Active Quests")]
    public void PrintActiveQuests()
    {
        Debug.Log($"=== Active Quests ({activeQuests.Count}) ===");
        foreach (var quest in activeQuests.Values)
        {
            Debug.Log(quest.ToString());
            foreach (var objective in quest.GetAllObjectives())
            {
                Debug.Log($"  {objective}");
            }
        }
    }

    [ContextMenu("Print Completed Quests")]
    public void PrintCompletedQuests()
    {
        Debug.Log($"=== Completed Quests ({completedQuests.Count}) ===");
        foreach (var quest in completedQuests.Values)
        {
            Debug.Log(quest.ToString());
        }
    }

    #endregion
}