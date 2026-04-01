using UnityEngine;
using UnityEngine.Events;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬 범용 트리거 존
    ///
    /// 세팅:
    ///   - BoxCollider → Is Trigger 체크
    ///   - On Player Enter: 호출할 메서드를 Inspector에서 직접 연결
    ///   - 플레이어 태그 "Player" 확인
    ///
    /// 예시:
    ///   Zone 1 → ForestEventController.OnPlayerEnterTrigger
    ///   Zone 2 → ForestEventController.OnPlayerEnterRainStart
    ///   Zone 3 → LeafEventController.OnLeafTimelineTrigger
    ///   Zone 4 → ForestEventController.OnPlayerEnterRainStop
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ForestTriggerZone : MonoBehaviour
    {
        [Tooltip("트리거할 대상 태그")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("플레이어 진입 시 호출할 메서드 (Inspector에서 연결)")]
        [SerializeField] private UnityEvent _onPlayerEnter;

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
            if (_triggered) return;
            if (!other.CompareTag(_playerTag)) return;

            _triggered = true;
            Debug.Log($"[ForestTriggerZone] {gameObject.name} 플레이어 진입");
            _onPlayerEnter?.Invoke();
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