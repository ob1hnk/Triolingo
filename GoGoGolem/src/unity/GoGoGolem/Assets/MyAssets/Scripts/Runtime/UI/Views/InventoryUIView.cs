// 5. InventoryUIView.cs (개선)
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
    
    private List<InventorySlot> slots = new();

    /* =====================
     * Public API
     * ===================== */

    public void Render(Dictionary<string, int> items)
    {
        // 모든 슬롯 초기화
        ClearAllSlots();
        
        // 아이템 렌더링
        int index = 0;
        foreach (var item in items)
        {
            if (index >= slots.Count)
            {
                Debug.LogWarning($"InventoryUIView: 슬롯 부족. 총 {slots.Count}개 중 {items.Count}개 아이템");
                break;
            }

            slots[index].SetItem(item.Key, item.Value);
            index++;
        }
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
        // TODO: EventSystem을 통한 마우스 감지 구현
        return -1;
    }

    public void Scroll(float delta)
    {
        if (scrollRect == null) return;

        // 스크롤 민감도 조정
        float scrollSensitivity = 0.1f;
        scrollRect.verticalNormalizedPosition += delta * scrollSensitivity;
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
    }

    public void UseSelectedItem(int index)
    {
        if (!IsValidIndex(index)) return;
        
        // TODO: 실제 아이템 사용 로직 구현
        Debug.Log($"InventoryUIView: 아이템 사용 시도 (인덱스: {index})");
    }

    public void UpdatePointer(Vector2 mousePos)
    {
        // TODO: 마우스 호버 효과 구현
    }

    /* =====================
     * Internal Methods
     * ===================== */

    private void ClearAllSlots()
    {
        foreach (var slot in slots)
        {
            slot.Clear();
        }
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Count;
    }

    private int CalculateNextIndex(int currentIndex, Vector2 direction)
    {
        int row = currentIndex / columns;
        int col = currentIndex % columns;

        // 좌우 이동 우선
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            col += direction.x > 0 ? 1 : -1;
        }
        // 상하 이동
        else
        {
            row += direction.y > 0 ? -1 : 1; // Y축 반전 (위=1, 아래=-1)
        }

        // 범위 체크
        col = Mathf.Clamp(col, 0, columns - 1);
        
        int nextIndex = row * columns + col;

        // 유효한 인덱스인지 확인
        return IsValidIndex(nextIndex) ? nextIndex : currentIndex;
    }

    private void ScrollToItem(int index)
    {
        if (scrollRect == null || slotContainer == null) return;
        
        // TODO: 선택된 아이템이 보이도록 스크롤 위치 조정
        // Canvas.ForceUpdateCanvases() 등을 활용
    }
}