using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : MonoBehaviour
{
    /* =====================
     * Serialized Fields
     * ===================== */
    
    [Header("Slot Settings")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private InventorySlot slotPrefab;
    
    [Header("Grid Settings")]
    [SerializeField] private int columns = 4;
    [SerializeField] private int initialSlotCount = 8;
    
    [Header("Item Info Panel")]
    [SerializeField] private ItemInfoPanel itemInfoPanel;
    
    /* =====================
     * Private Fields
     * ===================== */
    
    private List<InventorySlot> slots = new();
    private Dictionary<int, string> indexToItemID = new();
    
    /* =====================
     * Events
     * ===================== */
    
    // 슬롯 클릭 이벤트 (Presenter가 구독)
    public System.Action<int> OnSlotClicked;

    /* =====================
     * Unity Lifecycle
     * ===================== */

    private void Start()
    {
        Debug.Log("<color=yellow>[InventoryUIView]</color> Start - 슬롯 초기화 시작");
        
        if (Application.isPlaying)
        {
            InitializeSlots();
        }
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        if (!Application.isPlaying && slotContainer != null)
        {
            CleanupEditorSlots();
        }
    }

    private void CleanupEditorSlots()
    {
        int childCount = slotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
            if (child.gameObject.hideFlags == HideFlags.DontSave)
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
#endif

    /* =====================
     * Initialization
     * ===================== */

    private void InitializeSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUIView] slotPrefab이 할당되지 않았습니다!");
            return;
        }

        if (slotContainer == null)
        {
            Debug.LogError("[InventoryUIView] slotContainer가 할당되지 않았습니다!");
            return;
        }

        ClearAllSlotObjects();

        Debug.Log($"<color=yellow>[InventoryUIView]</color> {initialSlotCount}개 슬롯 생성 시작");
        
        for (int i = 0; i < initialSlotCount; i++)
        {
            CreateSlot(i);
        }

        Debug.Log($"<color=yellow>[InventoryUIView]</color> 슬롯 {slots.Count}개 생성 완료 ✓");
    }

    private void CreateSlot(int index)
    {
        InventorySlot newSlot = Instantiate(slotPrefab, slotContainer);
        newSlot.gameObject.name = $"Slot_{index}";
        newSlot.gameObject.hideFlags = HideFlags.DontSaveInEditor;
        
        // 슬롯 인덱스 설정
        newSlot.SetIndex(index);
        
        // 클릭 이벤트 구독
        newSlot.OnSlotClicked += HandleSlotClick;
        
        newSlot.ShowAsEmpty();
        slots.Add(newSlot);
    }

    private void ClearAllSlotObjects()
    {
        // 기존 슬롯 이벤트 구독 해제
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.OnSlotClicked -= HandleSlotClick;
            }
        }

        int childCount = slotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
            
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
        slots.Clear();
    }

    /* =====================
     * Public API - Rendering
     * ===================== */

    public void Render(Dictionary<string, int> items)
    {
        Debug.Log($"<color=yellow>[InventoryUIView]</color> Render 시작 - 아이템 {items?.Count ?? 0}개");
        
        if (slots.Count == 0)
        {
            Debug.LogWarning("<color=yellow>[InventoryUIView]</color> 슬롯이 없어서 재생성 시도");
            InitializeSlots();
        }
        
        // 매핑 초기화
        indexToItemID.Clear();
        
        // 모든 슬롯을 빈 상태로 초기화
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].ShowAsEmpty();
        }
        
        if (items == null || items.Count == 0)
        {
            Debug.Log("<color=yellow>[InventoryUIView]</color> 렌더링할 아이템이 없습니다.");
            return;
        }

        // 필요하면 슬롯 추가 생성
        int requiredSlots = items.Count;
        if (requiredSlots > slots.Count)
        {
            int slotsToAdd = requiredSlots - slots.Count;
            Debug.Log($"<color=yellow>[InventoryUIView]</color> 슬롯 부족 - {slotsToAdd}개 추가 생성");
            
            for (int i = 0; i < slotsToAdd; i++)
            {
                CreateSlot(slots.Count);
            }
        }

        // 아이템 렌더링
        int index = 0;
        foreach (var item in items)
        {
            if (index >= slots.Count)
            {
                Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 슬롯 부족");
                break;
            }

            ItemData itemData = Managers.Data?.ItemDB?.GetItem(item.Key);
            string displayName = itemData != null ? itemData.itemName : item.Key;
            
            slots[index].SetItem(displayName, item.Value);
            indexToItemID[index] = item.Key;
            index++;
        }

        Debug.Log($"<color=yellow>[InventoryUIView]</color> 총 {index}개 아이템 렌더링 완료 ✓");
    }

    /* =====================
     * Public API - Selection
     * ===================== */

    public void SelectItem(int index)
    {
        ClearSelection();

        if (IsValidIndex(index))
        {
            slots[index].SetSelected(true);
            ScrollToItem(index);
        }
    }

    public int MoveSelection(int currentIndex, Vector2 direction)
    {
        int nextIndex = CalculateNextIndex(currentIndex, direction);
        SelectItem(nextIndex);
        return nextIndex;
    }

    public void ClearSelection()
    {
        foreach (var slot in slots)
        {
            slot.SetSelected(false);
        }

        // 선택 해제 시 정보 패널도 비우기
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowEmpty();
        }
    }

    /* =====================
     * Public API - Item Info
     * ===================== */

    public void ShowItemInfo(int index)
    {
        Debug.Log($"<color=yellow>[InventoryUIView]</color> ShowItemInfo({index}) 호출");
        
        if (!IsValidIndex(index)) 
        {
            Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 유효하지 않은 인덱스: {index}");
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }
        
        if (!indexToItemID.TryGetValue(index, out string itemID))
        {
            Debug.LogWarning($"<color=orange>[InventoryUIView]</color> 인덱스 {index}에 아이템이 없습니다.");
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }
        
        ItemData itemData = Managers.Data?.ItemDB?.GetItem(itemID);
        if (itemData == null)
        {
            Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 아이템 데이터를 찾을 수 없습니다. ID: {itemID}");
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }

        // UI 패널에 표시
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowItemInfo(itemData);
        }
        
        // 콘솔에도 출력 (디버깅용)
        PrintItemInfo(itemData);
    }

    public bool HasItemAtIndex(int index)
    {
        return indexToItemID.ContainsKey(index);
    }

    /* =====================
     * Public API - Scrolling
     * ===================== */

    public void Scroll(float delta)
    {
        if (scrollRect == null) return;

        float scrollSensitivity = 0.1f;
        scrollRect.verticalNormalizedPosition += delta * scrollSensitivity;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
    }

    /* =====================
     * Public API - Mouse
     * ===================== */

    public void UpdatePointer(Vector2 mousePos)
    {
        // 필요시 마우스 호버 효과 구현
    }

    /* =====================
     * Event Handlers
     * ===================== */

    /// <summary>
    /// 슬롯 클릭 처리
    /// </summary>
    private void HandleSlotClick(int slotIndex)
    {
        Debug.Log($"<color=yellow>[InventoryUIView]</color> HandleSlotClick - 슬롯 {slotIndex} 클릭됨");
        
        // Presenter에게 전달
        OnSlotClicked?.Invoke(slotIndex);
    }

    /* =====================
     * Internal Helpers - Validation
     * ===================== */

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    /* =====================
     * Internal Helpers - Navigation
     * ===================== */

    private int CalculateNextIndex(int currentIndex, Vector2 direction)
    {
        int row = currentIndex / columns;
        int col = currentIndex % columns;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            col += direction.x > 0 ? 1 : -1;
        }
        else
        {
            row += direction.y > 0 ? -1 : 1;
        }

        col = Mathf.Clamp(col, 0, columns - 1);
        
        int maxRow = (slots.Count - 1) / columns;
        row = Mathf.Clamp(row, 0, maxRow);

        int nextIndex = row * columns + col;

        return IsValidIndex(nextIndex) ? nextIndex : currentIndex;
    }

    private void ScrollToItem(int index)
    {
        if (scrollRect == null || slotContainer == null) return;
        
        // TODO: 선택된 아이템이 보이도록 스크롤 위치 조정
    }

    /* =====================
     * Internal Helpers - Debug
     * ===================== */

    private void PrintItemInfo(ItemData itemData)
    {
        Debug.Log("====================================");
        Debug.Log($"<color=cyan>아이템 정보</color>");
        Debug.Log("====================================");
        Debug.Log($"<color=yellow>ID:</color> {itemData.itemID}");
        Debug.Log($"<color=yellow>이름:</color> {itemData.itemName}");
        Debug.Log($"<color=yellow>타입:</color> {itemData.type}");
        Debug.Log($"<color=yellow>단계:</color> {itemData.phase}");
        Debug.Log($"<color=yellow>설명:</color> {itemData.description}");
        Debug.Log($"<color=yellow>용도:</color> {itemData.usage}");
        Debug.Log("====================================");
    }
}