using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabaseSO", menuName = "Inventory/Item Database")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<ItemData> items = new List<ItemData>();
}
