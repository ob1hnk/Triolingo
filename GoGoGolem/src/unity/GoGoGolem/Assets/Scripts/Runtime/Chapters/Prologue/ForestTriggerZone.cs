using UnityEngine;
using UnityEngine.Events;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬 범용 트리거 존
    ///
    /// 플레이어 진입시 _onPlayerEnter UnityEvent 발동
    ///
    /// 트리거 존 오브젝트 세팅:
    ///   - BoxCollider → Is Trigger 체크
    ///   - On Player Enter: 호출할 메서드 연결
    ///   - 플레이어 태그 "Player" 확인
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ForestTriggerZone : MonoBehaviour
    {
        [Tooltip("플레이어 진입 시 발동할 이벤트. Inspector에서 원하는 메서드 연결.")]
        [SerializeField] private UnityEvent _onPlayerEnter;

        [Tooltip("트리거할 대상 태그")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("한 번만 트리거할지 여부 (기본 true)")]
        [SerializeField] private bool _triggerOnce = true;

        private bool _triggered = false;

        private void Start()
        {
            Collider col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                col.isTrigger = true;
                Debug.LogWarning("[ForestTriggerZone] Collider의 Is Trigger가 꺼져 있어서 자동으로 켰습니다.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggerOnce && _triggered) return;
            if (!other.CompareTag(_playerTag)) return;

            _triggered = true;
            _onPlayerEnter?.Invoke();
            Debug.Log($"[ForestTriggerZone] [{gameObject.name}] 플레이어 진입 감지 → 이벤트 발동");
        }

        /// <summary>
        /// 외부에서 트리거를 초기화할 때 사용 (재사용 필요 시)
        /// </summary>
        public void ResetTrigger() => _triggered = false;

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