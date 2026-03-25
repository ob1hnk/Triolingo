using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 아이템 획득 시 HUD 위에 잠깐 표시되는 토스트 알림.
/// HUDCanvas/GotItemToast 하위에 부착한다.
/// InventoryLogic.OnItemAdded 이벤트를 구독한다.
/// </summary>
public class GotItemToastPresenter : MonoBehaviour
{
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text messageText;

    [SerializeField] private float displayDuration = 2.5f;

    private InventoryLogic _inventoryLogic;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (toastRoot != null) toastRoot.SetActive(false);
    }

    private void Start()
    {
        if (Managers.Instance == null || Managers.Inventory == null)
        {
            Debug.LogWarning("[GotItemToastPresenter] Managers가 초기화되지 않았습니다.");
            return;
        }

        _inventoryLogic = Managers.Inventory.Logic;
        if (_inventoryLogic == null)
        {
            Debug.LogWarning("[GotItemToastPresenter] InventoryLogic을 찾을 수 없습니다.");
            return;
        }

        _inventoryLogic.OnItemAdded += ShowToast;
    }

    private void OnDestroy()
    {
        if (_inventoryLogic != null)
            _inventoryLogic.OnItemAdded -= ShowToast;
    }

    private void ShowToast(string itemID, int count)
    {
        var inventory = Managers.Inventory;
        ItemData data = inventory != null && inventory.ItemDB != null ? inventory.ItemDB.GetItem(itemID) : null;

        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        string displayName = data != null ? data.itemName : itemID;
        if (messageText != null)
            messageText.text = count > 1 ? $"{displayName} x{count} 획득!" : $"{displayName} 획득!";

        if (toastRoot != null) toastRoot.SetActive(true);

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        if (toastRoot != null) toastRoot.SetActive(false);
    }
}
