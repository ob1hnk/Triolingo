using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [Header("UI")]
	[SerializeField] private Image borderImage;
	[SerializeField] private TextMeshProUGUI nameText;
	[SerializeField] private TextMeshProUGUI countText;

	[Header("Colors")]
	[SerializeField] private Color normalColor = Color.white;
	[SerializeField] private Color selectedColor = Color.yellow;


	public void SetItem(string itemName, int count)
    {
        nameText.text = itemName;
        countText.text = count > 1 ? count.ToString() : string.Empty;
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        nameText.text = string.Empty;
        countText.text = string.Empty;
        SetSelected(false);
        gameObject.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        if (borderImage != null)
        {
            borderImage.color = selected ? selectedColor : normalColor;
        }
    }
}
