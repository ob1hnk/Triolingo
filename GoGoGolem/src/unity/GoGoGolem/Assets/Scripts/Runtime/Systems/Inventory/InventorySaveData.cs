using System.Collections.Generic;

[System.Serializable]
public class InventorySaveData
{
    public List<ItemSaveEntry> items = new List<ItemSaveEntry>();
    public string lastSaveTime;
    public int saveVersion = 1;
}

[System.Serializable]
public class ItemSaveEntry
{
    public string itemID;
    public int count;
}
