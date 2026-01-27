using MyAssets.FinalCharacterController;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f; // 상호작용 가능 거리 (구체 반지름)
    public LayerMask interactableLayer; // 상호작용 가능한 레이어만 선택 (성능 최적화)
    private IInteractable currentInteractable;

    private Animator _animator;
    private static int isGatheringHash = Animator.StringToHash("isGathering");

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        CheckForInteractables();

        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            PerformInteraction();
        }
    }


    private void PerformInteraction()
    {
        _animator.SetTrigger(isGatheringHash);
        currentInteractable.Interact();
        
        // Debug.Log("상호작용 수행됨: " + currentInteractable.GetInteractText());
    }


    void CheckForInteractables()
    {
        // 3D 구체 범위를 탐색합니다.
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