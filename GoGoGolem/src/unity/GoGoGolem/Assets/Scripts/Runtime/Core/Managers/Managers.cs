// 6. Managers.cs (개선)
using UnityEngine;

public class Managers : MonoBehaviour
{
    private static Managers _instance;
    public static Managers Instance => _instance;

    public static DataManager Data => Instance?._data;
    public static InventoryManager Inventory => Instance?._inventory;
    public static UIManager UI => Instance?._ui;

    private DataManager _data;
    private InventoryManager _inventory;
    private UIManager _ui;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();
    }

    private void InitializeManagers()
    {
        // DataManager 초기화 (가장 먼저)
        _data = GetComponent<DataManager>();
        if (_data == null)
        {
            Debug.LogError("Managers: DataManager 컴포넌트가 없습니다.");
        }
        else
        {
            _data.Init();
        }

        // InventoryManager 초기화 (DataManager 이후)
        _inventory = GetComponent<InventoryManager>();
        if (_inventory == null)
        {
            Debug.LogError("Managers: InventoryManager 컴포넌트가 없습니다.");
        }
        else
        {
            _inventory.Init();
        }

        // UIManager 초기화
        _ui = GetComponent<UIManager>();
        if (_ui == null)
        {
            Debug.LogError("Managers: UIManager 컴포넌트가 없습니다.");
        }
    }
}
