using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    public InventoryLogic Logic { get; private set; }

    public void Init()
    {
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
        var itemData = Managers.Data.ItemDB.GetItem(itemID);
        if (itemData == null)
        {
            Debug.LogWarning($"DB에 존재하지 않는 아이템 ID: {itemID}");
            return;
        }
        Logic.AddItem(itemID);
    }
}
