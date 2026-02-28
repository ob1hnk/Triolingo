using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfoPanel : MonoBehaviour
{
    [Header("UI Images")]
    [SerializeField] private Image itemNameImage;
    [SerializeField] private Image itemInfoBackground;
    [SerializeField] private Image itemInfoBorder;

    [Header("Sprites")]
    [SerializeField] private Sprite itemNameSprite;
    [SerializeField] private Sprite itemInfoBackgroundSprite;
    [SerializeField] private Sprite itemInfoBorderSprite;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("State")]
    [SerializeField] private GameObject contentArea;
    [SerializeField] private GameObject emptyMessage;

    private void Awake()
    {
        if (itemNameImage != null)       itemNameImage.sprite       = itemNameSprite;
        if (itemInfoBackground != null)  itemInfoBackground.sprite  = itemInfoBackgroundSprite;
        if (itemInfoBorder != null)      itemInfoBorder.sprite      = itemInfoBorderSprite;

        ShowEmpty();
    }

    public void ShowItemInfo(ItemData itemData)
    {
        if (itemData == null)
        {
            ShowEmpty();
            return;
        }

        if (contentArea != null)  contentArea.SetActive(true);
        if (emptyMessage != null) emptyMessage.SetActive(false);

        if (itemNameText != null)    itemNameText.text    = itemData.itemName;
        if (descriptionText != null) descriptionText.text = itemData.description;
    }

    public void ShowEmpty()
    {
        if (contentArea != null)  contentArea.SetActive(false);
        if (emptyMessage != null) emptyMessage.SetActive(true);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}