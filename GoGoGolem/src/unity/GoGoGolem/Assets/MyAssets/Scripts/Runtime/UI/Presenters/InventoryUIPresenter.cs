// 4. InventoryUIPresenter.cs (리팩토링)
using MyAssets.FinalCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

// InventoryUIPresenter.cs 수정

namespace MyAssets.UI.Presenters
{
    public class InventoryUIPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private InventoryUIView view;
        
        private GameInputActions.UIActions uiInput;
        private InventoryLogic inventoryLogic;
        private int selectedIndex = -1;
        
        // 입력 핸들러가 초기화되었는지 추적
        private bool isInputInitialized = false;

        /* =====================
         * Unity Lifecycle
         * ===================== */

        private void Awake()
        {
            ValidateComponents();
            InitializeInventoryLogic();
            InitializeInputHandlers();
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
            inventoryLogic = Managers.Inventory?.Logic;
            
            if (inventoryLogic == null)
            {
                Debug.LogError("InventoryUIPresenter: InventoryLogic을 찾을 수 없습니다.");
                return;
            }
            
            inventoryLogic.OnInventoryChanged += Refresh;
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
            // struct는 null 체크 불가, 초기화 여부로 확인
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
            if (inventoryLogic != null)
            {
                inventoryLogic.OnInventoryChanged -= Refresh;
            }
        }

        /* =====================
         * Public API
         * ===================== */

        public void Show()
        {
            if (canvasGroup == null) return;
            
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

            // 첫 선택
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                view.SelectItem(selectedIndex);
                return;
            }

            // 방향키로 이동
            selectedIndex = view.MoveSelection(selectedIndex, direction);
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
            if (selectedIndex < 0) return;

            view.UseSelectedItem(selectedIndex);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (!context.performed) return;

            // 상태 변경을 통해 인벤토리 닫기
            GameStateManager.Instance.ChangeState(GameState.Gameplay);
        }

        /* =====================
         * Internal Methods
         * ===================== */

        private void Refresh()
        {
            if (inventoryLogic == null || view == null)
            {
                Debug.LogWarning("InventoryUIPresenter: Refresh 실패 - inventoryLogic 또는 view가 null입니다.");
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