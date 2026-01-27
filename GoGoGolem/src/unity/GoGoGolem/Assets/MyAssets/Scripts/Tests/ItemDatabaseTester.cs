using UnityEngine;

public class ItemDatabaseTester : MonoBehaviour
{
    private ItemDatabase itemDatabase;

    void Start()
    {
        itemDatabase = new ItemDatabase();
        Debug.Log("📌 LoadDatabase() called");

        TextAsset csvData = Resources.Load<TextAsset>("Data/Items");

        if (csvData == null)
        {
            Debug.LogError("❌ CSV Load Failed: Resources/Data/Items.csv not found");
            return;
        }

        Debug.Log("✅ CSV Loaded Successfully");
        Debug.Log($"📄 Raw CSV:\n{csvData.text}");

        itemDatabase.LoadDatabase();

        // 테스트
        DebugItem("ITEM-001");
        DebugItem("SKILL-001");
        DebugItem("RWD-001");
    }

    void DebugItem(string id)
    {
        ItemData item = itemDatabase.GetItem(id);

        if (item == null)
        {
            Debug.LogError($"❌ Item not found: {id}");
            return;
        }

        Debug.Log(
            $"✅ [{item.itemID}] {item.itemName}\n" +
            $"- Type: {item.type}\n" +
            $"- Phase: {item.phase}\n" +
            $"- Desc: {item.description}\n" +
            $"- Usage: {item.usage}"
        );
    }
}
