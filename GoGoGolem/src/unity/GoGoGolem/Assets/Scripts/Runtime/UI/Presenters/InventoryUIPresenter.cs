using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUIPresenter : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private InventoryUIView view;

    private GameInputActions.UIActions uiInput;
    private InventoryLogic inventoryLogic;
    private int selectedIndex = -1;

    private bool isInputInitialized = false;
    private bool isInventoryLogicInitialized = false;

    private void Awake()
    {
        ValidateComponents();
        InitializeInputHandlers();
        Hide();

        if (view != null)
        {
            view.OnSlotClicked += OnSlotClickedByMouse;
        }
    }

    private void Start()
    {
        InitializeInventoryLogic();
    }

    private void OnDestroy()
    {
        if (view != null)
        {
            view.OnSlotClicked -= OnSlotClickedByMouse;
        }
        CleanupInputHandlers();
        CleanupInventoryLogic();
    }

    private void ValidateComponents()
    {
        if (view == null)
            Debug.LogError("[InventoryUIPresenter] view가 연결되지 않았습니다.");

        if (canvasGroup == null)
            Debug.LogError("[InventoryUIPresenter] canvasGroup이 연결되지 않았습니다.");
    }

    private void InitializeInventoryLogic()
    {
        if (Managers.Instance == null)
        {
            Debug.LogError("[InventoryUIPresenter] Managers가 초기화되지 않았습니다.");
            return;
        }

        if (Managers.Inventory == null)
        {
            Debug.LogError("[InventoryUIPresenter] InventoryManager가 초기화되지 않았습니다.");
            return;
        }

        inventoryLogic = Managers.Inventory.Logic;

        if (inventoryLogic == null)
        {
            Debug.LogError("[InventoryUIPresenter] InventoryLogic을 찾을 수 없습니다.");
            return;
        }

        inventoryLogic.OnInventoryChanged += Refresh;
        isInventoryLogicInitialized = true;
    }

    private void InitializeInputHandlers()
    {
        if (InputModeController.Instance == null)
        {
            Debug.LogError("[InventoryUIPresenter] InputModeController를 찾을 수 없습니다.");
            return;
        }

        uiInput = InputModeController.Instance.GetUIActions();

        uiInput.Navigate.performed += OnNavigate;
        uiInput.Point.performed += OnPoint;
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

    public void Show()
    {
        if (canvasGroup == null) return;

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

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (!context.performed || view == null) return;

        Vector2 direction = context.ReadValue<Vector2>();
        if (direction.sqrMagnitude < 0.1f) return;

        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            view.SelectItem(selectedIndex);
            ShowSelectedItemInfo();
            return;
        }

        int previousIndex = selectedIndex;
        selectedIndex = view.MoveSelection(selectedIndex, direction);

        if (previousIndex != selectedIndex)
        {
            ShowSelectedItemInfo();
        }
    }

    private void OnPoint(InputAction.CallbackContext context)
    {
        if (view == null) return;

        Vector2 mousePos = context.ReadValue<Vector2>();
        view.UpdatePointer(mousePos);
    }

    private void OnSlotClickedByMouse(int clickedIndex)
    {
        if (!IsValidIndex(clickedIndex)) return;

        selectedIndex = clickedIndex;
        view.SelectItem(selectedIndex);
        ShowSelectedItemInfo();
    }

    private void OnScroll(InputAction.CallbackContext context)
    {
        if (view == null) return;

        Vector2 scrollValue = context.ReadValue<Vector2>();
        view.Scroll(scrollValue.y);
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (!context.performed || view == null) return;
        if (selectedIndex < 0) return;

        ShowSelectedItemInfo();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        GameStateManager.Instance.ChangeState(GameState.Gameplay);
    }

    private void ShowSelectedItemInfo()
    {
        if (selectedIndex < 0 || view == null) return;
        if (!view.HasItemAtIndex(selectedIndex)) return;

        view.ShowItemInfo(selectedIndex);
    }

    private void Refresh()
    {
        if (!isInventoryLogicInitialized || inventoryLogic == null || view == null) return;

        var items = inventoryLogic.GetAllItems();
        view.Render(items);
    }

    private void ResetSelection()
    {
        selectedIndex = -1;
        view?.ClearSelection();
    }

    private bool IsValidIndex(int index)
    {
        return view != null && index >= 0;
    }
}
