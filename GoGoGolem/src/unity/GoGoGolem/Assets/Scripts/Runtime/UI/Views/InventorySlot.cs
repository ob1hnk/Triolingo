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

    // 슬롯의 인덱스 (View에서 설정)
    private int slotIndex = -1;
    
    // 클릭 이벤트 (View가 구독)
    public System.Action<int> OnSlotClicked;

    /// <summary>
    /// 슬롯 인덱스 설정
    /// </summary>
    public void SetIndex(int index)
    {
        slotIndex = index;
    }

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
    /// 빈 슬롯으로 표시
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
        
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 슬롯을 완전히 숨김
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

    /// <summary>
    /// 마우스 클릭 이벤트 처리 (Unity Event System)
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"<color=cyan>[InventorySlot]</color> 슬롯 {slotIndex} 클릭됨");
        
        // 클릭 이벤트 발생
        OnSlotClicked?.Invoke(slotIndex);
    }
}