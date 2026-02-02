using UnityEngine;
using MyAssets.Runtime.Data.Quest;
using MyAssets.Runtime.Systems.Quest;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 시스템의 중앙 관리자
/// 책임: 퀘스트 생명주기 관리 (시작/완료), 초기화, 세이브/로드 조율
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private QuestDatabase questDatabase;

    [Header("Options")]
    [SerializeField] private bool autoSave = true;
    [Tooltip("자동 저장 (Phase 완료 시마다)")]

    [SerializeField] private bool loadOnStart = true;
    [Tooltip("게임 시작 시 자동 로드")]

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

    private void OnApplicationQuit()
    {
        // 게임 종료 시 자동 저장
        if (autoSave && isInitialized)
        {
            SaveProgress();
        }
    }

    #endregion

    #region Initialization

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

        // QuestDatabase 확인
        if (questDatabase == null)
        {
            Debug.LogError("[QuestManager] QuestDatabase is not assigned!");
            return;
        }

        // QuestDatabase 초기화
        questDatabase.Initialize();

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

        // 자동 로드
        if (loadOnStart)
        {
            LoadProgress();
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        QuestEvents.OnQuestCompleted -= OnQuestCompleted;
        QuestEvents.OnObjectiveCompleted -= OnObjectiveCompleted;
        QuestEvents.OnPhaseCompleted -= OnPhaseCompleted;
    }

    #endregion

    #region Quest Management

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

        // 이미 활성화된 퀘스트인지 확인
        if (progressTracker.IsQuestActive(questID))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is already active.");
            return;
        }

        // 이미 완료한 퀘스트인지 확인
        if (progressTracker.IsQuestCompleted(questID))
        {
            Debug.LogWarning($"[QuestManager] Quest {questID} is already completed.");
            return;
        }

        // QuestData 가져오기
        QuestData questData = questDatabase.GetQuestData(questID);
        if (questData == null)
        {
            Debug.LogError($"[QuestManager] Quest {questID} not found in database.");
            return;
        }

        // Quest 인스턴스 생성
        Quest quest = new Quest(questData);
        Debug.Log($"[QuestManager] Quest Started: {quest.QuestName} ({questID})");

        // ProgressTracker에 추가
        progressTracker.AddActiveQuest(quest);

        // 이벤트 발생
        QuestEvents.TriggerQuestStarted(quest);


        // if (showDebugLogs)
        // {
        //     Debug.Log($"[QuestManager] Quest Started: {quest.QuestName} ({questID})");
        // }

        // 자동 저장
        if (autoSave)
        {
            SaveProgress();
        }
    }

    /// <summary>
    /// Phase 완료 처리
    /// </summary>
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

        // Phase 완료 이벤트
        var phase = quest.GetPhase(objectiveID, phaseID);
        if (phase != null && phase.IsCompleted)
        {
            QuestEvents.TriggerPhaseCompleted(phase);
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

        // 자동 저장
        if (autoSave)
        {
            SaveProgress();
        }
    }

    #endregion

    #region Query Methods (ProgressTracker에 위임)

    /// <summary>
    /// 활성 퀘스트 가져오기
    /// </summary>
    public Quest GetActiveQuest(string questID)
    {
        return progressTracker?.GetActiveQuest(questID);
    }

    /// <summary>
    /// 모든 활성 퀘스트 가져오기
    /// </summary>
    public List<Quest> GetAllActiveQuests()
    {
        return progressTracker?.GetAllActiveQuests() ?? new List<Quest>();
    }

    /// <summary>
    /// 완료된 퀘스트 가져오기
    /// </summary>
    public Quest GetCompletedQuest(string questID)
    {
        return progressTracker?.GetCompletedQuest(questID);
    }

    /// <summary>
    /// 퀘스트가 활성화되어 있는지 확인
    /// </summary>
    public bool IsQuestActive(string questID)
    {
        return progressTracker?.IsQuestActive(questID) ?? false;
    }

    /// <summary>
    /// 퀘스트가 완료되었는지 확인
    /// </summary>
    public bool IsQuestCompleted(string questID)
    {
        return progressTracker?.IsQuestCompleted(questID) ?? false;
    }

    /// <summary>
    /// 퀘스트 진행도 가져오기 (0.0 ~ 1.0)
    /// </summary>
    public float GetQuestProgress(string questID)
    {
        return progressTracker?.GetQuestProgress(questID) ?? 0f;
    }

    #endregion

    #region Save/Load (SaveSystem에 위임)

    /// <summary>
    /// 현재 진행 상태 저장
    /// </summary>
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

    /// <summary>
    /// 저장된 진행 상태 로드
    /// </summary>
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

        // 기존 진행 상태 초기화
        progressTracker.ClearAll();

        // 활성 퀘스트 복원
        foreach (var progressData in saveData.activeQuests)
        {
            RestoreQuest(progressData);
        }

        // 완료된 퀘스트 복원
        foreach (var questID in saveData.completedQuestIDs)
        {
            RestoreCompletedQuest(questID);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[QuestManager] ✓ Loaded {saveData.activeQuests.Count} active, {saveData.completedQuestIDs.Count} completed quests");
        }
    }

    /// <summary>
    /// 퀘스트 복원 (세이브 데이터로부터)
    /// </summary>
    private void RestoreQuest(QuestProgressData progressData)
    {
        // QuestData 가져오기
        QuestData questData = questDatabase.GetQuestData(progressData.questID);
        if (questData == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot restore quest {progressData.questID} - not found in database.");
            return;
        }

        // Quest 인스턴스 생성
        Quest quest = new Quest(questData);

        // Phase 완료 상태 복원
        foreach (var phaseID in progressData.completedPhaseIDs)
        {
            // phaseID에서 objectiveID 추출 (예: MQ-01-P01 → MQ-01-OBJ-01)
            string objectiveID = ExtractObjectiveIDFromPhaseID(phaseID, quest);
            if (!string.IsNullOrEmpty(objectiveID))
            {
                quest.CompletePhase(objectiveID, phaseID);
            }
        }

        // ProgressTracker에 추가
        progressTracker.AddActiveQuest(quest);
    }

    /// <summary>
    /// 완료된 퀘스트 복원
    /// </summary>
    private void RestoreCompletedQuest(string questID)
    {
        QuestData questData = questDatabase.GetQuestData(questID);
        if (questData == null)
        {
            Debug.LogWarning($"[QuestManager] Cannot restore completed quest {questID} - not found in database.");
            return;
        }

        Quest quest = new Quest(questData);
        quest.Complete(); // 완료 상태로 설정

        progressTracker.MoveToCompleted(quest);
    }

    /// <summary>
    /// PhaseID로부터 ObjectiveID 추출
    /// </summary>
    private string ExtractObjectiveIDFromPhaseID(string phaseID, Quest quest)
    {
        // phaseID 형식: MQ-01-P01, MQ-01-P02 등
        // 각 Objective의 Phase 목록에서 phaseID와 일치하는 것 찾기
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

    /// <summary>
    /// 세이브 파일 삭제
    /// </summary>
    public void DeleteSaveFile()
    {
        saveSystem?.DeleteSaveFile();
    }

    #endregion

    #region Event Handlers

    private void OnQuestCompleted(Quest quest)
    {
        // ProgressTracker로 이동
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

    /// <summary>
    /// 현재 진행 상태 출력
    /// </summary>
    [ContextMenu("Print Quest Progress")]
    public void PrintQuestProgress()
    {
        progressTracker?.PrintStatus();
    }

    /// <summary>
    /// 세이브 파일 내용 출력
    /// </summary>
    [ContextMenu("Print Save File")]
    public void PrintSaveFile()
    {
        saveSystem?.PrintSaveFileContent();
    }

    /// <summary>
    /// 강제 저장
    /// </summary>
    [ContextMenu("Force Save")]
    public void ForceSave()
    {
        SaveProgress();
    }

    /// <summary>
    /// 강제 로드
    /// </summary>
    [ContextMenu("Force Load")]
    public void ForceLoad()
    {
        LoadProgress();
    }

    #endregion
}