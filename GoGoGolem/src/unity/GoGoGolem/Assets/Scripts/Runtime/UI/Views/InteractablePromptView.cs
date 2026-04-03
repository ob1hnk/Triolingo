using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 근처의 Interactable 오브젝트 위에 상호작용 힌트 UI를 표시한다.
/// Player GameObject에 부착한다.
/// interactionRange는 PlayerInteraction과 공유한다.
/// </summary>
[RequireComponent(typeof(PlayerInteraction))]
public class InteractablePromptView : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private LayerMask interactableLayer;

    [Header("UI")]
    [SerializeField] private RectTransform promptRoot;
    [SerializeField] private TMP_Text actionLabelText;
    [SerializeField] private Image keyHintImage;

    [Header("Position")]
    [SerializeField] private Camera mainCamera;

    private PlayerInteraction _playerInteraction;
    private Canvas _canvas;
    private IInteractable _current;
    private Transform _currentTransform;

    private void Awake()
    {
        _playerInteraction = GetComponent<PlayerInteraction>();

        if (promptRoot != null)
        {
            _canvas = promptRoot.GetComponentInParent<Canvas>();
            promptRoot.gameObject.SetActive(false);
        }

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        DetectNearest();

        if (_current == null)
        {
            if (promptRoot != null) promptRoot.gameObject.SetActive(false);
            return;
        }

        string label = _current.GetActionLabel();
        if (string.IsNullOrEmpty(label))
        {
            if (promptRoot != null) promptRoot.gameObject.SetActive(false);
            return;
        }

        if (promptRoot != null) promptRoot.gameObject.SetActive(true);
        if (actionLabelText != null) actionLabelText.text = label;

        Sprite keySprite = _current.GetKeyHintSprite();
        if (keyHintImage != null)
        {
            keyHintImage.sprite = keySprite;
            keyHintImage.gameObject.SetActive(keySprite != null);
        }

        UpdatePosition();
    }

    private void DetectNearest()
    {
        float range = _playerInteraction != null ? _playerInteraction.interactionRange : 2f;
        Collider[] colliders = Physics.OverlapSphere(transform.position, range, interactableLayer);

        IInteractable closest = null;
        Transform closestTransform = null;
        float minDist = Mathf.Infinity;

        foreach (var col in colliders)
        {
            var interactable = col.GetComponent<IInteractable>();
            if (interactable == null || !interactable.CanInteract) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = interactable;
                closestTransform = col.transform;
            }
        }

        _current = closest;
        _currentTransform = closestTransform;
    }

    private void UpdatePosition()
    {
        if (_currentTransform == null || mainCamera == null || _canvas == null) return;

        Vector3 worldPos = _currentTransform.position + _current.GetPromptOffset();
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z < 0f)
        {
            promptRoot.gameObject.SetActive(false);
            return;
        }

        Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _canvas.transform as RectTransform,
                screenPos,
                uiCamera,
                out Vector3 worldPoint))
        {
            promptRoot.position = worldPoint;
        }
    }
}