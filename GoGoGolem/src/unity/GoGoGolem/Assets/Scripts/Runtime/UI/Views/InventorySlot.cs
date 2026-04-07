using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private Image borderImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;

    [Header("Sprites")]
    [SerializeField] private Sprite borderSprite;
    [SerializeField] private Sprite backgroundNormalSprite;
    [SerializeField] private Sprite backgroundSelectedSprite;

    private int slotIndex = -1;
    private CanvasGroup _canvasGroup;
    public bool IsDimmed { get; private set; }

    public System.Action<int> OnSlotClicked;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetIndex(int index)
    {
        slotIndex = index;
    }

    public void SetItem(Sprite icon = null)
    {
        if (backgroundImage != null)
            backgroundImage.sprite = backgroundNormalSprite;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        gameObject.SetActive(true);
    }

    public void ShowAsEmpty()
    {
        if (backgroundImage != null)
            backgroundImage.sprite = backgroundNormalSprite;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

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

    public void SetDimmed(bool dimmed)
    {
        IsDimmed = dimmed;
        if (_canvasGroup == null) return;
        _canvasGroup.alpha = dimmed ? 0.35f : 1f;
        _canvasGroup.interactable = !dimmed;
        _canvasGroup.blocksRaycasts = !dimmed;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex);
    }
}