using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [SerializeField] private string itemID;
    public string GetInteractText() => "줍기 (E)";

    public void Interact()
    {
        if (Managers.Inventory == null)
        {
            Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
            return;
        }
        Managers.Inventory.AcquireItem(itemID);
        Debug.Log("아이템 획득: " + itemID);
        Destroy(gameObject); // 아이템을 줍고 사라지게 함
    }
}