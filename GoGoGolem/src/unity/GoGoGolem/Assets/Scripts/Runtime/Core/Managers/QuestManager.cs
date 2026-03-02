using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 시스템의 중앙 관리자
/// 책임: 퀘스트 생명주기 관리 (시작/완료), 초기화, 세이브/로드 조율
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private QuestDatabase questDatabase;

    [Header("Event Channels - Notifications")]
    [SerializeField] private QuestGameEvent onQuestStartedEvent;
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private QuestObjectiveGameEvent onObjectiveCompletedEvent;
    [SerializeField] private QuestPhaseGameEvent onPhaseCompletedEvent;

    [Header("Event Channels - Requests")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;
    [SerializeField] private StringGameEvent requestStartDialogueEvent;

    [Header("Options")]
    [SerializeField] private bool autoSave = true;
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool showDebugLogs = true;

    // 협력 객체
    private QuestProgressTracker progressTracker;
    private QuestSaveSystem saveSystem;

    private bool isInitialized = false;

    #region Unity Lifecycle

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (requestStartQuestEvent != null)
            requestStartQuestEvent.Register(HandleStartQuestRequest);
        if (requestCompletePhaseEvent != null)
            requestCompletePhaseEvent.Register(HandleCompletePhaseRequest);
        if (onObjectiveCompletedEvent != null)
            onObjectiveCompletedEvent.Register(OnObjectiveCompleted);
        if (onPhaseCompletedEvent != null)
            onPhaseCompletedEvent.Register(OnPhaseCompleted);
    }

    private void OnDisable()
    {
        if (requestStartQuestEvent != null)
            requestStartQuestEvent.Unregister(HandleStartQuestRequest);
        if (requestCompletePhaseEvent != null)
            requestCompletePhaseEvent.Unregister(HandleCompletePhaseRequest);
        if (onObjectiveCompletedEvent != null)
            onObjectiveCompletedEvent.Unregister(OnObjectiveCompleted);
        if (onPhaseCompletedEvent != null)
            onPhaseCompletedEvent.Unregister(OnPhaseCompleted);
    }

    private void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        if (autoSave && isInitialized)
            SaveProgress();
#endif
    }

    #endregion

    #region Initialization

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

        // 협력 객체 생성
        progressTracker = new QuestProgressTracker();
        saveSystem = new QuestSaveSystem();

        isInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log("[QuestManager] Initialized successfully.");
        }

        if (loadOnStart)
        {
#if !UNITY_EDITOR
            LoadProgress();
#endif
        }
    }

    #endregion

    #region Event Handlers (Requests)

    private void HandleStartQuestRequest(string questID)
    {
        StartQuest(questID);
    }

    private void HandleCompletePhaseRequest(CompletePhaseRequest req)
    {
        CompletePhase(req.QuestID, req.ObjectiveID, req.PhaseID);
    }

    #endregion

    #region Quest Management

    public void StartQuest(string questID)
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Not initialized yet!");
            return;
        }

        if (progressTracker.IsQuestActive(questID))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is already active.");
            return;
        }

        if (progressTracker.IsQuestCompleted(questID))
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
        progressTracker.AddActiveQuest(quest);

        onQuestStartedEvent?.Raise(quest);

        if (autoSave)
        {
            SaveProgress();
        }
    }

    public void CompletePhase(string questID, string objectiveID, string phaseID)
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Not initialized yet!");
            return;
        }

        Quest quest = progressTracker.GetActiveQuest(questID);
        if (quest == null)
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is not active.");
            return;
        }

        // Phase 완료
        quest.CompletePhase(objectiveID, phaseID);

        // Phase 완료 이벤트 및 Dialogue 트리거
        var phase = quest.GetPhase(objectiveID, phaseID);
        if (phase != null && phase.IsCompleted)
        {
            onPhaseCompletedEvent?.Raise(phase);
            
            if (phase.PhaseType == PhaseType.Dialogue && !string.IsNullOrEmpty(phase.ContentID))
            {
                TriggerDialogue(phase.ContentID);
            }
        }

        // Objective 완료 체크
        if (quest.IsObjectiveCompleted(objectiveID))
        {
            var objective = quest.GetObjective(objectiveID);
            onObjectiveCompletedEvent?.Raise(objective);
        }

        // Quest 완료 체크
        if (quest.IsCompleted())
        {
            progressTracker.MoveToCompleted(quest);
            onQuestCompletedEvent?.Raise(quest);

            if (showDebugLogs)
                Debug.Log($"[QuestManager] Quest Completed: {quest.QuestName}");
        }

        if (autoSave)
        {
            SaveProgress();
        }
    }

    #endregion

    #region Dialogue Integration

    private void TriggerDialogue(string dialogueID)
    {
        if (requestStartDialogueEvent == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot trigger dialogue {dialogueID} - RequestStartDialogue event not assigned.");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Triggering dialogue: {dialogueID}");
        }

        requestStartDialogueEvent.Raise(dialogueID);
    }

    #endregion

    #region Query Methods

    public Quest GetActiveQuest(string questID)
    {
        return progressTracker?.GetActiveQuest(questID);
    }

    public List<Quest> GetAllActiveQuests()
    {
        return progressTracker?.GetAllActiveQuests() ?? new List<Quest>();
    }

    public Quest GetCompletedQuest(string questID)
    {
        return progressTracker?.GetCompletedQuest(questID);
    }

    public bool IsQuestActive(string questID)
    {
        return progressTracker?.IsQuestActive(questID) ?? false;
    }

    public bool IsQuestCompleted(string questID)
    {
        return progressTracker?.IsQuestCompleted(questID) ?? false;
    }

    public float GetQuestProgress(string questID)
    {
        return progressTracker?.GetQuestProgress(questID) ?? 0f;
    }

    #endregion

    #region Save/Load

    public void SaveProgress()
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Cannot save - not initialized!");
            return;
        }

        saveSystem.SaveQuests(
            progressTracker.GetActiveQuestsDictionary(),
            progressTracker.GetCompletedQuestsDictionary()
        );
    }

    public void LoadProgress()
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Cannot load - not initialized!");
            return;
        }

        QuestSaveData saveData = saveSystem.LoadQuests();

        if (saveData == null || (saveData.activeQuests.Count == 0 && saveData.completedQuestIDs.Count == 0))
        {
            if (showDebugLogs)
            {
                Debug.Log("[QuestManager] No save data to load.");
            }
            return;
        }

        progressTracker.ClearAll();

        foreach (var progressData in saveData.activeQuests)
        {
            RestoreQuest(progressData);
        }

        foreach (var questID in saveData.completedQuestIDs)
        {
            RestoreCompletedQuest(questID);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Loaded {saveData.activeQuests.Count} active, {saveData.completedQuestIDs.Count} completed quests");
        }
    }

    private void RestoreQuest(QuestProgressData progressData)
    {
        QuestData questData = questDatabase.GetQuestData(progressData.questID);
        if (questData == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot restore quest {progressData.questID} - not found in database.");
            return;
        }

        Quest quest = new Quest(questData);

        foreach (var phaseID in progressData.completedPhaseIDs)
        {
            string objectiveID = ExtractObjectiveIDFromPhaseID(phaseID, quest);
            if (!string.IsNullOrEmpty(objectiveID))
            {
                quest.CompletePhase(objectiveID, phaseID);
            }
        }

        progressTracker.AddActiveQuest(quest);
    }

    private void RestoreCompletedQuest(string questID)
    {
        QuestData questData = questDatabase.GetQuestData(questID);
        if (questData == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot restore completed quest {questID} - not found in database.");
            return;
        }

        Quest quest = new Quest(questData);
        quest.Complete();

        progressTracker.MoveToCompleted(quest);
    }

    private string ExtractObjectiveIDFromPhaseID(string phaseID, Quest quest)
    {
        foreach (var objective in quest.GetAllObjectives())
        {
            foreach (var phase in objective.GetAllPhases())
            {
                if (phase.PhaseID == phaseID)
                {
                    return objective.ObjectiveID;
                }
            }
        }

        return null;
    }

    public void DeleteSaveFile()
    {
        saveSystem?.DeleteSaveFile();
        progressTracker?.ClearAll();
    }

    #endregion

    #region Event Handlers (Notifications)

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

    [ContextMenu("Print Quest Progress")]
    public void PrintQuestProgress()
    {
        progressTracker?.PrintStatus();
    }

    [ContextMenu("Print Save File")]
    public void PrintSaveFile()
    {
        saveSystem?.PrintSaveFileContent();
    }

    [ContextMenu("Force Save")]
    public void ForceSave()
    {
        SaveProgress();
    }

    [ContextMenu("Force Load")]
    public void ForceLoad()
    {
        LoadProgress();
    }

    #endregion
}