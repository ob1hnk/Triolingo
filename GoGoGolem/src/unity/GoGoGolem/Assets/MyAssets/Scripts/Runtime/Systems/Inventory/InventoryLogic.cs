using System;
using System.Collections.Generic;
using UnityEngine;
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
            Debug.Log($"<color=cyan>[InventoryLogic]</color> 아이템 추가: {id} (기존 {items[id] - count}개 → 총 {items[id]}개)");
        }
        else
        {
            items[id] = count;
            Debug.Log($"<color=cyan>[InventoryLogic]</color> 새 아이템 추가: {id} x{count}");
        }

        int subscriberCount = OnInventoryChanged?.GetInvocationList().Length ?? 0;
        Debug.Log($"<color=cyan>[InventoryLogic]</color> OnInventoryChanged 구독자 수: {subscriberCount}");


        // 구독 중인 UI가 있다면 신호를 보냄
        OnInventoryChanged?.Invoke();

        Debug.Log($"<color=cyan>[InventoryLogic]</color> 현재 인벤토리 총 {items.Count}개 아이템 보유");
        foreach (var item in items)
        {
            Debug.Log($" - {item.Key}: {item.Value}개");
        }
    }

    public bool HasItem(string id) => items.ContainsKey(id) && items[id] > 0;

    public Dictionary<string, int> GetAllItems() => items;
    
}