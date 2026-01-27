using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase
{
    private Dictionary<string, ItemData> itemDict = new Dictionary<string, ItemData>();

    public void LoadDatabase()
    {
        // Resources/Data/Items.csv 로드
        TextAsset csvData = Resources.Load<TextAsset>("Data/Items");
        if (csvData == null) return;

        string[] lines = csvData.text.Split('\n');

        // 첫 줄(헤더) 제외하고 반복
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = lines[i].Split(',');
            ItemData data = new ItemData
            {
                itemID = row[0],
                itemName = row[1],
                type = (ItemType)System.Enum.Parse(typeof(ItemType), row[2]),
                phase = row[3],
                description = row[4],
                usage = row[5]
            };
            
            if (!itemDict.ContainsKey(data.itemID))
                itemDict.Add(data.itemID, data);
        }
    }

    public ItemData GetItem(string itemID)
    {
        return itemDict.TryGetValue(itemID, out var data) ? data : null;
    }
}