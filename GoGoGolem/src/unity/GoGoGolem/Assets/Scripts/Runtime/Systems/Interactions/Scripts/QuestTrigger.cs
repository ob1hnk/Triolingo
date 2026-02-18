using UnityEngine;

/// <summary>
/// 씬에 배치하여 특정 조건에서 퀘스트를 시작하거나 Phase를 완료시키는 트리거
/// Managers를 통해 QuestManager에 접근합니다.
/// </summary>
public class QuestTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [Tooltip("트리거 타입")]
    [SerializeField] private TriggerType triggerType = TriggerType.OnTriggerEnter;

    [Header("Quest Info")]
    [Tooltip("관련된 Quest ID (예: MQ-01)")]
    [SerializeField] private string questID;

    [Tooltip("트리거 액션")]
    [SerializeField] private TriggerAction action = TriggerAction.StartQuest;

    [Header("Phase Info (CompletePhase 액션 전용)")]
    [Tooltip("완료할 Objective ID")]
    [SerializeField] private string objectiveID;

    [Tooltip("완료할 Phase ID")]
    [SerializeField] private string phaseID;

    [Header("Options")]
    [Tooltip("트리거 실행 후 자동으로 파괴")]
    [SerializeField] private bool destroyAfterTrigger = true;

    [Tooltip("플레이어 태그")]
    [SerializeField] private string playerTag = "Player";

    private bool hasTriggered = false;

    private void Start()
    {
        // Start 액션이면 시작 시 실행
        if (triggerType == TriggerType.OnStart)
        {
            ExecuteTrigger();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerType == TriggerType.OnTriggerEnter && !hasTriggered)
        {
            if (other.CompareTag(playerTag))
            {
                ExecuteTrigger();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggerType == TriggerType.OnCollisionEnter && !hasTriggered)
        {
            if (collision.gameObject.CompareTag(playerTag))
            {
                ExecuteTrigger();
            }
        }
    }

    /// <summary>
    /// 외부에서 호출 가능한 트리거 실행 메서드
    /// </summary>
    public void ExecuteTrigger()
    {
        if (hasTriggered)
        {
            Debug.LogWarning($"[QuestTrigger] Already triggered: {gameObject.name}");
            return;
        }

        if (string.IsNullOrEmpty(questID))
        {
            Debug.LogError($"[QuestTrigger] QuestID is empty on {gameObject.name}");
            return;
        }

        if (Managers.Quest == null)
        {
            Debug.LogError("[QuestTrigger] Managers.Quest is null! Make sure Managers GameObject exists.");
            return;
        }

        hasTriggered = true;

        // 액션 실행
        switch (action)
        {
            case TriggerAction.StartQuest:
                Managers.Quest.StartQuest(questID);
                Debug.Log($"[QuestTrigger] Started Quest: {questID}");
                break;

            case TriggerAction.CompletePhase:
                if (string.IsNullOrEmpty(objectiveID) || string.IsNullOrEmpty(phaseID))
                {
                    Debug.LogError($"[QuestTrigger] ObjectiveID or PhaseID is empty on {gameObject.name}");
                    return;
                }
                Managers.Quest.CompletePhase(questID, objectiveID, phaseID);
                Debug.Log($"[QuestTrigger] Completed Phase: {questID}/{objectiveID}/{phaseID}");
                break;
        }

        // 트리거 후 파괴
        if (destroyAfterTrigger)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 트리거 타입
    /// </summary>
    public enum TriggerType
    {
        OnStart,            // 씬 시작 시
        OnTriggerEnter,     // Trigger 충돌 시
        OnCollisionEnter,   // Collision 충돌 시
        Manual              // 수동 호출 (ExecuteTrigger)
    }

    /// <summary>
    /// 트리거 액션
    /// </summary>
    public enum TriggerAction
    {
        StartQuest,         // 퀘스트 시작
        CompletePhase       // Phase 완료
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 트리거 영역 시각화
        Gizmos.color = hasTriggered ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        // 선택 시 더 자세한 정보 표시
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
#endif
}
