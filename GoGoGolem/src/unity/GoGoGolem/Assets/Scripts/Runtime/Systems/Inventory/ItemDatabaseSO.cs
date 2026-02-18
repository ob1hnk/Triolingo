using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabaseSO", menuName = "GoGoGolem/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject
{
    public List<ItemData> items = new List<ItemData>();
}
