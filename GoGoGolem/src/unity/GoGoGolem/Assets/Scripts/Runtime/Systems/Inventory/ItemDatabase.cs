using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase
{
    private Dictionary<string, ItemData> itemDict = new Dictionary<string, ItemData>();

    public void LoadDatabase(ItemDatabaseSO catalogue)
    {
        itemDict.Clear();

        if (catalogue == null)
        {
            Debug.LogError("[ItemDatabase] ItemDatabaseSO가 할당되지 않았습니다.");
            return;
        }

        foreach (var item in catalogue.items)
        {
            if (item == null) continue;

            if (!itemDict.ContainsKey(item.itemID))
                itemDict.Add(item.itemID, item);
            else
                Debug.LogWarning($"[ItemDatabase] 중복된 itemID: {item.itemID}");
        }

    }

    public ItemData GetItem(string itemID)
    {
        return itemDict.TryGetValue(itemID, out var data) ? data : null;
    }
}
