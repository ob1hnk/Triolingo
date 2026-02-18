using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public InventoryLogic Logic { get; private set; }

    public void Init()
    {
        Logic = new InventoryLogic();
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
