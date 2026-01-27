using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers _instance;
    public static Managers Instance => _instance;

    public static DataManager Data => Instance._data;
    public static InventoryManager Inventory => Instance._inventory;

    private DataManager _data;
    private InventoryManager _inventory;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 같은 GameObject에 붙어 있는 컴포넌트 참조
        _data = GetComponent<DataManager>();
        _inventory = GetComponent<InventoryManager>();

        if (_data == null)
            Debug.LogError("Managers: DataManager 컴포넌트가 없습니다.");
        else
            _data.Init();

        if (_inventory == null)
            Debug.LogError("Managers: InventoryManager 컴포넌트가 없습니다.");
        else
            _inventory.Init();
    }
}
