using UnityEngine;

public enum ItemType { Item, Skill, Reward }

[CreateAssetMenu(fileName = "NewItem", menuName = "GoGoGolem/Item")]
public class ItemData : ScriptableObject
{
    public string itemID;
    public string itemName;
    public ItemType type;
    public string phase;
    [TextArea(2, 4)]
    public string description;
    [TextArea(2, 4)]
    public string usage;
    public Sprite icon;
}
