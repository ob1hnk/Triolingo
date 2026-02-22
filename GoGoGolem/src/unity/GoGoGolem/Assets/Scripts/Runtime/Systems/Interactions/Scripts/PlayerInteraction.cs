using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f;
    public LayerMask interactableLayer;

    private IInteractable currentInteractable;
    private Animator _animator;
    private static int isGatheringHash = Animator.StringToHash("isGathering");

    private GameInputActions.PlayerActionsActions _playerActions;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (InputModeController.Instance == null) return;
        _playerActions = InputModeController.Instance.GetPlayerActionsActions();
        _playerActions.Gather.performed += OnInteract;
    }

    private void OnDisable()
    {
        _playerActions.Gather.performed -= OnInteract;
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (currentInteractable != null)
            PerformInteraction();
    }

    void Update()
    {
        CheckForInteractables();
    }

    private void PerformInteraction()
    {
        _animator.SetTrigger(isGatheringHash);
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
            if (interactable != null)
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
