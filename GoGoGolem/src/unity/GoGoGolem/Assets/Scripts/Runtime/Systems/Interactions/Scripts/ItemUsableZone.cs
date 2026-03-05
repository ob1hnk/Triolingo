using UnityEngine;

/// <summary>
/// 플레이어가 이 존 안에 있을 때 인벤토리에서 특정 아이템을 사용할 수 있다.
/// Trigger Collider가 필요하며, 플레이어 오브젝트에 "Player" 태그가 있어야 한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemUsableZone : MonoBehaviour
{
    /// <summary>현재 플레이어가 위치한 존. 없으면 null.</summary>
    public static ItemUsableZone Current { get; private set; }

    [Header("Zone Settings")]
    [Tooltip("비워두면 모든 아이템 허용")]
    [SerializeField] private string acceptedItemID;

    [Header("Spawn")]
    [SerializeField] private GameObject itemPrefab;
    [Tooltip("비워두면 이 오브젝트 위치에 스폰")]
    [SerializeField] private Transform spawnPoint;

    [Header("Visual")]
    [Tooltip("플레이어 진입 시 활성화할 발광 오브젝트 (자식으로 배치)")]
    [SerializeField] private GameObject glowIndicator;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (glowIndicator != null) glowIndicator.SetActive(false);
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
            if (glowIndicator != null) glowIndicator.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Current = this;
        if (glowIndicator != null) glowIndicator.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || Current != this) return;
        Current = null;
        if (glowIndicator != null) glowIndicator.SetActive(false);
    }

    public bool Accepts(string itemID)
    {
        return string.IsNullOrEmpty(acceptedItemID) || itemID == acceptedItemID;
    }

    public void SpawnItem()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning($"[ItemUsableZone] itemPrefab이 설정되지 않았습니다: {gameObject.name}");
            return;
        }

        Transform sp = spawnPoint != null ? spawnPoint : transform;
        Instantiate(itemPrefab, sp.position, sp.rotation);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Current == this ? Color.green : new Color(0f, 1f, 0f, 0.25f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
            Gizmos.DrawWireCube(transform.position + box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
    }
#endif
}
