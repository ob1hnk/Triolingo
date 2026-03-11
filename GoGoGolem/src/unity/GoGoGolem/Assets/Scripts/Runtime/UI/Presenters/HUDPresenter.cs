using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 버튼 Presenter.
/// 퀘스트 로그 버튼(Tab 동작)과 인벤토리 버튼(Q 동작)을 처리한다.
/// 각 패널의 열림/닫힘 상태에 따라 버튼 아이콘을 교체한다.
/// </summary>
public class HUDPresenter : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button questLogButton;
    [SerializeField] private Button inventoryButton;

    [Header("Button Icons")]
    [SerializeField] private Image questButtonIcon;
    [SerializeField] private Sprite questIconDefault;
    [SerializeField] private Sprite questIconActive;

    [SerializeField] private Image inventoryButtonIcon;
    [SerializeField] private Sprite inventoryIconDefault;
    [SerializeField] private Sprite inventoryIconActive;

    [Header("References")]
    [SerializeField] private QuestUIPresenter questUIPresenter;
    [SerializeField] private InventoryUIPresenter inventoryUIPresenter;

    private void Start()
    {
        if (questLogButton != null) questLogButton.onClick.AddListener(OnQuestLogButtonClicked);
        if (inventoryButton != null) inventoryButton.onClick.AddListener(OnInventoryButtonClicked);
    }

    private void OnDestroy()
    {
        if (questLogButton != null) questLogButton.onClick.RemoveListener(OnQuestLogButtonClicked);
        if (inventoryButton != null) inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);
    }

    private void OnQuestLogButtonClicked()
    {
        if (questUIPresenter == null) return;
        questUIPresenter.Toggle();
        UpdateQuestIcon();
    }

    private void OnInventoryButtonClicked()
    {
        if (inventoryUIPresenter == null) return;
        inventoryUIPresenter.Toggle();
        UpdateInventoryIcon();
    }

    private void UpdateQuestIcon()
    {
        if (questButtonIcon == null) return;
        questButtonIcon.sprite = questUIPresenter.IsVisible ? questIconActive : questIconDefault;
    }

    private void UpdateInventoryIcon()
    {
        if (inventoryButtonIcon == null) return;
        inventoryButtonIcon.sprite = inventoryUIPresenter.IsVisible ? inventoryIconActive : inventoryIconDefault;
    }
}