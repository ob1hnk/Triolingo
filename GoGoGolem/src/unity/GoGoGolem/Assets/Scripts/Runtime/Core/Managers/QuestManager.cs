using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using IngameDebugConsole;
#endif

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
        if (autoSave && isInitialized)
            SaveProgress();
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
            LoadProgress();
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

    /// <summary>
    /// 특정 Objective가 완료됐는지 확인
    /// Active 또는 Completed 퀘스트 모두에서 조회
    /// </summary>
    public bool IsObjectiveCompleted(string questID, string objectiveID)
    {
        Quest quest = progressTracker?.GetActiveQuest(questID)
                ?? progressTracker?.GetCompletedQuest(questID);
        return quest?.IsObjectiveCompleted(objectiveID) ?? false;
    }

    /// <summary>
    /// 특정 Phase가 완료됐는지 확인
    /// Active 또는 Completed 퀘스트 모두에서 조회
    /// </summary>
    public bool IsPhaseCompleted(string questID, string objectiveID, string phaseID)
    {
        Quest quest = progressTracker?.GetActiveQuest(questID)
                ?? progressTracker?.GetCompletedQuest(questID);
        return quest?.IsPhaseCompleted(objectiveID, phaseID) ?? false;
    }

    public float GetQuestProgress(string questID)
    {
        return progressTracker?.GetQuestProgress(questID) ?? 0f;
    }

    public List<Quest> GetAllCompletedQuests()
    {
        return progressTracker?.GetAllCompletedQuests() ?? new List<Quest>();
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

        // 모든 objective/phase를 완료 상태로 복원한 뒤 Complete() 호출
        // (Status만 세팅하면 phase.IsCompleted가 false로 남아 씬 복원 로직이 오동작)
        foreach (var obj in questData.objectives)
            foreach (var phase in obj.phases)
                quest.CompletePhase(obj.objectiveID, phase.phaseID);

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

    /// <summary>
    /// 디버그용: phaseID가 속한 quest를 찾아 그 이전 메인 퀘스트와 같은 quest 내 이전 phase를
    /// 모두 완료 처리하고, 해당 phase가 활성인 상태로 만든다.
    /// 인벤토리 등 부수효과는 씬의 reconciler가 담당.
    /// </summary>
    public void JumpToPhase(string phaseID)
    {
        if (!isInitialized)
        {
            Debug.LogError("[QuestManager] Cannot jump - not initialized!");
            return;
        }

        if (questDatabase == null)
        {
            Debug.LogError("[QuestManager] QuestDatabase null");
            return;
        }

        if (string.IsNullOrEmpty(phaseID))
        {
            Debug.LogError("[QuestManager] phaseID가 비어있음.");
            return;
        }

        // phaseID가 속한 quest 검색
        var ordered = questDatabase.GetAllQuestDataInOrder();
        QuestData targetData = null;
        int targetIndex = -1;
        for (int i = 0; i < ordered.Count; i++)
        {
            var q = ordered[i];
            if (q == null) continue;
            foreach (var obj in q.objectives)
            {
                foreach (var phase in obj.phases)
                {
                    if (phase.phaseID == phaseID)
                    {
                        targetData = q;
                        targetIndex = i;
                        break;
                    }
                }
                if (targetData != null) break;
            }
            if (targetData != null) break;
        }

        if (targetData == null)
        {
            Debug.LogError($"[QuestManager] phaseID '{phaseID}'가 어떤 quest에도 없음.");
            return;
        }

        if (targetData.questType != QuestType.MainQuest)
        {
            Debug.LogError($"[QuestManager] {targetData.questID}는 MainQuest가 아님. jumpto는 메인 퀘스트만 지원.");
            return;
        }

        progressTracker.ClearAll();

        int completedQuestCount = 0;
        for (int i = 0; i < targetIndex; i++)
        {
            var q = ordered[i];
            if (q == null || q.questType != QuestType.MainQuest) continue;
            RestoreCompletedQuest(q.questID);
            completedQuestCount++;
        }

        StartQuest(targetData.questID);

        // Quest.CompletePhase()를 직접 호출 (이벤트 안 터트림 → dialogue 스팸 방지)
        int completedPhaseCount = 0;
        var activeQuest = progressTracker.GetActiveQuest(targetData.questID);
        if (activeQuest != null)
        {
            bool reachedTarget = false;
            foreach (var obj in targetData.objectives)
            {
                foreach (var phase in obj.phases)
                {
                    if (phase.phaseID == phaseID) { reachedTarget = true; break; }
                    activeQuest.CompletePhase(obj.objectiveID, phase.phaseID);
                    completedPhaseCount++;
                }
                if (reachedTarget) break;
            }
        }

        SaveProgress();

        Debug.Log($"[QuestManager] JumpTo {targetData.questID} / {phaseID} 완료 (이전 메인 퀘스트 {completedQuestCount}개, 동일 quest 내 phase {completedPhaseCount}개 완료). 씬 재진입 시 reconciler 적용.");
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Debug Console Commands

    [ConsoleMethod("quest.list", "메인 퀘스트 진행 순서와 현재 상태 출력")]
    public static void Cmd_ListQuests()
    {
        var qm = Managers.Quest;
        if (qm == null || qm.questDatabase == null)
        {
            Debug.LogError("[quest.list] QuestManager 또는 QuestDatabase 없음");
            return;
        }

        var ordered = qm.questDatabase.GetAllQuestDataInOrder();
        int idx = 0;
        foreach (var q in ordered)
        {
            if (q == null || q.questType != QuestType.MainQuest) continue;
            string status = qm.IsQuestCompleted(q.questID) ? "✓완료"
                          : qm.IsQuestActive(q.questID) ? "▶진행중"
                          : "·";
            Debug.Log($"[{idx++:D2}] {q.questID} {status} - {q.questName}");
        }
    }

    [ConsoleMethod("quest.jumpto", "phaseID 이전을 모두 완료 처리하고 해당 phase가 활성인 상태로 만듦. 예: quest.jumpto MQ-03-P04 (MQ-03의 첫 phase로 가려면 MQ-03-P01 입력)")]
    public static void Cmd_JumpToPhase(string phaseID)
    {
        var qm = Managers.Quest;
        if (qm == null) { Debug.LogError("[quest.jumpto] QuestManager 없음"); return; }
        qm.JumpToPhase(phaseID);
    }

    [ConsoleMethod("quest.reset", "모든 퀘스트 진행 초기화 + 세이브 삭제")]
    public static void Cmd_ResetQuests()
    {
        var qm = Managers.Quest;
        if (qm == null) { Debug.LogError("[quest.reset] QuestManager 없음"); return; }
        qm.DeleteSaveFile();
        Debug.Log("[quest.reset] 모든 퀘스트 진행 초기화 및 세이브 삭제 완료. 씬 재진입 권장.");
    }

    #endregion
#endif
}