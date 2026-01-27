using UnityEngine;

public class DataManager : MonoBehaviour
{
    public ItemDatabase ItemDB { get; private set; }

    public void Init()
    {
        ItemDB = new ItemDatabase();
        ItemDB.LoadDatabase();
    }
}