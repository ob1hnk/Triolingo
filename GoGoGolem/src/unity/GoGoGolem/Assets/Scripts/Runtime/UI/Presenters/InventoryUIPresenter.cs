using System.Collections;
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

    /* =====================
     * Unity Lifecycle
     * ===================== */

    private void Awake()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Awake 시작");
        ValidateComponents();
        InitializeInputHandlers();

        if (view != null)
        {
            view.OnSlotClicked += OnSlotClickedByMouse;
        }
    }

    private void Start()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Start 시작");
        InitializeInventoryLogic();
    }

    private void OnDestroy()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> OnDestroy");
        if (view != null)
        {
            view.OnSlotClicked -= OnSlotClickedByMouse;
        }
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
            Debug.LogError("[InventoryUIPresenter] view가 연결되지 않았습니다.");
        }
        else
        {
            Debug.Log("<color=magenta>[InventoryUIPresenter]</color> view 연결 확인 완료");
        }
        
        if (canvasGroup == null)
        {
            Debug.LogError("[InventoryUIPresenter] canvasGroup이 연결되지 않았습니다.");
        }
        else
        {
            Debug.Log("<color=magenta>[InventoryUIPresenter]</color> canvasGroup 연결 확인 완료");
        }
    }

    private void InitializeInventoryLogic()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> InitializeInventoryLogic 시작");
        
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
        
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> InventoryLogic 연결 완료 ✓");
        Debug.Log($"<color=magenta>[InventoryUIPresenter]</color> 현재 인벤토리 아이템 수: {inventoryLogic.GetAllItems().Count}");
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
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Input 핸들러 초기화 완료 ✓");
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

    /* =====================
     * Public API
     * ===================== */

    public void Show()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Show() 호출");
        
        if (canvasGroup == null) return;
        
        // InventoryLogic이 아직 초기화되지 않았다면 시도
        if (!isInventoryLogicInitialized)
        {
            Debug.LogWarning("<color=magenta>[InventoryUIPresenter]</color> InventoryLogic이 초기화되지 않아 재시도");
            InitializeInventoryLogic();
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        ResetSelection();
        Refresh();
        
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> 인벤토리 표시 완료");
    }

    public void Hide()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Hide() 호출");
        
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

        if (direction.sqrMagnitude < 0.1f) return;

        // 첫 선택
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
            view.SelectItem(selectedIndex);
            Debug.Log($"<color=green>[Input]</color> 첫 슬롯 선택: 인덱스 {selectedIndex}");
            
            // ⭐ 첫 선택 시에도 아이템 정보 표시
            ShowSelectedItemInfo();
            return;
        }

        // 방향키로 이동
        int previousIndex = selectedIndex;
        selectedIndex = view.MoveSelection(selectedIndex, direction);
        
        if (previousIndex != selectedIndex)
        {
            Debug.Log($"<color=green>[Input]</color> 슬롯 이동: {previousIndex} → {selectedIndex}");
            
            // ⭐ 슬롯 이동 시 자동으로 아이템 정보 표시
            ShowSelectedItemInfo();
        }
    }

    private void OnPoint(InputAction.CallbackContext context)
    {
        if (view == null) return;
        
        Vector2 mousePos = context.ReadValue<Vector2>();
        view.UpdatePointer(mousePos);
    }

    /// <summary>
/// 마우스로 슬롯 클릭 시 처리
/// </summary>
private void OnSlotClickedByMouse(int clickedIndex)
{
Debug.Log($"<color=green>[Input]</color> 마우스 클릭: 슬롯 {clickedIndex} 선택");

if (!IsValidIndex(clickedIndex))
{
    Debug.LogWarning($"<color=orange>[Input]</color> 유효하지 않은 슬롯 인덱스: {clickedIndex}");
    return;
}

// 슬롯 선택
selectedIndex = clickedIndex;
view.SelectItem(selectedIndex);

// 아이템 정보 표시
ShowSelectedItemInfo();
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
        
        Debug.Log($"<color=green>[Input]</color> E 키 입력 - selectedIndex: {selectedIndex}");
        
        if (selectedIndex < 0)
        {
            Debug.LogWarning("<color=orange>[Input]</color> 아이템을 먼저 선택해주세요.");
            return;
        }

        // E키는 이제 다른 용도로 사용 가능 (예: 아이템 사용/장착)
        // 여기서는 일단 정보 표시만 유지
        ShowSelectedItemInfo();
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        Debug.Log("<color=yellow>[Q/ESC 키]</color> 인벤토리 닫기");
        GameStateManager.Instance.ChangeState(GameState.Gameplay);
    }

    /* =====================
     * Internal Methods
     * ===================== */

    /// <summary>
    /// 선택된 슬롯의 아이템 정보를 표시
    /// </summary>
    private void ShowSelectedItemInfo()
    {
        if (selectedIndex < 0 || view == null)
        {
            return;
        }

        bool hasItem = view.HasItemAtIndex(selectedIndex);
        
        if (!hasItem)
        {
            Debug.Log($"<color=gray>[Info]</color> 슬롯 {selectedIndex}는 비어있습니다.");
            return;
        }

        Debug.Log($"<color=cyan>[슬롯 선택]</color> 슬롯 {selectedIndex} 아이템 정보 표시");
        view.ShowItemInfo(selectedIndex);
    }

    private void Refresh()
    {
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Refresh() 호출");
        
        if (!isInventoryLogicInitialized)
        {
            Debug.LogWarning("<color=magenta>[InventoryUIPresenter]</color> Refresh 실패 - InventoryLogic 초기화 안됨");
            return;
        }
        
        if (inventoryLogic == null)
        {
            Debug.LogWarning("<color=magenta>[InventoryUIPresenter]</color> Refresh 실패 - inventoryLogic null");
            return;
        }
        
        if (view == null)
        {
            Debug.LogWarning("<color=magenta>[InventoryUIPresenter]</color> Refresh 실패 - view null");
            return;
        }
        
        var items = inventoryLogic.GetAllItems();
        Debug.Log($"<color=magenta>[InventoryUIPresenter]</color> View에 {items.Count}개 아이템 렌더링 시작");
        
        view.Render(items);
        
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> Refresh 완료 ✓");
    }

    private void ResetSelection()
    {
        selectedIndex = -1;
        view?.ClearSelection();
        Debug.Log("<color=magenta>[InventoryUIPresenter]</color> 선택 초기화");
    }


    private bool IsValidIndex(int index)
{
return view != null && index >= 0;
}
}
