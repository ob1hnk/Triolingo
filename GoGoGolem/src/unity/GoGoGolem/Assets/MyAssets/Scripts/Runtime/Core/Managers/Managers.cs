using UnityEngine;

    /// <summary>
    /// 게임의 주요 매니저들을 중앙에서 관리하는 싱글톤 매니저 클래스
    /// </summary>
    public class Managers : MonoBehaviour
    {
        private static Managers _instance;
        public static Managers Instance => _instance;

        public static DataManager Data => Instance?._data;
        public static InventoryManager Inventory => Instance?._inventory;
        public static UIManager UI => Instance?._ui;
        public static QuestManager Quest => Instance?._quest;

        private DataManager _data;
        private InventoryManager _inventory;
        private UIManager _ui;
        private QuestManager _quest;

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

            // QuestManager 초기화
            _quest = GetComponent<QuestManager>();
            if (_quest == null)
            {
                Debug.LogError("Managers: QuestManager 컴포넌트가 없습니다.");
            }
            // QuestManager는 자체 Awake에서 Initialize를 호출하므로 별도 Init 불필요
        }
    }
