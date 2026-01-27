using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public InventoryLogic Logic { get; private set; }

    public void Init()
    {
        Logic = new InventoryLogic();
    }

    // 외부(퀘스트, 충돌체)에서 호출할 메서드
    public void AcquireItem(string itemID)
    {
        // DB에 존재하는 아이템인지 먼저 확인
        var itemData = Managers.Data.ItemDB.GetItem(itemID);
        if (itemData == null)
        {
            Debug.LogWarning($"DB에 존재하지 않는 아이템 ID: {itemID}");
        }
        Logic.AddItem(itemID);
        Debug.Log($"아이템 획득 성공: {itemData.itemName}");
        PrintInventoryToConsole();
    }

    public void PrintInventoryToConsole()
    {
        Debug.Log("===== 플레이어 인벤토리 =====");

        if (Logic.GetAllItems().Count == 0)
        {
            Debug.Log(" (비어 있음)");
            return;
        }

        foreach (var item in Logic.GetAllItems())
        {
            Debug.Log($"아이템명: {item.Key}, 수량: {item.Value}");
        }
    }
}