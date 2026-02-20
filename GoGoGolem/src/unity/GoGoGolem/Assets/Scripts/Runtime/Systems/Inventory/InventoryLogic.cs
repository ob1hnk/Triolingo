using System;
using System.Collections.Generic;
public class InventoryLogic
{
    // 아이템 ID와 개수를 저장하는 딕셔너리
    private Dictionary<string, int> items = new Dictionary<string, int>();
    
    // 데이터 변경 시 UI에 알리기 위한 이벤트
    public event Action OnInventoryChanged;

    public void AddItem(string id, int count = 1)
    {
        if (items.ContainsKey(id))
        {
            items[id] += count;
        }
        else
        {
            items[id] = count;
        }

        int subscriberCount = OnInventoryChanged?.GetInvocationList().Length ?? 0;


        // 구독 중인 UI가 있다면 신호를 보냄
        OnInventoryChanged?.Invoke();
    }

    public bool HasItem(string id) => items.ContainsKey(id) && items[id] > 0;

    public Dictionary<string, int> GetAllItems() => items;
    
}