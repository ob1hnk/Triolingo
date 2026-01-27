using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [SerializeField] private string itemID;
    public string GetInteractText() => "줍기 (E)";
    public string ItemID => itemID;

    private void Awake()
    {
        if (string.IsNullOrEmpty(itemID))
        {
            Debug.LogError("Item ID가 설정되지 않았습니다: " + gameObject.name);
            return;
        }
        if (Managers.Data != null && Managers.Data.ItemDB.GetItem(itemID) == null)
        {
            Debug.LogError("존재하지 않는 Item ID가 설정되었습니다: " + itemID);
        }
    }

    public void Interact()
    {
        if (Managers.Inventory == null)
        {
            Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
            return;
        }
        Managers.Inventory.AcquireItem(itemID);
        Debug.Log("아이템 획득: " + itemID);
        Destroy(gameObject); // 아이템을 씬에서 사라지게 함
    }
}