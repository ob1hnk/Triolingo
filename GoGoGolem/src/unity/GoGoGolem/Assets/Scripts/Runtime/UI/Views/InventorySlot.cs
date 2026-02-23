using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color emptyBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color filledBackgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private int slotIndex = -1;

    public System.Action<int> OnSlotClicked;

    public void SetIndex(int index)
    {
        slotIndex = index;
    }

    public void SetItem(string itemName, int count)
    {
        if (nameText != null)
            nameText.text = itemName;

        if (countText != null)
            countText.text = count > 1 ? count.ToString() : string.Empty;

        if (backgroundImage != null)
            backgroundImage.color = filledBackgroundColor;

        gameObject.SetActive(true);
    }

    public void ShowAsEmpty()
    {
        if (nameText != null)
            nameText.text = string.Empty;

        if (countText != null)
            countText.text = string.Empty;

        if (backgroundImage != null)
            backgroundImage.color = emptyBackgroundColor;

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
        {
            borderImage.color = selected ? selectedColor : normalColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex);
    }
}
