using TMPro;
using UnityEngine;

public class ItemInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject contentArea;
    [SerializeField] private GameObject emptyMessage;

    [Header("Content Elements")]
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    private void Awake()
    {
        ValidateReferences();
        ShowEmpty();
    }

    private void ValidateReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (contentArea == null)
        {
            Transform t = transform.Find("ContentArea");
            if (t == null) t = transform.Find("Content");
            if (t != null) contentArea = t.gameObject;
        }

        if (emptyMessage == null)
        {
            Transform t = transform.Find("EmptyMessage");
            if (t == null) t = transform.Find("Empty");
            if (t != null) emptyMessage = t.gameObject;
        }

        if (contentArea != null)
        {
            var texts = contentArea.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                string name = text.gameObject.name.ToLower();
                if (itemNameText == null && name.Contains("name"))
                    itemNameText = text;
                else if (descriptionText == null && (name.Contains("desc") || name.Contains("description")))
                    descriptionText = text;
            }
        }

        if (contentArea == null) Debug.LogWarning("[ItemInfoPanel] contentArea가 연결되지 않았습니다.");
        if (itemNameText == null) Debug.LogWarning("[ItemInfoPanel] itemNameText가 연결되지 않았습니다.");
        if (descriptionText == null) Debug.LogWarning("[ItemInfoPanel] descriptionText가 연결되지 않았습니다.");
    }

    public void ShowItemInfo(ItemData itemData)
    {
        if (itemData == null)
        {
            ShowEmpty();
            return;
        }

        if (contentArea != null)
            contentArea.SetActive(true);

        if (emptyMessage != null)
            emptyMessage.SetActive(false);

        if (itemNameText != null)
            itemNameText.text = itemData.itemName;

        if (descriptionText != null)
            descriptionText.text = itemData.description;
    }

    public void ShowEmpty()
    {
        if (contentArea != null)
            contentArea.SetActive(false);

        if (emptyMessage != null)
            emptyMessage.SetActive(true);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }
}
