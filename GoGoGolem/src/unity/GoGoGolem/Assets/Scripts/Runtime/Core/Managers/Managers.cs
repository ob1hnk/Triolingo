using UnityEngine;

    /// <summary>
    /// 게임의 주요 매니저들을 중앙에서 관리하는 싱글톤 매니저 클래스
    /// </summary>
    public class Managers : MonoBehaviour
    {
        private static Managers _instance;
        public static Managers Instance => _instance;

        public static InventoryManager Inventory => Instance?._inventory;
        public static UIManager UI => Instance?._ui;
        public static QuestManager Quest => Instance?._quest;
        public static DialogueManager Dialogue => Instance?._dialogue;

        private InventoryManager _inventory;
        private UIManager _ui;
        private QuestManager _quest;
        private DialogueManager _dialogue;

        /// <summary>
        /// 에디터에서 Domain Reload 없이 Play 모드 진입 시 static 초기화.
        /// Do not reload Domain or Scene 설정에서 _instance가 잔류하는 문제 방지.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            _instance = null;
        }

        private void Awake()
        {
            Debug.Log($"[Managers] Awake 호출됨. _instance: {_instance}");
            if (_instance != null)
            {
                Debug.Log("[Managers] 중복 인스턴스 → Destroy");
                Destroy(gameObject);
                return;
            }

            _instance = this;
             Debug.Log("[Managers] DontDestroyOnLoad 호출");
            DontDestroyOnLoad(gameObject);
            Debug.Log("[Managers] InitializeManagers 시작");
            InitializeManagers();
            Debug.Log("[Managers] 초기화 완료");
        }

        private void InitializeManagers()
        {
            // InventoryManager 초기화 (ItemDB 포함)
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

            // DialogueManager 초기화
            _dialogue = FindObjectOfType<DialogueManager>();
            if (_dialogue == null)
            {
                Debug.LogWarning("Managers: DialogueManager 컴포넌트가 없습니다.");
            }
        }
    }