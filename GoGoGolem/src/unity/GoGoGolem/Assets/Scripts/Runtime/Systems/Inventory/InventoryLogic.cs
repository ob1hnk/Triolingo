using System;
using System.Collections.Generic;
public class InventoryLogic
{
    // 아이템 ID와 개수를 저장하는 딕셔너리
    private Dictionary<string, int> items = new Dictionary<string, int>();

    // 데이터 변경 시 UI에 알리기 위한 이벤트
    public event Action OnInventoryChanged;

    // 아이템이 추가될 때 발생 (itemID, count)
    public event Action<string, int> OnItemAdded;

    public void AddItem(string id, int count = 1)
    {
        if (items.ContainsKey(id))
            items[id] += count;
        else
            items[id] = count;

        OnItemAdded?.Invoke(id, count);
        OnInventoryChanged?.Invoke();
    }

    public bool RemoveItem(string id, int count = 1)
    {
        if (!items.TryGetValue(id, out int current) || current < count) return false;
        int next = current - count;
        if (next <= 0) items.Remove(id);
        else items[id] = next;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool HasItem(string id) => items.ContainsKey(id) && items[id] > 0;

    public Dictionary<string, int> GetAllItems() => items;

    public void Clear()
    {
        if (items.Count == 0) return;
        items.Clear();
        OnInventoryChanged?.Invoke();
    }
}