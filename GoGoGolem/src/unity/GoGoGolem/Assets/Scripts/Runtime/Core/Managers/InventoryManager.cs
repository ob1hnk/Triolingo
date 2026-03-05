using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ItemDatabaseSO itemCatalogue;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    public InventoryLogic Logic { get; private set; }
    public ItemDatabaseSO ItemDB { get; private set; }

    public void Init()
    {
        if (itemCatalogue == null)
        {
            Debug.LogError("[InventoryManager] itemCatalogue이 Inspector에 연결되지 않았습니다.");
        }
        else
        {
            ItemDB = itemCatalogue;
            ItemDB.Initialize();
        }

        Logic = new InventoryLogic();
    }

    private void OnEnable()
    {
        if (requestAcquireItemEvent != null)
            requestAcquireItemEvent.Register(AcquireItem);
    }

    private void OnDisable()
    {
        if (requestAcquireItemEvent != null)
            requestAcquireItemEvent.Unregister(AcquireItem);
    }

    public void AcquireItem(string itemID)
    {
        var itemData = ItemDB.GetItem(itemID);
        if (itemData == null)
        {
            Debug.LogWarning($"DB에 존재하지 않는 아이템 ID: {itemID}");
            return;
        }
        Logic.AddItem(itemID);
    }

    public void UseItem(string itemID)
    {
        var zone = ItemUsableZone.Current;
        if (zone == null)
        {
            Debug.Log("[InventoryManager] 아이템을 사용할 수 있는 위치가 아닙니다.");
            return;
        }

        if (!zone.Accepts(itemID))
        {
            Debug.Log($"[InventoryManager] 이 위치에서는 '{itemID}'을 사용할 수 없습니다.");
            return;
        }

        if (!Logic.HasItem(itemID))
        {
            Debug.LogWarning($"[InventoryManager] 인벤토리에 '{itemID}'이 없습니다.");
            return;
        }

        Logic.RemoveItem(itemID);
        zone.SpawnItem();
    }
}
