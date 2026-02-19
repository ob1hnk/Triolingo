using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [SerializeField] private string itemID;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    public string GetInteractText() => "줍기 (E)";
    public string ItemID => itemID;

    private void Start()
    {
        if (string.IsNullOrEmpty(itemID))
        {
            Debug.LogError("Item ID가 설정되지 않았습니다: " + gameObject.name);
        }
    }

    public void Interact()
    {
        if (requestAcquireItemEvent != null)
        {
            requestAcquireItemEvent.Raise(itemID);
        }
        else
        {
            Debug.LogError($"[Item] RequestAcquireItem 이벤트가 연결되지 않았습니다: {gameObject.name}");
        }
        Destroy(gameObject);
    }
}
