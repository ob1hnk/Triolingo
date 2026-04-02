using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f;
    public LayerMask interactableLayer;

    [Header("Event Channels")]
    [SerializeField] private GameEvent onGatherEvent;  // Animator의 isGathering 트리거 대신 사용
    // onSleepEvent 등 추후 Timeline 연동 시 동일 패턴으로 추가

    private IInteractable currentInteractable;
    private GameInputActions.PlayerActionsActions _playerActions;
    private bool _initialized = false;

    private void Start()
    {
        if (InputModeController.Instance == null)
        {
            Debug.LogError("[PlayerInteraction] InputModeController를 찾을 수 없습니다.");
            return;
        }

        _playerActions = InputModeController.Instance.GetPlayerActionsActions();
        _playerActions.Gather.performed += OnGather;
        _playerActions.InteractNPC.performed += OnInteract;
        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        _playerActions.Gather.performed += OnGather;
        _playerActions.InteractNPC.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        _playerActions.Gather.performed -= OnGather;
        _playerActions.InteractNPC.performed -= OnInteract;
    }

    private void OnGather(InputAction.CallbackContext ctx)
    {
        if (currentInteractable?.InteractionType == InteractionType.Gather)
            PerformGather();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (currentInteractable == null) return;
        if (currentInteractable.InteractionType != InteractionType.Gather)
            currentInteractable.Interact();
    }

    void Update()
    {
        CheckForInteractables();
    }

    private void PerformGather()
    {
        if (onGatherEvent != null) onGatherEvent.Raise();
        currentInteractable.Interact();
    }

    void CheckForInteractables()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange, interactableLayer);

        IInteractable closest = null;
        float minDistance = Mathf.Infinity;

        foreach (var col in colliders)
        {
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract)
            {
                float dist = Vector3.Distance(transform.position, col.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = interactable;
                }
            }
        }

        currentInteractable = closest;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = (currentInteractable != null) ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
