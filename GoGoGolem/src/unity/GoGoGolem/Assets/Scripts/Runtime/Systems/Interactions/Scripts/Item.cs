using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    [Header("Item Info")]
    [SerializeField] private string itemID;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    public InteractionType InteractionType => InteractionType.Gather;
    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "줍기";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);
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
