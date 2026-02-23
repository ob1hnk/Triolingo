using UnityEngine;

public class ItemDatabaseTester : MonoBehaviour
{
    void Start()
    {
        if (Managers.Inventory == null || Managers.Inventory.ItemDB == null)
        {
            Debug.LogError("ItemDatabaseTester: Managers.Inventory 또는 ItemDB가 초기화되지 않았습니다.");
            return;
        }

        DebugItem("ITEM-001");
        DebugItem("SKILL-001");
        DebugItem("RWD-001");
    }

    void DebugItem(string id)
    {
        ItemData item = Managers.Inventory.ItemDB.GetItem(id);

        if (item == null)
        {
            Debug.LogError($"Item not found: {id}");
            return;
        }

        Debug.Log(
            $"[{item.itemID}] {item.itemName}\n" +
            $"- Type: {item.type}\n" +
            $"- Phase: {item.phase}\n" +
            $"- Desc: {item.description}\n" +
            $"- Usage: {item.usage}"
        );
    }
}
