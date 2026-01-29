using MyAssets.FinalCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.UI.Presenters
{
    public class InventoryUIPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private InventoryUIView view;
        
        private GameInputActions.UIActions uiInput;
        private InventoryLogic inventoryLogic;
        private int selectedIndex = -1;
        
        private bool isInputInitialized = false;
        private bool isInventoryLogicInitialized = false;

        /* =====================
         * Unity Lifecycle
         * ===================== */

        private void Awake()
        {
            ValidateComponents();
            InitializeInputHandlers();
        }

        private void Start()
        {
            InitializeInventoryLogic();
        }

        private void OnDestroy()
        {
            CleanupInputHandlers();
            CleanupInventoryLogic();
        }

        /* =====================
         * Initialization
         * ===================== */

        private void ValidateComponents()
        {
            if (view == null)
            {
                Debug.LogError("InventoryUIPresenter: view가 연결되지 않았습니다.");
            }
            
            if (canvasGroup == null)
            {
                Debug.LogError("InventoryUIPresenter: canvasGroup이 연결되지 않았습니다.");
            }
        }

        private void InitializeInventoryLogic()
        {
            if (Managers.Instance == null)
            {
                Debug.LogError("InventoryUIPresenter: Managers가 초기화되지 않았습니다.");
                return;
            }

            if (Managers.Inventory == null)
            {
                Debug.LogError("InventoryUIPresenter: InventoryManager가 초기화되지 않았습니다.");
                return;
            }

            inventoryLogic = Managers.Inventory.Logic;
            
            if (inventoryLogic == null)
            {
                Debug.LogError("InventoryUIPresenter: InventoryLogic을 찾을 수 없습니다.");
                return;
            }
            
            inventoryLogic.OnInventoryChanged += Refresh;
            isInventoryLogicInitialized = true;
            Debug.Log("InventoryUIPresenter: InventoryLogic 연결 완료");
        }

        private void InitializeInputHandlers()
        {
            if (InputModeController.Instance == null)
            {
                Debug.LogError("InventoryUIPresenter: InputModeController를 찾을 수 없습니다.");
                return;
            }
            
            uiInput = InputModeController.Instance.GetUIActions();
            
            uiInput.Navigate.performed += OnNavigate;
            uiInput.Point.performed += OnPoint;
            uiInput.Click.performed += OnClick;
            uiInput.Scroll.performed += OnScroll;
            uiInput.Submit.performed += OnSubmit;
            uiInput.Cancel.performed += OnCancel;
            
            isInputInitialized = true;
        }

        private void CleanupInputHandlers()
        {
            if (!isInputInitialized) return;
            
            uiInput.Navigate.performed -= OnNavigate;
            uiInput.Point.performed -= OnPoint;
            uiInput.Click.performed -= OnClick;
            uiInput.Scroll.performed -= OnScroll;
            uiInput.Submit.performed -= OnSubmit;
            uiInput.Cancel.performed -= OnCancel;
            
            isInputInitialized = false;
        }

        private void CleanupInventoryLogic()
        {
            if (!isInventoryLogicInitialized) return;
            
            if (inventoryLogic != null)
            {
                inventoryLogic.OnInventoryChanged -= Refresh;
            }
            
            isInventoryLogicInitialized = false;
        }

        /* =====================
         * Public API
         * ===================== */

        public void Show()
        {
            if (canvasGroup == null) return;
            
            // InventoryLogic이 아직 초기화되지 않았다면 시도
            if (!isInventoryLogicInitialized)
            {
                InitializeInventoryLogic();
            }
            
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            ResetSelection();
            Refresh();
        }

        public void Hide()
        {
            if (canvasGroup == null) return;
            
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /* =====================
         * Input Handlers
         * ===================== */

        private void OnNavigate(InputAction.CallbackContext context)
        {
            if (!context.performed || view == null) return;

            Vector2 direction = context.ReadValue<Vector2>();

            // 방향 입력이 없으면 무시
            if (direction.sqrMagnitude < 0.1f) return;

            // 첫 선택
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                view.SelectItem(selectedIndex);
                Debug.Log($"<color=green>슬롯 선택:</color> 인덱스 {selectedIndex}");
                return;
            }

            // 방향키로 이동
            int previousIndex = selectedIndex;
            selectedIndex = view.MoveSelection(selectedIndex, direction);
            
            if (previousIndex != selectedIndex)
            {
                Debug.Log($"<color=green>슬롯 이동:</color> {previousIndex} → {selectedIndex}");
            }
        }

        private void OnPoint(InputAction.CallbackContext context)
        {
            if (view == null) return;
            
            Vector2 mousePos = context.ReadValue<Vector2>();
            view.UpdatePointer(mousePos);
        }

        private void OnClick(InputAction.CallbackContext context)
        {
            if (!context.performed || view == null) return;

            int clickedIndex = view.GetItemIndexUnderMouse();
            if (clickedIndex < 0) return;

            selectedIndex = clickedIndex;
            view.SelectItem(selectedIndex);
            Debug.Log($"<color=green>마우스 클릭:</color> 슬롯 {selectedIndex} 선택");
        }

        private void OnScroll(InputAction.CallbackContext context)
        {
            if (view == null) return;
            
            float scrollDelta = context.ReadValue<float>();
            view.Scroll(scrollDelta);
        }

        private void OnSubmit(InputAction.CallbackContext context)
        {
            if (!context.performed || view == null) return;
            if (selectedIndex < 0)
            {
                Debug.LogWarning("아이템을 먼저 선택해주세요.");
                return;
            }

            // 선택한 슬롯에 아이템이 있는지 확인
            if (!view.HasItemAtIndex(selectedIndex))
            {
                Debug.LogWarning($"슬롯 {selectedIndex}에 아이템이 없습니다.");
                return;
            }

            Debug.Log($"<color=cyan>[E 키 입력]</color> 슬롯 {selectedIndex} 아이템 정보 표시");
            view.UseSelectedItem(selectedIndex);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            Debug.Log("<color=yellow>[Q/ESC 키 입력]</color> 인벤토리 닫기");
            GameStateManager.Instance.ChangeState(GameState.Gameplay);
        }

        /* =====================
         * Internal Methods
         * ===================== */

        private void Refresh()
        {
            if (!isInventoryLogicInitialized || inventoryLogic == null || view == null)
            {
                Debug.LogWarning("InventoryUIPresenter: Refresh 실패 - 초기화되지 않았습니다.");
                return;
            }
            
            view.Render(inventoryLogic.GetAllItems());
        }

        private void ResetSelection()
        {
            selectedIndex = -1;
            view?.ClearSelection();
        }
    }
}