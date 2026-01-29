using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage; // 배경 이미지 추가
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color emptyBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 빈 슬롯 배경색
    [SerializeField] private Color filledBackgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f); // 아이템 있는 슬롯 배경색

    /// <summary>
    /// 아이템을 슬롯에 설정
    /// </summary>
    public void SetItem(string itemName, int count)
    {
        if (nameText != null)
            nameText.text = itemName;
        
        if (countText != null)
            countText.text = count > 1 ? count.ToString() : string.Empty;
        
        // 배경색 변경 (아이템이 있음을 표시)
        if (backgroundImage != null)
            backgroundImage.color = filledBackgroundColor;
        
        gameObject.SetActive(true);
        
        Debug.Log($"<color=lime>[InventorySlot]</color> 슬롯 활성화: {itemName} x{count}");
    }

    /// <summary>
    /// 빈 슬롯으로 표시 (슬롯은 보이지만 아이템은 없음)
    /// </summary>
    public void ShowAsEmpty()
    {
        if (nameText != null)
            nameText.text = string.Empty;
        
        if (countText != null)
            countText.text = string.Empty;
        
        // 배경색 변경 (빈 슬롯 표시)
        if (backgroundImage != null)
            backgroundImage.color = emptyBackgroundColor;
        
        SetSelected(false);
        
        gameObject.SetActive(true); // 중요: 활성화 상태 유지
    }

    /// <summary>
    /// 슬롯을 완전히 숨김 (사용하지 않음)
    /// </summary>
    public void Clear()
    {
        ShowAsEmpty();
    }

    /// <summary>
    /// 선택 상태 설정
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (borderImage != null)
        {
            borderImage.color = selected ? selectedColor : normalColor;
        }
    }
}