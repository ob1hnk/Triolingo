using UnityEngine;

/// <summary>
/// 골렘 음성 대화 상호작용 컴포넌트
/// PlayerInteraction의 E키 감지 시스템과 연동됨
/// requiredQuestId 퀘스트 완료 후 상호작용 활성화
/// </summary>
public class GolemInteractable : MonoBehaviour, IInteractable
{
    [Header("Quest Gate")]
    [Tooltip("완료되어야 상호작용 가능한 퀘스트 ID. 비워두면 항상 활성화 (테스트용)")]
    [SerializeField] private string requiredQuestId = "MQ-02";

    [Header("Event Channels")]
    [SerializeField] private GameEvent requestEnterDialogueEvent;

    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    private bool IsQuestGatePassed()
    {
        if (string.IsNullOrEmpty(requiredQuestId)) return true;
        if (Managers.Quest == null) return true;
        return Managers.Quest.IsQuestCompleted(requiredQuestId);
    }

    public InteractionType InteractionType => InteractionType.TalkGolem;
    public bool CanInteract => true;

    public string GetActionLabel() => IsQuestGatePassed() ? (promptData != null ? promptData.ActionLabel : "대화하기") : string.Empty;
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);

    public void Interact()
    {
        if (!IsQuestGatePassed()) return;

        if (requestEnterDialogueEvent != null)
            requestEnterDialogueEvent.Raise();
        else
            Debug.LogError($"[GolemInteractable] requestEnterDialogueEvent가 null입니다!");
    }
}