using UnityEngine;

public enum InventoryAssetType { Item, Skill, Reward }

[CreateAssetMenu(fileName = "NewInventoryAsset", menuName = "Inventory/Inventory Asset")]
public class InventoryAsset : ScriptableObject
{
    public string itemID;
    public string itemName;
    public InventoryAssetType type;
    public string phase;
    [TextArea(2, 4)]
    public string description;
    [TextArea(2, 4)]
    public string usage;
    public Sprite icon;
}
