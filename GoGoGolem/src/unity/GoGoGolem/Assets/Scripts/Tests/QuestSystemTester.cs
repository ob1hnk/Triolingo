using UnityEngine;

/// <summary>
/// 퀘스트 시스템 테스터
/// Managers를 통해 QuestManager에 접근합니다.
/// 씬에 배치하여 키보드 입력으로 퀘스트를 테스트할 수 있습니다.
/// </summary>
public class QuestSystemTester : MonoBehaviour
{
    [Header("Test Quest Settings")]
    [Tooltip("테스트할 Quest ID")]
    [SerializeField] private string testQuestID = "MQ-01";

    [Tooltip("테스트할 Objective ID")]
    [SerializeField] private string testObjectiveID = "MQ-01-OBJ-01";

    [Tooltip("테스트할 Phase ID")]
    [SerializeField] private string testPhaseID = "MQ-01-P01";

    [Header("Event Channels")]
    [SerializeField] private QuestGameEvent onQuestStartedEvent;
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private QuestObjectiveGameEvent onObjectiveCompletedEvent;
    [SerializeField] private QuestPhaseGameEvent onPhaseCompletedEvent;

    [Header("Options")]
    [SerializeField] private bool showHelpOnStart = true;

    private void Start()
    {
        if (showHelpOnStart)
        {
            PrintHelp();
        }

        if (Managers.Quest == null)
        {
            Debug.LogError("[QuestSystemTester] Managers.Quest is null! Make sure Managers GameObject has QuestManager component.");
            return;
        }
    }

    private void OnEnable()
    {
        onQuestStartedEvent?.Register(OnQuestStarted);
        onQuestCompletedEvent?.Register(OnQuestCompleted);
        onObjectiveCompletedEvent?.Register(OnObjectiveCompleted);
        onPhaseCompletedEvent?.Register(OnPhaseCompleted);
    }

    private void OnDisable()
    {
        onQuestStartedEvent?.Unregister(OnQuestStarted);
        onQuestCompletedEvent?.Unregister(OnQuestCompleted);
        onObjectiveCompletedEvent?.Unregister(OnObjectiveCompleted);
        onPhaseCompletedEvent?.Unregister(OnPhaseCompleted);
    }

    private void Update()
    {
        // 키보드 입력 처리
        if (Input.GetKeyDown(KeyCode.F1))
        {
            PrintHelp();
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestStartQuest();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestCompletePhase();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestPrintActiveQuests();
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TestPrintQuestProgress();
        }
    }

    #region Test Methods

    /// <summary>
    /// 퀘스트 시작 테스트
    /// </summary>
    [ContextMenu("Test: Start Quest")]
    public void TestStartQuest()
    {
        if (Managers.Quest == null)
        {
            Debug.LogError("[Tester] Managers.Quest is null!");
            return;
        }

        Debug.Log($"[Tester] === Testing Start Quest: {testQuestID} ===");
        Managers.Quest.StartQuest(testQuestID);
    }

    /// <summary>
    /// Phase 완료 테스트
    /// </summary>
    [ContextMenu("Test: Complete Phase")]
    public void TestCompletePhase()
    {
        if (Managers.Quest == null)
        {
            Debug.LogError("[Tester] Managers.Quest is null!");
            return;
        }

        Debug.Log($"[Tester] === Testing Complete Phase: {testPhaseID} ===");
        Managers.Quest.CompletePhase(testQuestID, testObjectiveID, testPhaseID);
    }

    /// <summary>
    /// 활성 퀘스트 출력 테스트
    /// </summary>
    [ContextMenu("Test: Print Active Quests")]
    public void TestPrintActiveQuests()
    {
        if (Managers.Quest == null)
        {
            Debug.LogError("[Tester] Managers.Quest is null!");
            return;
        }

        Debug.Log("[Tester] === Testing Print Active Quests ===");
        
        var activeQuests = Managers.Quest.GetAllActiveQuests();
        Debug.Log($"Active Quests Count: {activeQuests.Count}");

        foreach (var quest in activeQuests)
        {
            Debug.Log($"  {quest}");
        }
    }

    /// <summary>
    /// 퀘스트 진행도 출력 테스트
    /// </summary>
    [ContextMenu("Test: Print Quest Progress")]
    public void TestPrintQuestProgress()
    {
        if (Managers.Quest == null)
        {
            Debug.LogError("[Tester] Managers.Quest is null!");
            return;
        }

        Debug.Log("[Tester] === Testing Quest Progress ===");
        
        var quest = Managers.Quest.GetActiveQuest(testQuestID);
        if (quest == null)
        {
            Debug.Log($"Quest {testQuestID} is not active.");
            return;
        }

        Debug.Log($"Quest: {quest.QuestName}");
        Debug.Log($"Status: {quest.Status}");
        Debug.Log($"Progress: {quest.GetProgress() * 100:F1}%");
        Debug.Log($"Objectives:");

        foreach (var objective in quest.GetAllObjectives())
        {
            Debug.Log($"  - {objective.Description} ({objective.GetProgress() * 100:F1}%)");
            
            foreach (var phase in objective.GetAllPhases())
            {
                string status = phase.IsCompleted ? "[✓]" : "[ ]";
                Debug.Log($"    {status} {phase.PhaseID}: {phase.Description}");
            }
        }
    }

    /// <summary>
    /// MQ-01 전체 플로우 자동 테스트
    /// </summary>
    [ContextMenu("Test: Auto Complete MQ-01")]
    public void TestAutoCompleteMQ01()
    {
        if (Managers.Quest == null)
        {
            Debug.LogError("[Tester] Managers.Quest is null!");
            return;
        }

        Debug.Log("[Tester] === Auto Completing MQ-01 ===");

        // 퀘스트 시작
        Managers.Quest.StartQuest("MQ-01");

        // 딜레이를 주고 싶으면 Coroutine 사용
        // 지금은 즉시 실행
        Managers.Quest.CompletePhase("MQ-01", "MQ-01-OBJ-01", "MQ-01-P01");
        Managers.Quest.CompletePhase("MQ-01", "MQ-01-OBJ-01", "MQ-01-P02");
        Managers.Quest.CompletePhase("MQ-01", "MQ-01-OBJ-01", "MQ-01-P03");
    }

    #endregion

    #region Event Handlers

    private void OnQuestStarted(Quest quest)
    {
        Debug.Log($"[Tester Event] ★ Quest Started: {quest.QuestName}");
    }

    private void OnQuestCompleted(Quest quest)
    {
        Debug.Log($"[Tester Event] ★★★ QUEST COMPLETED: {quest.QuestName} ★★★");
    }

    private void OnObjectiveCompleted(QuestObjective objective)
    {
        Debug.Log($"[Tester Event] ★★ Objective Completed: {objective.Description}");
    }

    private void OnPhaseCompleted(QuestPhase phase)
    {
        Debug.Log($"[Tester Event] ★ Phase Completed: {phase.PhaseID}");
    }

    #endregion

    #region Helper Methods

    private void PrintHelp()
    {
        Debug.Log("=== Quest System Tester Help ===");
        Debug.Log("F1: Show this help");
        Debug.Log("1: Start Quest");
        Debug.Log("2: Complete Phase");
        Debug.Log("3: Print Active Quests");
        Debug.Log("4: Print Quest Progress");
        Debug.Log("===================================");
    }

    #endregion

    #region GUI (Optional)

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 400));
        GUILayout.Label("=== Quest System Tester ===");
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("1. Start Quest"))
        {
            TestStartQuest();
        }
        
        if (GUILayout.Button("2. Complete Phase"))
        {
            TestCompletePhase();
        }
        
        if (GUILayout.Button("3. Print Active Quests"))
        {
            TestPrintActiveQuests();
        }
        
        if (GUILayout.Button("4. Print Quest Progress"))
        {
            TestPrintQuestProgress();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Auto Complete MQ-01"))
        {
            TestAutoCompleteMQ01();
        }

        GUILayout.Space(20);
        GUILayout.Label($"Test Quest ID: {testQuestID}");
        GUILayout.Label($"Test Objective ID: {testObjectiveID}");
        GUILayout.Label($"Test Phase ID: {testPhaseID}");

        GUILayout.EndArea();
    }

    #endregion
}
