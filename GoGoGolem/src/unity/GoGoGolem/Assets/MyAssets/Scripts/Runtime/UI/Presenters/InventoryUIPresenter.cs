using MyAssets.FinalCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;


namespace MyAssets.UI.Presenters
{
        public class InventoryUIPresenter : MonoBehaviour, GameInputActions.IUIActions
        {
                [SerializeField] private InventoryUIView view;
                private GameInputActions inputActions;
                private int selectedIndex = -1;

                private InventoryLogic inventoryLogic;
                private void Awake()
                {
                        inputActions = new GameInputActions();
                }

                void Start()
                {
                        // InventoryLogic 연결
                        inventoryLogic = Managers.Inventory.Logic;

                        if (inventoryLogic == null)
                        {
                        Debug.LogError("InventoryUIPresenter: InventoryLogic을 찾을 수 없습니다.");
                        return;
                        }

                        // 이벤트 구독
                        inventoryLogic.OnInventoryChanged += Refresh;

                        // 최초 1회 렌더링
                        Refresh();
                }

                void OnDestroy()
                {
                        if (inventoryLogic != null)
                        inventoryLogic.OnInventoryChanged -= Refresh;
                }

                private void Refresh()
                {
                        view.Render(inventoryLogic.GetAllItems());
                }


                private void OnEnable()
                {
                        inputActions.UI.SetCallbacks(this);
                        inputActions.UI.Enable();
                }

                private void OnDisable()
                {
                        inputActions.UI.RemoveCallbacks(this);
                        inputActions.UI.Disable();
                }
                
                // WASD, Arrow
                public void OnNavigate(InputAction.CallbackContext context)
                {
                        if (!context.performed) return;

                        Vector2 dir = context.ReadValue<Vector2>();

                        if (selectedIndex < 0)
                        {
                                selectedIndex = 0;
                                view.SelectItem(selectedIndex);
                                return;
                        }

                        selectedIndex = view.MoveSelection(selectedIndex, dir);
                }
                // 마우스 위치 (필요하면 View에서 사용)
                public void OnPoint(InputAction.CallbackContext context)
                {
                        Vector2 mousePos = context.ReadValue<Vector2>();
                        view.UpdatePointer(mousePos);
                }

                // 마우스 클릭
                public void OnClick(InputAction.CallbackContext context)
                {
                        if (!context.performed) return;

                        int clickedIndex = view.GetItemIndexUnderMouse();
                        if (clickedIndex < 0) return;

                        selectedIndex = clickedIndex;
                        view.SelectItem(selectedIndex);
                }

                // 스크롤
                public void OnScroll(InputAction.CallbackContext context)
                {
                        float scroll = context.ReadValue<float>();
                        view.Scroll(scroll);
                }

                // E 키
                public void OnSubmit(InputAction.CallbackContext context)
                {
                        if (!context.performed) return;
                        if (selectedIndex < 0) return;

                        view.UseSelectedItem(selectedIndex);
                }

                // Q / Esc
                public void OnCancel(InputAction.CallbackContext context)
                {
                        if (!context.performed) return;

                        CloseInventory();
                }

                private void CloseInventory()
                {
                        // 인벤토리 닫기
                }

        }
}