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
    private bool _isVisible = false;

    public bool IsVisible => _isVisible;
    public event System.Action<bool> OnVisibilityChanged;

    private void Awake()
    {
        ValidateComponents();
        Hide();

        if (view != null)
        {
            view.OnSlotClicked += OnSlotClickedByMouse;
        }
    }

    private void Start()
    {
        // InputModeController/Managers는 DontDestroyOnLoad 싱글톤이라 Awake 타이밍에는
        // 아직 초기화되지 않았을 수 있어 Start에서 수행한다.
        InitializeInputHandlers();
        InitializeInventoryLogic();
    }

    private void Update()
    {
        if (!_isVisible) return;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryUseSelectedItem();
    }

    private void TryUseSelectedItem()
    {
        if (view == null || selectedIndex < 0 || !view.HasItemAtIndex(selectedIndex)) return;

        var zone = ItemUsableZone.Current;
        if (zone == null)
        {
            Debug.Log("[InventoryUIPresenter] 이 위치에서는 아이템을 사용할 수 없습니다.");
            return;
        }

        string itemID = view.GetItemIdAt(selectedIndex);
        if (string.IsNullOrEmpty(itemID)) return;

        if (!zone.TryPlace(itemID))
        {
            string expected = zone.NextExpectedItemID;
            Debug.Log(expected != null
                ? $"[InventoryUIPresenter] {itemID}은(는) 지금 배치할 수 없습니다. 다음 필요: {expected}"
                : $"[InventoryUIPresenter] 이 존은 이미 모든 배치가 완료되었습니다.");
            return;
        }

        inventoryLogic.RemoveItem(itemID, 1);
        view?.ClearFilter();
        GameStateManager.Instance.ChangeState(GameState.Gameplay);
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
        uiInput.Submit.performed += OnSubmit;
        uiInput.Cancel.performed += OnCancel;

        isInputInitialized = true;
    }

    private void CleanupInputHandlers()
    {
        if (!isInputInitialized) return;

        uiInput.Navigate.performed -= OnNavigate;
        uiInput.Point.performed -= OnPoint;
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

    public void Toggle()
    {
        var state = GameStateManager.Instance.CurrentState;
        if (state == GameState.Gameplay)
            GameStateManager.Instance.ChangeState(GameState.InventoryUI);
        else if (state == GameState.InventoryUI)
            GameStateManager.Instance.ChangeState(GameState.Gameplay);
    }

    public void Show()
    {
        if (canvasGroup == null) return;

        if (!isInputInitialized)
        {
            InitializeInputHandlers();
        }

        if (!isInventoryLogicInitialized)
        {
            InitializeInventoryLogic();
        }

        _isVisible = true;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        ResetSelection();
        Refresh();
        OnVisibilityChanged?.Invoke(true);
    }

    public void Hide()
    {
        if (canvasGroup == null) return;

        _isVisible = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        OnVisibilityChanged?.Invoke(false);
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

        selectedIndex = view.MoveSelection(selectedIndex, direction);
        ShowSelectedItemInfo();
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

        var allItems = inventoryLogic.GetAllItems();
        var assetDB = Managers.Inventory?.AssetDB;

        var items = new System.Collections.Generic.Dictionary<string, int>();
        var skills = new System.Collections.Generic.Dictionary<string, int>();

        foreach (var kv in allItems)
        {
            var data = assetDB?.GetAsset(kv.Key);
            if (data != null && data.type == InventoryAssetType.Skill)
                skills[kv.Key] = kv.Value;
            else
                items[kv.Key] = kv.Value;
        }

        view.RenderItems(items);
        view.RenderSkills(skills);

        var zone = ItemUsableZone.Current;
        if (zone != null && !zone.IsComplete)
            view.ApplyFilter(zone.NextExpectedItemID);
        else
            view.ClearFilter();
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
