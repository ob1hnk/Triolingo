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

    [Header("References")]
    [SerializeField] private GolemDialogueSceneController dialogueController;

    private bool IsQuestGatePassed()
    {
        if (string.IsNullOrEmpty(requiredQuestId)) return true;       // 비워두면 항상 통과
        if (Managers.Quest == null) return true;                      // QuestManager 없으면 통과 (테스트용)
        return Managers.Quest.IsQuestCompleted(requiredQuestId);
    }

    public string GetInteractText()
    {
        return IsQuestGatePassed() ? "대화하기" : string.Empty;
    }

    public void Interact()
    {
        if (!IsQuestGatePassed()) return;
        dialogueController.EnterDialogueMode();
    }
}
