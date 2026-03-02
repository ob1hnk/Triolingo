using UnityEngine;

/// <summary>
/// 범용 NPC 클래스. 대화 시작을 담당한다.
/// 퀘스트 액션이 필요한 경우 같은 GameObject에 NPCQuestHandler를 추가한다.
/// </summary>
public class NPC : MonoBehaviour, IInteractable
{
    [Header("NPC Info")]
    [SerializeField] private string npcName = "마을 주민";

    [Header("Dialogue")]
    [Tooltip("Yarn 대화 노드 이름 (예: DLG-001)")]
    [SerializeField] private string dialogueID;

    [Header("Options")]
    [Tooltip("한 번만 상호작용 가능")]
    [SerializeField] private bool onceOnly = false;

    [Tooltip("상호작용 후 대사")]
    [TextArea(2, 4)]
    [SerializeField] private string afterInteractionText = "";

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartDialogueEvent;

    private bool hasInteracted = false;

    public InteractionType InteractionType => InteractionType.Talk;

    public string GetInteractText()
    {
        if (onceOnly && hasInteracted) return "";
        return $"{npcName}와 대화하기 (E)";
    }

    public void Interact()
    {
        if (onceOnly && hasInteracted)
        {
            if (!string.IsNullOrEmpty(afterInteractionText))
                Debug.Log($"[NPC] {npcName}: {afterInteractionText}");
            else
                Debug.Log($"[NPC] {npcName}: 이미 대화했습니다.");
            return;
        }

        hasInteracted = true;

        if (!string.IsNullOrEmpty(dialogueID) && requestStartDialogueEvent != null)
        {
            requestStartDialogueEvent.Raise(dialogueID);
        }
        else if (string.IsNullOrEmpty(dialogueID))
        {
            Debug.LogWarning($"[NPC] {npcName}: 대화 ID가 설정되지 않았습니다.");
        }
        else if (requestStartDialogueEvent == null)
        {
            Debug.LogError($"[NPC] {npcName}: requestStartDialogueEvent가 null입니다!");
        }

        GetComponent<NPCQuestHandler>()?.Execute();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = hasInteracted ? Color.green : Color.blue;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.3f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 3f);
    }
#endif
}
