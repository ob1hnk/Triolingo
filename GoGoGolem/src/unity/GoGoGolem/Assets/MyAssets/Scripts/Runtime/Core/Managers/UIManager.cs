using UnityEngine;
using MyAssets.UI.Presenters;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private InventoryUIPresenter inventoryPresenter;

    private void Awake()
    {
        Instance = this;
    }

    public InventoryUIPresenter Inventory => inventoryPresenter;
}
