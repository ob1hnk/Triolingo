using UnityEngine;
using UnityEngine.UI;

public class HUDPresenter : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button questLogButton;
    [SerializeField] private Button inventoryButton;

    [Header("Quest Button Icons")]
    [SerializeField] private GameObject questIconClosed;
    [SerializeField] private GameObject questIconOpen;

    [Header("Inventory Button Icons")]
    [SerializeField] private GameObject inventoryIconClosed;
    [SerializeField] private GameObject inventoryIconOpen;

    [Header("References")]
    [SerializeField] private QuestUIPresenter questUIPresenter;
    [SerializeField] private InventoryUIPresenter inventoryUIPresenter;

    private void Start()
    {
        if (questLogButton != null) questLogButton.onClick.AddListener(OnQuestLogButtonClicked);
        if (inventoryButton != null) inventoryButton.onClick.AddListener(OnInventoryButtonClicked);

        if (questUIPresenter != null) questUIPresenter.OnVisibilityChanged += SetQuestIcon;
        if (inventoryUIPresenter != null) inventoryUIPresenter.OnVisibilityChanged += SetInventoryIcon;

        SetQuestIcon(false);
        SetInventoryIcon(false);
    }

    private void OnDestroy()
    {
        if (questLogButton != null) questLogButton.onClick.RemoveListener(OnQuestLogButtonClicked);
        if (inventoryButton != null) inventoryButton.onClick.RemoveListener(OnInventoryButtonClicked);

        if (questUIPresenter != null) questUIPresenter.OnVisibilityChanged -= SetQuestIcon;
        if (inventoryUIPresenter != null) inventoryUIPresenter.OnVisibilityChanged -= SetInventoryIcon;
    }

    private void OnQuestLogButtonClicked()
    {
        if (questUIPresenter == null) return;
        if (inventoryUIPresenter != null && inventoryUIPresenter.IsVisible)
            inventoryUIPresenter.Toggle();
        questUIPresenter.Toggle();
    }

    private void OnInventoryButtonClicked()
    {
        if (inventoryUIPresenter == null) return;
        if (questUIPresenter != null && questUIPresenter.IsVisible)
            questUIPresenter.Hide();
        inventoryUIPresenter.Toggle();
    }

    private void SetQuestIcon(bool isOpen)
    {
        if (questIconClosed != null) questIconClosed.SetActive(!isOpen);
        if (questIconOpen != null) questIconOpen.SetActive(isOpen);
    }

    private void SetInventoryIcon(bool isOpen)
    {
        if (inventoryIconClosed != null) inventoryIconClosed.SetActive(!isOpen);
        if (inventoryIconOpen != null) inventoryIconOpen.SetActive(isOpen);
    }
}