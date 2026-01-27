using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers _instance;
    private static Managers Instance { get { Init(); return _instance; } }

    private DataManager _data = new DataManager();
    private InventoryManager _inventory = new InventoryManager();

    public static DataManager Data => Instance._data;
    public static InventoryManager Inventory => Instance._inventory;

    static void Init()
    {
        if (_instance == null)
        {
            GameObject go = GameObject.Find("@Managers");
            if (go == null)
            {
                go = new GameObject { name = "@Managers" };
                go.AddComponent<Managers>();
            }
            DontDestroyOnLoad(go);
            _instance = go.GetComponent<Managers>();

            // 초기화
            Debug.Log("현위치: Managers.cs data.Init()");
            _instance._data.Init();
            Debug.Log("현위치: Managers.cs inventory.Init()");
            _instance._inventory.Init();
        }
    }
}