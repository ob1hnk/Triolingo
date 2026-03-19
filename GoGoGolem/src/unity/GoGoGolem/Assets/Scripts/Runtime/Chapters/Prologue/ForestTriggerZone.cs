using UnityEngine;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 이벤트 트리거 존
    /// 
    /// 세팅:
    ///   - BoxCollider → Is Trigger 체크
    ///   - Forest Event Controller 연결
    ///   - 플레이어 태그 "Player" 확인
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ForestTriggerZone : MonoBehaviour
    {
        [SerializeField] private ForestEventController _forestEventController;
        [Tooltip("트리거할 대상 태그")]
        [SerializeField] private string _playerTag = "Player";

        private bool _triggered = false;

        private void Start()
        {
            if (_forestEventController == null)
                Debug.LogError("[ForestTriggerZone] ForestEventController가 연결되지 않았습니다!");

            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[ForestTriggerZone] Collider의 Is Trigger가 꺼져 있어서 자동으로 켰습니다.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag(_playerTag)) return;

            _triggered = true;
            _forestEventController?.OnPlayerEnterTrigger();
            Debug.Log("[ForestTriggerZone] 플레이어 진입 감지 → ForestEventController 호출");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = oldMatrix;
        }
#endif
    }
}