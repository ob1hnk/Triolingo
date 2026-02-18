public enum ItemType { Item, Skill, Reward }

[System.Serializable]
public class ItemData {
    public string itemID;
    public string itemName;
    public ItemType type;
    public string phase;
    public string description; 
    public string usage;
}