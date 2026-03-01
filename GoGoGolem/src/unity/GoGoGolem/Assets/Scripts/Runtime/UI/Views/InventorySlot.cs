using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage;

    [Header("Sprites")]
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite backgroundNormalSprite;
    [SerializeField] private Sprite backgroundSelectedSprite;

    private int slotIndex = -1;

    public System.Action<int> OnSlotClicked;

    public void SetIndex(int index)
    {
        slotIndex = index;
    }

    public void SetItem()
    {
        if (backgroundImage != null)
            backgroundImage.sprite = backgroundNormalSprite;

        gameObject.SetActive(true);
    }

    public void ShowAsEmpty()
    {
        if (backgroundImage != null)
            backgroundImage.sprite = backgroundNormalSprite;

        SetSelected(false);
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        ShowAsEmpty();
    }

    public void SetSelected(bool selected)
    {
        if (borderImage != null)
            borderImage.sprite = borderSprite;

        if (backgroundImage != null)
            backgroundImage.sprite = selected ? backgroundSelectedSprite : backgroundNormalSprite;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex);
    }
}