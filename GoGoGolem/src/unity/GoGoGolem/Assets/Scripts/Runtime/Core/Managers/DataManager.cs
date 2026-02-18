using UnityEngine;

public class DataManager : MonoBehaviour
{
    [SerializeField] private ItemDatabaseSO itemCatalogue;

    public ItemDatabase ItemDB { get; private set; }

    public void Init()
    {
        ItemDB = new ItemDatabase();
        ItemDB.LoadDatabase(itemCatalogue);
    }
}
