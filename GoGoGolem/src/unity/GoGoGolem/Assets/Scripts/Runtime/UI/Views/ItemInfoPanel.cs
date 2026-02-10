using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private GameObject contentArea;
    [SerializeField] private GameObject emptyMessage;
    
    [Header("Content Elements")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemTypeText;
    [SerializeField] private TextMeshProUGUI itemPhaseText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI usageText;

    private void Awake()
    {
        // 초기 상태: 빈 메시지 표시
        ShowEmpty();
    }

    /// <summary>
    /// 아이템 정보 표시
    /// </summary>
    public void ShowItemInfo(ItemData itemData)
    {
        if (itemData == null)
        {
            ShowEmpty();
            return;
        }

        // 내용 영역 표시
        if (contentArea != null)
            contentArea.SetActive(true);
        
        if (emptyMessage != null)
            emptyMessage.SetActive(false);

        // 타이틀 설정
        if (titleText != null)
            titleText.text = itemData.itemName;

        // 아이콘 (현재는 색상으로 구분, 나중에 실제 아이콘 추가)
        if (itemIcon != null)
        {
            itemIcon.color = GetColorByType(itemData.type);
        }

        // 아이템 이름
        if (itemNameText != null)
            itemNameText.text = itemData.itemName;

        // 타입
        if (itemTypeText != null)
            itemTypeText.text = $"[{GetTypeDisplayName(itemData.type)}]";

        // 단계
        if (itemPhaseText != null)
            itemPhaseText.text = $"단계: {itemData.phase}";

        // 설명
        if (descriptionText != null)
            descriptionText.text = itemData.description;

        // 용도
        if (usageText != null)
            usageText.text = itemData.usage;

        Debug.Log($"<color=cyan>[ItemInfoPanel]</color> {itemData.itemName} 정보 표시");
    }

    /// <summary>
    /// 빈 상태 표시
    /// </summary>
    public void ShowEmpty()
    {
        if (contentArea != null)
            contentArea.SetActive(false);
        
        if (emptyMessage != null)
            emptyMessage.SetActive(true);

        if (titleText != null)
            titleText.text = "아이템 정보";

        Debug.Log("<color=cyan>[ItemInfoPanel]</color> 빈 상태 표시");
    }

    /// <summary>
    /// 패널 표시/숨김
    /// </summary>
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

    /// <summary>
    /// 아이템 타입별 색상 (임시, 나중에 실제 아이콘으로 교체)
    /// </summary>
    private Color GetColorByType(ItemType type)
    {
        switch (type)
        {
            case ItemType.Item:
                return new Color(0.5f, 0.8f, 0.5f); // 초록
            case ItemType.Skill:
                return new Color(0.5f, 0.6f, 1f); // 파랑
            case ItemType.Reward:
                return new Color(1f, 0.8f, 0.3f); // 금색
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// 타입 표시명
    /// </summary>
    private string GetTypeDisplayName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Item:
                return "아이템";
            case ItemType.Skill:
                return "스킬";
            case ItemType.Reward:
                return "보상";
            default:
                return "알 수 없음";
        }
    }
}