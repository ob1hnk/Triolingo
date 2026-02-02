using UnityEngine;

/// <summary>
/// 범용 NPC 클래스
/// 퀘스트와 무관하게도 사용 가능하며, 퀘스트 관련 기능은 선택적으로 설정
/// </summary>
public class NPC : MonoBehaviour, IInteractable
{
    [Header("NPC Info")]
    [SerializeField] private string npcName = "마을 주민";
    
    [Header("Dialogue")]
    [Tooltip("상호작용 시 표시할 대사")]
    [TextArea(2, 4)]
    [SerializeField] private string dialogueText = "안녕하세요!";
    
    [Header("Quest Settings (Optional)")]
    [Tooltip("퀘스트 기능을 사용하려면 체크")]
    [SerializeField] private bool hasQuestAction = false;
    
    [Tooltip("퀘스트 ID (예: MQ-01)")]
    [SerializeField] private string questID;
    
    [Tooltip("NPC 액션 타입")]
    [SerializeField] private NPCQuestAction questAction = NPCQuestAction.None;
    
    [Tooltip("완료할 Objective ID (CompletePhase 전용)")]
    [SerializeField] private string objectiveID;
    
    [Tooltip("완료할 Phase ID (CompletePhase 전용)")]
    [SerializeField] private string phaseID;
    
    [Header("Options")]
    [Tooltip("한 번만 상호작용 가능")]
    [SerializeField] private bool onceOnly = false;
    
    [Tooltip("상호작용 후 대사")]
    [TextArea(2, 4)]
    [SerializeField] private string afterInteractionText = "";
    
    private bool hasInteracted = false;

    #region IInteractable Implementation

    private void Start()
    {
        if (questAction != NPCQuestAction.None)
        {
            hasQuestAction = true;
        }
    }

    public string GetInteractText()
    {
        if (onceOnly && hasInteracted)
        {
            return ""; // 이미 상호작용함
        }
        
        return $"{npcName}와 대화하기 (E)";
    }

    public void Interact()
    {
        // 한 번만 상호작용 체크
        if (onceOnly && hasInteracted)
        {
            // 상호작용 후 대사가 있으면 표시
            if (!string.IsNullOrEmpty(afterInteractionText))
            {
                Debug.Log($"[NPC] {npcName}: {afterInteractionText}");
            }
            else
            {
                Debug.Log($"[NPC] {npcName}: 이미 대화했습니다.");
            }
            return;
        }

        hasInteracted = true;

        // 대사 표시
        ShowDialogue(dialogueText);

        // 퀘스트 액션 실행 (설정되어 있는 경우)
        if (hasQuestAction)
        {
            ExecuteQuestAction();
        }
    }

    #endregion

    #region Dialogue

    /// <summary>
    /// 대사 표시
    /// </summary>
    private void ShowDialogue(string text)
    {
        Debug.Log($"[NPC] {npcName}: {text}");
        // TODO: 나중에 UI 시스템 연동
    }

    #endregion

    #region Quest Actions

    /// <summary>
    /// 퀘스트 관련 액션 실행
    /// </summary>
    private void ExecuteQuestAction()
    {
        // Quest ID가 비어있으면 실행 안 함
        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogWarning($"[NPC] {npcName}: Quest ID가 설정되지 않았습니다!");
            return;
        }

        // QuestManager 확인
        if (Managers.Quest == null)
        {
            Debug.LogError($"[NPC] {npcName}: Managers.Quest is null!");
            return;
        }

        // 액션 실행
        switch (questAction)
        {
            case NPCQuestAction.None:
                // 퀘스트 액션 없음
                break;

            case NPCQuestAction.StartQuest:
                HandleStartQuest();
                break;

            case NPCQuestAction.CompletePhase:
                HandleCompletePhase();
                break;

            case NPCQuestAction.CompleteQuest:
                HandleCompleteQuest();
                break;
        }
    }

    /// <summary>
    /// 퀘스트 시작 처리
    /// </summary>
    private void HandleStartQuest()
    {
        Debug.Log($"[NPC] {npcName}에게서 퀘스트를 받았습니다!");
        Managers.Quest.StartQuest(questID);
    }

    /// <summary>
    /// Phase 완료 처리
    /// </summary>
    private void HandleCompletePhase()
    {
        if (string.IsNullOrEmpty(objectiveID) || string.IsNullOrEmpty(phaseID))
        {
            Debug.LogError($"[NPC] {npcName}: ObjectiveID 또는 PhaseID가 설정되지 않았습니다!");
            return;
        }

        Managers.Quest.CompletePhase(questID, objectiveID, phaseID);
        Debug.Log($"[NPC] {npcName}: Phase 완료!");
    }

    /// <summary>
    /// 퀘스트 완료 처리 (모든 Phase 자동 완료)
    /// </summary>
    private void HandleCompleteQuest()
    {
        var quest = Managers.Quest.GetActiveQuest(questID);
        if (quest == null)
        {
            Debug.LogWarning($"[NPC] {npcName}: Quest {questID}가 활성화되어 있지 않습니다.");
            return;
        }

        // 모든 Objective의 모든 Phase 완료
        foreach (var objective in quest.GetAllObjectives())
        {
            foreach (var phase in objective.GetAllPhases())
            {
                if (!phase.IsCompleted)
                {
                    Managers.Quest.CompletePhase(questID, objective.ObjectiveID, phase.PhaseID);
                }
            }
        }

        Debug.Log($"[NPC] {npcName}: Quest 완료!");
    }

    #endregion

    #region Enums

    /// <summary>
    /// NPC 퀘스트 액션 타입
    /// </summary>
    public enum NPCQuestAction
    {
        None,           // 퀘스트 액션 없음
        StartQuest,     // 퀘스트 시작
        CompletePhase,  // 특정 Phase 완료
        CompleteQuest   // 퀘스트 전체 완료
    }

    #endregion

    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // NPC 위치 시각화
        Color gizmoColor = Color.blue;
        
        if (hasInteracted)
        {
            gizmoColor = Color.green;
        }
        else if (hasQuestAction)
        {
            gizmoColor = Color.yellow;
        }

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        // 선택 시 상호작용 범위 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 3f);
        
        // 퀘스트 정보 표시
        if (hasQuestAction && !string.IsNullOrEmpty(questID))
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"[{questAction}]\n{questID}"
            );
#endif
        }
    }
#endif

    #endregion
}