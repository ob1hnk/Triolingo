using UnityEngine;

/// <summary>
/// 씬에 배치하여 특정 조건에서 퀘스트를 시작하거나 Phase를 완료시키는 트리거
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

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

    private bool hasTriggered = false;

    private void Start()
    {
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

        hasTriggered = true;

        switch (action)
        {
            case TriggerAction.StartQuest:
                requestStartQuestEvent?.Raise(questID);
                Debug.Log($"[QuestTrigger] Started Quest: {questID}");
                break;

            case TriggerAction.CompletePhase:
                if (string.IsNullOrEmpty(objectiveID) || string.IsNullOrEmpty(phaseID))
                {
                    Debug.LogError($"[QuestTrigger] ObjectiveID or PhaseID is empty on {gameObject.name}");
                    return;
                }
                requestCompletePhaseEvent?.Raise(new CompletePhaseRequest(questID, objectiveID, phaseID));
                Debug.Log($"[QuestTrigger] Completed Phase: {questID}/{objectiveID}/{phaseID}");
                break;
        }

        if (destroyAfterTrigger)
        {
            Destroy(gameObject);
        }
    }

    public enum TriggerType
    {
        OnStart,
        OnTriggerEnter,
        OnCollisionEnter,
        Manual
    }

    public enum TriggerAction
    {
        StartQuest,
        CompletePhase
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = hasTriggered ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
#endif
}
