using UnityEngine;

/// <summary>
/// 골렘 음성 대화 상호작용 컴포넌트
/// PlayerInteraction의 E키 감지 시스템과 연동됨
/// requiredPhaseID phase 완료 후 상호작용 활성화. 비워두면 항상 활성화 (테스트용)
/// </summary>
public class GolemInteractable : MonoBehaviour, IInteractable
{
    [Header("Quest Gate")]
    [Tooltip("완료되어야 상호작용 가능한 phase ID. 비워두면 항상 활성화 (테스트용)")]
    [SerializeField] private string requiredPhaseID;
    [SerializeField] private string requiredQuestID;
    [SerializeField] private string requiredObjectiveID;

    [Header("Event Channels")]
    [SerializeField] private GameEvent requestEnterDialogueEvent;

    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    private bool IsGatePassed()
    {
        if (string.IsNullOrEmpty(requiredPhaseID)) return true;
        if (Managers.Quest == null) return true;
        return Managers.Quest.IsPhaseCompleted(requiredQuestID, requiredObjectiveID, requiredPhaseID);
    }

    public InteractionType InteractionType => InteractionType.TalkGolem;
    public bool CanInteract => IsGatePassed();

    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "대화하기";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);

    public void Interact()
    {
        if (requestEnterDialogueEvent != null)
            requestEnterDialogueEvent.Raise();
        else
            Debug.LogError($"[GolemInteractable] requestEnterDialogueEvent가 null입니다!");
    }
}