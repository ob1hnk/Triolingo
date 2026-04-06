using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f;
    public LayerMask interactableLayer;

    [Header("Event Channels")]
    [SerializeField] private GameEvent onGatherEvent;

    private IInteractable currentInteractable;
    private GameInputActions.PlayerActionsActions _playerActions;

    private void Start()
    {
        if (InputModeController.Instance == null)
        {
            Debug.LogError("[PlayerInteraction] InputModeController를 찾을 수 없습니다.");
            return;
        }

        _playerActions = InputModeController.Instance.GetPlayerActionsActions();
    }

    private void OnEnable()
    {
        if (InputModeController.Instance == null) return;

        _playerActions = InputModeController.Instance.GetPlayerActionsActions();
        _playerActions.Gather.performed += OnGather;
        _playerActions.InteractNPC.performed += OnInteract;
    }

    private void OnDisable()
    {
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
        Debug.Log("[PlayerInteraction] OnInteract called");  // 임시
        if (currentInteractable == null) return;

        switch (currentInteractable.InteractionType)
        {
            case InteractionType.TalkNPC:
            case InteractionType.TalkGolem:
            case InteractionType.WriteLetter:
            case InteractionType.Sleep:
            case InteractionType.ChangeScene:
                currentInteractable.Interact();
                break;
        }
    }

    private void Update()
    {
        CheckForInteractables();
    }

    private void PerformGather()
    {
        if (onGatherEvent != null) onGatherEvent.Raise();
        currentInteractable.Interact();
    }

    private void CheckForInteractables()
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