using UnityEngine;
using System.Collections.Generic;

public class QuestUIPresenter : MonoBehaviour
{
    [SerializeField] private QuestUIView view;

    [Header("Event Channels")]
    [SerializeField] private QuestGameEvent onQuestStartedEvent;
    [SerializeField] private QuestGameEvent onQuestCompletedEvent;
    [SerializeField] private QuestObjectiveGameEvent onObjectiveCompletedEvent;
    [SerializeField] private QuestPhaseGameEvent onPhaseCompletedEvent;

    // Model (QuestManager 연동 - 읽기 전용)
    private QuestManager questManager;
    
    // 추적 중인 퀘스트
    private HashSet<string> trackedQuests = new HashSet<string>();
    
    private void Awake()
    {
        if (view == null)
            view = GetComponent<QuestUIView>();
    }
    
    private void Start()
    {
        // QuestManager 찾기
        questManager = FindObjectOfType<QuestManager>();
        
        if (questManager == null)
        {
            Debug.LogError("QuestManager not found!");
            return;
        }
        
        // View 이벤트 등록
        view.OnExpandRequested += HandleExpand;
        view.OnCollapseRequested += HandleCollapse;
        
        // SO 이벤트 등록
        RegisterEvents();
        
        // 초기 퀘스트 로드 (저장된 퀘스트가 있다면)
        LoadActiveQuests();
    }
    
    private void RegisterEvents()
    {
        onQuestStartedEvent?.Register(OnQuestStarted);
        onObjectiveCompletedEvent?.Register(OnObjectiveCompleted);
        onQuestCompletedEvent?.Register(OnQuestCompleted);
        onPhaseCompletedEvent?.Register(OnPhaseCompleted);
    }

    private void UnregisterEvents()
    {
        onQuestStartedEvent?.Unregister(OnQuestStarted);
        onObjectiveCompletedEvent?.Unregister(OnObjectiveCompleted);
        onQuestCompletedEvent?.Unregister(OnQuestCompleted);
        onPhaseCompletedEvent?.Unregister(OnPhaseCompleted);
    }
    
    /// <summary>
    /// 퀘스트 시작 처리
    /// </summary>
    private void OnQuestStarted(Quest quest)
    {
        if (quest == null) return;
        
        // UI에 퀘스트 추가
        bool isMain = quest.QuestType == QuestType.MainQuest;
        view.AddQuestItem(quest.QuestID, quest.QuestType, quest.QuestName, isMain);
        
        // 현재 활성화된 목표들 추가
        var objectives = quest.GetAllObjectives();
        foreach (var objective in objectives)
        {
            // 첫 번째 Objective만 표시 (또는 활성 상태인 것만)
            if (!objective.IsCompleted)
            {
                view.AddObjective(quest.QuestID, objective.ObjectiveID, objective.Description);
                break; // 첫 번째만 표시
            }
        }
        
        // 추적 목록에 추가
        trackedQuests.Add(quest.QuestID);
        
        // New Indicator 표시
        view.ShowNewIndicator();
        
        // 시스템 메시지 출력
        ShowSystemMessage($"<{GetQuestTypeText(quest.QuestType)}> {quest.QuestName}");
    }
    
    /// <summary>
    /// Phase 완료 처리 (새로운 Objective 활성화 체크)
    /// </summary>
    private void OnPhaseCompleted(QuestPhase phase)
    {
        if (phase == null) return;
        
        // Phase가 완료되면 Objective 완료 여부를 OnObjectiveCompleted에서 처리
    }
    
    /// <summary>
    /// Objective 완료 처리
    /// </summary>
    private void OnObjectiveCompleted(QuestObjective objective)
    {
        if (objective == null) return;
        
        // 어느 Quest에 속한 Objective인지 찾기
        Quest parentQuest = FindQuestByObjective(objective);
        if (parentQuest == null) return;
        
        // UI에서 목표 완료 표시
        view.CompleteObjective(parentQuest.QuestID, objective.ObjectiveID);
        
        // 다음 Objective가 있다면 추가
        var nextObjective = GetNextObjective(parentQuest, objective);
        if (nextObjective != null)
        {
            view.AddObjective(parentQuest.QuestID, nextObjective.ObjectiveID, nextObjective.Description);
            
            // New Indicator 표시
            view.ShowNewIndicator();
            
            // 시스템 메시지
            ShowSystemMessage($"<{GetQuestTypeText(parentQuest.QuestType)}> {parentQuest.QuestName} - 새로운 목표!");
        }
    }
    
    /// <summary>
    /// 퀘스트 완료 처리
    /// </summary>
    private void OnQuestCompleted(Quest quest)
    {
        if (quest == null) return;
        
        // UI에서 퀘스트 제거
        view.RemoveQuest(quest.QuestID);
        
        // 추적 목록에서 제거
        trackedQuests.Remove(quest.QuestID);
        
        // 시스템 메시지 출력
        ShowSystemMessage($"<{GetQuestTypeText(quest.QuestType)}> {quest.QuestName} - 완료!");
    }
    
    /// <summary>
    /// 퀘스트 창 펼치기
    /// </summary>
    private void HandleExpand()
    {
        view.Expand();
    }
    
    /// <summary>
    /// 퀘스트 창 접기
    /// </summary>
    private void HandleCollapse()
    {
        view.Collapse();
    }
    
    /// <summary>
    /// 활성화된 퀘스트 로드 (게임 시작/로드 시)
    /// </summary>
    private void LoadActiveQuests()
    {
        if (questManager == null) return;
        
        // QuestManager에서 활성 퀘스트 가져오기
        var activeQuests = questManager.GetAllActiveQuests();
        
        foreach (var quest in activeQuests)
        {
            OnQuestStarted(quest);
        }
    }
    
    /// <summary>
    /// Objective가 속한 Quest 찾기
    /// </summary>
    private Quest FindQuestByObjective(QuestObjective objective)
    {
        var activeQuests = questManager.GetAllActiveQuests();
        
        foreach (var quest in activeQuests)
        {
            var objectives = quest.GetAllObjectives();
            foreach (var obj in objectives)
            {
                if (obj.ObjectiveID == objective.ObjectiveID)
                {
                    return quest;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 다음 Objective 가져오기
    /// </summary>
    private QuestObjective GetNextObjective(Quest quest, QuestObjective currentObjective)
    {
        var allObjectives = quest.GetAllObjectives();
        
        for (int i = 0; i < allObjectives.Count; i++)
        {
            if (allObjectives[i].ObjectiveID == currentObjective.ObjectiveID)
            {
                // 다음 Objective 반환
                if (i + 1 < allObjectives.Count)
                {
                    return allObjectives[i + 1];
                }
                break;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 시스템 메시지 출력
    /// </summary>
    private void ShowSystemMessage(string message)
    {
        // SystemMessageManager가 있다면 연동
        // 예: SystemMessageManager.Instance?.ShowMessage(message);
        Debug.Log($"[Quest System] {message}");
    }
    
    /// <summary>
    /// 퀘스트 타입 텍스트 변환
    /// </summary>
    private string GetQuestTypeText(QuestType questType)
    {
        return questType == QuestType.MainQuest ? "메인" : "서브";
    }
    
    private void OnDestroy()
    {
        // View 이벤트 해제
        if (view != null)
        {
            view.OnExpandRequested -= HandleExpand;
            view.OnCollapseRequested -= HandleCollapse;
        }
        
        UnregisterEvents();
    }
}