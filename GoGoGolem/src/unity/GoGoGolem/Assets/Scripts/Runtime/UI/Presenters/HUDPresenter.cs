using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 버튼 Presenter.
/// 퀘스트 로그 버튼(Tab 동작)과 인벤토리 버튼(Q 동작)을 처리한다.
/// </summary>
public class HUDPresenter : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button questLogButton;
    [SerializeField] private Button inventoryButton;

    [Header("References")]
    [SerializeField] private QuestUIPresenter questUIPresenter;
    [SerializeField] private InventoryUIPresenter inventoryUIPresenter;

    private void Start()
    {
        questLogButton?.onClick.AddListener(OnQuestLogButtonClicked);
        inventoryButton?.onClick.AddListener(OnInventoryButtonClicked);
    }

    private void OnDestroy()
    {
        questLogButton?.onClick.RemoveListener(OnQuestLogButtonClicked);
        inventoryButton?.onClick.RemoveListener(OnInventoryButtonClicked);
    }

    private void OnQuestLogButtonClicked()
    {
        questUIPresenter?.Toggle();
    }

    private void OnInventoryButtonClicked()
    {
        inventoryUIPresenter?.Toggle();
    }
}