using UnityEngine;

public class InventoryAssetDatabaseTester : MonoBehaviour
{
    void Start()
    {
        if (Managers.Inventory == null || Managers.Inventory.AssetDB == null)
        {
            Debug.LogError("InventoryAssetDatabaseTester: Managers.Inventory 또는 AssetDB가 초기화되지 않았습니다.");
            return;
        }

        DebugItem("ITEM-001");
        DebugItem("SKILL-001");
        DebugItem("RWD-001");
    }

    void DebugItem(string id)
    {
        InventoryAsset asset = Managers.Inventory.AssetDB.GetAsset(id);

        if (asset == null)
        {
            Debug.LogError($"Asset not found: {id}");
            return;
        }

        Debug.Log(
            $"[{asset.itemID}] {asset.itemName}\n" +
            $"- Type: {asset.type}\n" +
            $"- Phase: {asset.phase}\n" +
            $"- Desc: {asset.description}\n" +
            $"- Usage: {asset.usage}"
        );
    }
}
