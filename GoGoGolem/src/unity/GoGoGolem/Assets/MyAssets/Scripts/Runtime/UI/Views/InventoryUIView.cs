using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private InventorySlot slotPrefab;
    
    [Header("Grid Settings")]
    [SerializeField] private int columns = 4;
    [SerializeField] private int initialSlotCount = 8; // 기본 슬롯 개수
    
    private List<InventorySlot> slots = new();
    private Dictionary<int, string> indexToItemID = new();

    /* =====================
     * Unity Lifecycle
     * ===================== */

    private void Awake()
    {
        Debug.Log("<color=yellow>[InventoryUIView]</color> Awake - 슬롯 초기화 시작");
        InitializeSlots();
    }

    /* =====================
     * Initialization
     * ===================== */

    private void InitializeSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUIView] slotPrefab이 할당되지 않았습니다! Inspector에서 프리팹을 할당해주세요.");
            return;
        }

        if (slotContainer == null)
        {
            Debug.LogError("[InventoryUIView] slotContainer가 할당되지 않았습니다!");
            return;
        }

        // 기존 슬롯 모두 제거
        int childCount = slotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(slotContainer.GetChild(i).gameObject);
            else
                DestroyImmediate(slotContainer.GetChild(i).gameObject);
        }
        slots.Clear();

        // 기본 슬롯 생성 (항상 표시될 빈 슬롯)
        Debug.Log($"<color=yellow>[InventoryUIView]</color> {initialSlotCount}개 슬롯 생성 시작");
        
        for (int i = 0; i < initialSlotCount; i++)
        {
            InventorySlot newSlot = Instantiate(slotPrefab, slotContainer);
            newSlot.gameObject.name = $"Slot_{i}";
            newSlot.ShowAsEmpty(); // 빈 슬롯으로 표시
            slots.Add(newSlot);
        }

        Debug.Log($"<color=yellow>[InventoryUIView]</color> 슬롯 {slots.Count}개 생성 완료 ✓");
    }

    /* =====================
     * Public API
     * ===================== */

    public void Render(Dictionary<string, int> items)
    {
        Debug.Log($"<color=yellow>[InventoryUIView]</color> Render 시작 - 아이템 {items?.Count ?? 0}개");
        
        // 슬롯이 없으면 생성
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
            Debug.Log("<color=yellow>[InventoryUIView]</color> 렌더링할 아이템이 없습니다. 빈 슬롯만 표시합니다.");
            return;
        }
        
        Debug.Log($"<color=yellow>[InventoryUIView]</color> 현재 슬롯 수: {slots.Count}");

        // 필요하면 슬롯 추가 생성
        int requiredSlots = items.Count;
        if (requiredSlots > slots.Count)
        {
            int slotsToAdd = requiredSlots - slots.Count;
            Debug.Log($"<color=yellow>[InventoryUIView]</color> 슬롯 부족 - {slotsToAdd}개 추가 생성");
            
            for (int i = 0; i < slotsToAdd; i++)
            {
                InventorySlot newSlot = Instantiate(slotPrefab, slotContainer);
                newSlot.gameObject.name = $"Slot_{slots.Count}";
                newSlot.ShowAsEmpty();
                slots.Add(newSlot);
            }
        }

        // 아이템 렌더링
        int index = 0;
        foreach (var item in items)
        {
            if (index >= slots.Count)
            {
                Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 슬롯 부족. 총 {slots.Count}개 중 {items.Count}개 아이템");
                break;
            }

            // ItemData 가져오기
            ItemData itemData = Managers.Data?.ItemDB?.GetItem(item.Key);
            string displayName = itemData != null ? itemData.itemName : item.Key;
            
            Debug.Log($"<color=yellow>[InventoryUIView]</color> 슬롯 {index}에 할당: {displayName} (ID: {item.Key}) x{item.Value}");
            
            slots[index].SetItem(displayName, item.Value);
            indexToItemID[index] = item.Key;
            index++;
        }

        Debug.Log($"<color=yellow>[InventoryUIView]</color> 총 {index}개 아이템 렌더링 완료 ✓");
        Debug.Log($"<color=yellow>[InventoryUIView]</color> indexToItemID 크기: {indexToItemID.Count}");
    }

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
    }

    public int GetItemIndexUnderMouse()
    {
        return -1;
    }

    public void Scroll(float delta)
    {
        if (scrollRect == null) return;

        float scrollSensitivity = 0.1f;
        scrollRect.verticalNormalizedPosition += delta * scrollSensitivity;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
    }

    public void UseSelectedItem(int index)
    {
        Debug.Log($"<color=yellow>[InventoryUIView]</color> UseSelectedItem({index}) 호출");
        
        if (!IsValidIndex(index)) 
        {
            Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 유효하지 않은 인덱스: {index}");
            return;
        }
        
        if (!indexToItemID.TryGetValue(index, out string itemID))
        {
            Debug.LogWarning($"<color=orange>[InventoryUIView]</color> 인덱스 {index}에 아이템이 없습니다.");
            return;
        }
        
        ItemData itemData = Managers.Data?.ItemDB?.GetItem(itemID);
        if (itemData == null)
        {
            Debug.LogWarning($"<color=yellow>[InventoryUIView]</color> 아이템 데이터를 찾을 수 없습니다. ID: {itemID}");
            return;
        }
        
        PrintItemInfo(itemData);
    }

    public void UpdatePointer(Vector2 mousePos)
    {
    }

    public bool HasItemAtIndex(int index)
    {
        bool hasItem = indexToItemID.ContainsKey(index);
        Debug.Log($"<color=yellow>[InventoryUIView]</color> HasItemAtIndex({index}): {hasItem}");
        return hasItem;
    }

    /* =====================
     * Internal Methods
     * ===================== */

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

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
    }

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