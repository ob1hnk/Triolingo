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
    private bool _initialized = false;

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (InputModeController.Instance == null)
        {
            Debug.LogError("[PlayerInteraction] InputModeController를 찾을 수 없습니다.");
            return;
        }

        _playerActions = InputModeController.Instance.GetPlayerActionsActions();
        _playerActions.Gather.performed += OnGather;
        _playerActions.Interact.performed += OnInteract;
        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized) return;
        _playerActions.Gather.performed += OnGather;
        _playerActions.Interact.performed += OnInteract;
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        _playerActions.Gather.performed -= OnGather;
        _playerActions.Interact.performed -= OnInteract;
    }

    private void OnGather(InputAction.CallbackContext ctx)
    {
        if (currentInteractable?.InteractionType == InteractionType.Gather)
            PerformGather();
    }

    private void OnInteract(InputAction.CallbackContext ctx)
    {
        if (currentInteractable?.InteractionType == InteractionType.Talk)
            PerformTalk();
    }

    void Update()
    {
        CheckForInteractables();
    }

    private void PerformGather()
    {
        _animator.SetTrigger(isGatheringHash);
        currentInteractable.Interact();
    }

    private void PerformTalk()
    {
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
