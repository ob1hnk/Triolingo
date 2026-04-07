using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class InventorySaveSystem
{
    private readonly string saveFolderPath;
    private const string SaveFileName = "inventory_save.json";
    private string SaveFilePath => Path.Combine(saveFolderPath, SaveFileName);

    public InventorySaveSystem()
    {
        saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");
        if (!Directory.Exists(saveFolderPath))
            Directory.CreateDirectory(saveFolderPath);
    }

    public void Save(Dictionary<string, int> items)
    {
        try
        {
            var data = new InventorySaveData
            {
                lastSaveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            foreach (var kv in items)
                data.items.Add(new ItemSaveEntry { itemID = kv.Key, count = kv.Value });

            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[InventorySaveSystem] 저장 완료: {data.items.Count}개 아이템");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventorySaveSystem] 저장 실패: {e.Message}");
        }
    }

    public InventorySaveData Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.Log("[InventorySaveSystem] 저장 파일 없음 — 빈 인벤토리로 시작");
            return new InventorySaveData();
        }

        try
        {
            var data = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(SaveFilePath));
            Debug.Log($"[InventorySaveSystem] 로드 완료: {data.items.Count}개 아이템 (저장: {data.lastSaveTime})");
            return data;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InventorySaveSystem] 로드 실패: {e.Message}");
            return new InventorySaveData();
        }
    }

    public void Delete()
    {
        if (!File.Exists(SaveFilePath)) return;
        File.Delete(SaveFilePath);
        Debug.Log("[InventorySaveSystem] 저장 파일 삭제");
    }

    public bool HasSaveFile() => File.Exists(SaveFilePath);
}
