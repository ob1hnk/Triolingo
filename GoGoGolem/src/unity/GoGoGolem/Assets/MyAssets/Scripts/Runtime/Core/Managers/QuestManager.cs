using UnityEngine;
using MyAssets.Runtime.Data.Quest;
using MyAssets.Runtime.Systems.Quest;
using System.Collections.Generic;
using MyAssets.Runtime.Systems.Dialogue;

/// <summary>
/// 퀘스트 시스템의 중앙 관리자
/// 책임: 퀘스트 생명주기 관리 (시작/완료), 초기화, 세이브/로드 조율
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private QuestDatabase questDatabase;
    [SerializeField] private DialogueManager dialogueManager;

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

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        QuestEvents.OnQuestCompleted -= OnQuestCompleted;
        QuestEvents.OnObjectiveCompleted -= OnObjectiveCompleted;
        QuestEvents.OnPhaseCompleted -= OnPhaseCompleted;
    }

    private void OnApplicationQuit()
    {
        if (autoSave && isInitialized)
        {
            SaveProgress();
        }
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

        // DialogueManager 자동 찾기
        if (dialogueManager == null)
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
            
            if (dialogueManager == null)
            {
                Debug.LogWarning("[QuestManager] DialogueManager not found in scene.");
            }
        }

        // 협력 객체 생성
        progressTracker = new QuestProgressTracker();
        saveSystem = new QuestSaveSystem();

        // 이벤트 구독
        QuestEvents.OnQuestCompleted += OnQuestCompleted;
        QuestEvents.OnObjectiveCompleted += OnObjectiveCompleted;
        QuestEvents.OnPhaseCompleted += OnPhaseCompleted;

        isInitialized = true;

        if (showDebugLogs)
        {
            Debug.Log("[QuestManager] Initialized successfully.");
        }

        if (loadOnStart)
        {
            LoadProgress();
        }
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

        QuestEvents.TriggerQuestStarted(quest);

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
            QuestEvents.TriggerPhaseCompleted(phase);
            
            if (phase.PhaseType == PhaseType.Dialogue && !string.IsNullOrEmpty(phase.ContentID))
            {
                TriggerDialogue(phase.ContentID);
            }
        }

        // Objective 완료 체크
        if (quest.IsObjectiveCompleted(objectiveID))
        {
            var objective = quest.GetObjective(objectiveID);
            QuestEvents.TriggerObjectiveCompleted(objective);
        }

        // Quest 완료 체크
        if (quest.IsCompleted())
        {
            QuestEvents.TriggerQuestCompleted(quest);
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
        if (dialogueManager == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot trigger dialogue {dialogueID} - DialogueManager not found.");
            return;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Triggering dialogue: {dialogueID}");
        }
        
        dialogueManager.StartDialogue(dialogueID);
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
            Debug.Log($"[QuestManager] ✓ Loaded {saveData.activeQuests.Count} active, {saveData.completedQuestIDs.Count} completed quests");
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
    }

    #endregion

    #region Event Handlers

    private void OnQuestCompleted(Quest quest)
    {
        progressTracker.MoveToCompleted(quest);

        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] Quest Completed: {quest.QuestName}");
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