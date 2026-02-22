using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabaseSO", menuName = "Inventory/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<ItemData> items = new List<ItemData>();

    private Dictionary<string, ItemData> itemDict = new Dictionary<string, ItemData>();
    private bool isInitialized = false;

    public void Initialize()
    {
        if (isInitialized) return;

        itemDict.Clear();

        foreach (var item in items)
        {
            if (item == null) continue;

            if (!itemDict.ContainsKey(item.itemID))
                itemDict.Add(item.itemID, item);
            else
                Debug.LogWarning($"[ItemDatabaseSO] 중복된 itemID: {item.itemID}");
        }

        isInitialized = true;
    }

    public ItemData GetItem(string itemID)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[ItemDatabaseSO] Initialize()를 먼저 호출해야 합니다.");
            Initialize();
        }

        return itemDict.TryGetValue(itemID, out var data) ? data : null;
    }

    private void OnEnable()
    {
        isInitialized = false;
    }
}
