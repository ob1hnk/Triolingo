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
    /// 발동 조건 (ForestQuestController 연결 시 활성화):
    ///   Required Phase ID        : 이 phase가 완료돼야 발동. 비워두면 조건 없음.
    ///   Blocked Phase ID         : 이 phase가 완료되면 발동 안 함. 비워두면 조건 없음.
    ///   Required Quest Completed : 이 퀘스트가 완료돼야 발동. 비워두면 조건 없음.
    ///   Blocked Quest Completed  : 이 퀘스트가 완료되면 발동 안 함. 비워두면 조건 없음.
    ///   ForestQuestController가 없으면 조건 체크를 건너뛰고 항상 발동 (안전 fallback).
    ///
    /// 씬별 설정 예시:
    ///   Zone 0 (스폰)    Blocked Phase  = MQ-01-P01   → On Player Enter: ForestEventController.OnPlayerEnterDialogue (DLG_001)
    ///   Zone 1 (통나무)  Blocked Phase  = MQ-01-P04   → On Player Enter: ForestEventController.OnPlayerEnterTrigger
    ///   Zone 2, 3        Blocked Phase  = MQ-01-P05   → On Player Enter: 각각 Rain/Leaf
    ///   Zone 4 (비 멈춤) Required Quest = MQ-01
    ///                    Blocked Phase  = MQ-02-P01   → On Player Enter: ForestEventController.OnPlayerEnterRainStop
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ForestTriggerZone : MonoBehaviour
    {
        [Header("Trigger")]
        [Tooltip("트리거할 대상 태그")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("플레이어 진입 시 호출할 메서드 (Inspector에서 연결)")]
        [SerializeField] private UnityEvent _onPlayerEnter;

        [Header("Quest Conditions (선택 — ForestQuestController 연결 필요)")]
        [Tooltip("Forest 씬의 ForestQuestController. 없으면 조건 체크 스킵.")]
        [SerializeField] private ForestQuestController _questController;

        [Tooltip("이 Phase ID가 완료돼야 발동. 비워두면 조건 없음.")]
        [SerializeField] private string _requiredPhaseID = "";

        [Tooltip("이 Phase ID가 완료되면 발동 안 함. 비워두면 조건 없음.")]
        [SerializeField] private string _blockedPhaseID = "";

        [Tooltip("이 Quest ID가 완전히 완료돼야 발동. 비워두면 조건 없음.")]
        [SerializeField] private string _requiredQuestCompleted = "";

        [Tooltip("이 Quest ID가 완전히 완료되면 발동 안 함. 비워두면 조건 없음.")]
        [SerializeField] private string _blockedQuestCompleted = "";

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
            if (!CheckQuestConditions()) return;

            _triggered = true;
            Debug.Log($"[ForestTriggerZone] {gameObject.name} 플레이어 진입");
            _onPlayerEnter?.Invoke();
        }

        // =============================================
        // 퀘스트 조건 체크
        // =============================================

        private bool CheckQuestConditions()
        {
            if (_questController == null) return true;

            // 필수 phase 완료 여부
            if (!string.IsNullOrEmpty(_requiredPhaseID))
            {
                if (!_questController.IsPhaseCompleted(_requiredPhaseID))
                {
                    Debug.Log($"[ForestTriggerZone] {gameObject.name}: 선행 phase 미완료 ({_requiredPhaseID}) → 발동 안 함");
                    return false;
                }
            }

            // 차단 phase: 완료되면 발동 안 함
            if (!string.IsNullOrEmpty(_blockedPhaseID))
            {
                if (_questController.IsPhaseCompleted(_blockedPhaseID))
                {
                    Debug.Log($"[ForestTriggerZone] {gameObject.name}: 차단 phase 완료 ({_blockedPhaseID}) → 발동 안 함");
                    return false;
                }
            }

            // 필수 퀘스트 완료 여부
            if (!string.IsNullOrEmpty(_requiredQuestCompleted))
            {
                if (!_questController.IsQuestCompleted(_requiredQuestCompleted))
                {
                    Debug.Log($"[ForestTriggerZone] {gameObject.name}: 선행 퀘스트 미완료 ({_requiredQuestCompleted}) → 발동 안 함");
                    return false;
                }
            }

            // 차단 퀘스트 완료 시 발동 안 함
            if (!string.IsNullOrEmpty(_blockedQuestCompleted))
            {
                if (_questController.IsQuestCompleted(_blockedQuestCompleted))
                {
                    Debug.Log($"[ForestTriggerZone] {gameObject.name}: 차단 퀘스트 완료 ({_blockedQuestCompleted}) → 발동 안 함");
                    return false;
                }
            }

            return true;
        }

        // =============================================
        // Public API
        // =============================================

        /// <summary>
        /// 트리거를 수동으로 리셋 (재진입 허용).
        /// 씬 재진입 복원 로직에서 필요 시 호출.
        /// </summary>
        public void ResetTrigger()
        {
            _triggered = false;
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