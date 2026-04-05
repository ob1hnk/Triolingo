using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Leaf 이벤트 상태
    /// </summary>
    public enum LeafEventState
    {
        Idle,         // 대기
        LeafTimeline, // 트리거 존 3: 나뭇잎 타임라인 재생 중
        Complete      // 완료
    }

    /// <summary>
    /// Forest 씬 Leaf 이벤트 흐름 제어 (Glue 코드)
    ///
    /// 흐름:
    ///   [트리거 존 3] OnLeafTimelineTrigger()
    ///     → 주인공 제어 비활성화
    ///     → LeafTimeline 재생
    ///       (Timeline 내부: 카메라 전환, 골렘 이동, 나뭇잎 activation,
    ///        손 부착, 골렘 복귀, 주인공 손 뻗기)
    ///     → Signal: OnNpcSpeech()         → NPC(골렘) 말풍선: "우산이에요!"
    ///     → Signal: OnPlayerReceiveLeaf() → 나뭇잎 전달 + 주인공 말풍선: "고마워"
    ///     → Signal: OnItemReceived()      → 아이템 획득 (TODO: Quest/Inventory 연동)
    ///     → Timeline 종료 → 주인공 제어 복원 → Complete
    ///
    /// Inspector 세팅:
    ///   - Leaf Timeline Director: 트리거 존 3용 PlayableDirector
    ///   - Player Speech Bubble:   주인공 FollowSpeechBubbleView
    ///   - Npc Speech Bubble:      골렘 FollowSpeechBubbleView
    ///   - Player*:                ForestEventController와 동일한 레퍼런스 연결
    ///   - Walk Duration:          이동에 걸리는 시간 (초)
    ///   - Rotate Duration:        도착 후 최종 회전 정렬에 걸리는 시간 (초)
    ///
    /// 나뭇잎 부착 오프셋:
    ///   _golemHandPositionOffset / _golemHandRotationOffset
    ///   _playerHandPositionOffset / _playerHandRotationOffset
    ///   → Inspector에서 위치/회전 조절 가능
    /// </summary>
    public class LeafEventController : MonoBehaviour
    {
        [Header("트리거 존 3 - 나뭇잎 타임라인")]
        [Tooltip("나뭇잎 이벤트 Timeline PlayableDirector")]
        [SerializeField] private PlayableDirector _leafDirector;

        [Header("나뭇잎 오브젝트")]
        [Tooltip("나뭇잎 오브젝트 (평소엔 비활성화)")]
        [SerializeField] private GameObject _leafObject;
 
        [Header("골렘 손 부착")]
        [Tooltip("골렘 손 Bone Transform")]
        [SerializeField] private Transform _golemHandBone;
        [Tooltip("골렘 손 기준 나뭇잎 위치 오프셋")]
        [SerializeField] private Vector3 _golemHandPositionOffset = Vector3.zero;
        [Tooltip("골렘 손 기준 나뭇잎 회전 오프셋 (Euler)")]
        [SerializeField] private Vector3 _golemHandRotationOffset = Vector3.zero;
 
        [Header("주인공 손 부착")]
        [Tooltip("주인공 손 Bone Transform")]
        [SerializeField] private Transform _playerHandBone;
        [Tooltip("주인공 손 기준 나뭇잎 위치 오프셋")]
        [SerializeField] private Vector3 _playerHandPositionOffset = Vector3.zero;
        [Tooltip("주인공 손 기준 나뭇잎 회전 오프셋 (Euler)")]
        [SerializeField] private Vector3 _playerHandRotationOffset = Vector3.zero;

        [Header("Walk In")]
        [Tooltip("플레이어가 이동할 목표")]
        [SerializeField] private Transform _playerStartPoint;
        [Tooltip("골렘이 이동할 목표 위치 (트리거 존 3용)")]
        [SerializeField] private Transform _golemStartPoint;
        [Tooltip("골렘 NavMesh 이동 제어 컴포넌트")]
        [SerializeField] private GolemFollow _golemFollow;
        [Tooltip("위치 이동에 걸리는 시간 (초)")]
        [SerializeField] private float _walkDuration = 1f;
        [Tooltip("도착 후 최종 회전 정렬에 걸리는 시간 (초)")]
        [SerializeField] private float _rotateDuration = 0.5f;

        [Header("Player")]
        [Tooltip("Player Transform")]
        [SerializeField] private Transform _player;
        [Tooltip("Player 이동 권한 제어")]
        [SerializeField] private PlayerAnimation _playerAnimation;
        [SerializeField] private PlayerLocomotionInput _playerLocomotionInput;
        [SerializeField] private MonoBehaviour _playerControllerScript;
        [Tooltip("플레이어 Animator")]
        [SerializeField] private Animator _playerAnimator;
        [Tooltip("플레이어 걷기 파라미터 이름 (Float, inputMagnitude)")]
        [SerializeField] private string _playerWalkParam = "inputMagnitude";

        [Header("말풍선")]
        [Tooltip("주인공 머리 위 말풍선 (FollowSpeechBubbleView)")]
        [SerializeField] private FollowSpeechBubbleView _playerSpeechBubble;
        [Tooltip("NPC(골렘) 머리 위 말풍선 (FollowSpeechBubbleView)")]
        [SerializeField] private FollowSpeechBubbleView _npcSpeechBubble;

        [Header("Debug")]
        [SerializeField] private bool _debugSkipToLeaf = false;

        [Header("Event Channels")]
        [SerializeField] private GameEvent _requestHideHUDEvent;
        [SerializeField] private GameEvent _requestShowHUDEvent;

        private LeafEventState _state = LeafEventState.Idle;

        // =============================================
        // Unity 생명주기
        // =============================================

        private void Start()
        {
            ValidateComponents();
            _leafObject?.SetActive(false);

            if (_debugSkipToLeaf)
                OnLeafTimelineTrigger();
        }

        private void OnDestroy()
        {
            if (_leafDirector != null)
                _leafDirector.stopped -= OnLeafTimelineStopped;
        }

        // =============================================
        // 트리거 존 3 - 나뭇잎 타임라인
        // =============================================

        /// <summary>
        /// 트리거 존 3 → ForestTriggerZone의 OnPlayerEnter에 연결
        /// </summary>
        public void OnLeafTimelineTrigger()
        {
            if (_state == LeafEventState.LeafTimeline || _state == LeafEventState.Complete) return;

            Debug.Log("[LeafEventController] 나뭇잎 타임라인 시작");
            ChangeState(LeafEventState.LeafTimeline);
            _playerSpeechBubble?.Hide();
            _requestHideHUDEvent?.Raise();
            SetPlayerMovement(false);
            ResetPlayerState();

            // 골렘 추적 중단 + 지정 위치로 이동
            _golemFollow?.StopFollowing();
            if (_golemFollow != null && _golemStartPoint != null)
                _golemFollow.MoveToPoint(_golemStartPoint);

            if (_leafDirector == null)
            {
                Debug.LogWarning("[LeafEventController] LeafDirector 없음 → 바로 Complete");
                EnterCompleteState();
                return;
            }

            if (_player != null && _playerStartPoint != null)
                StartCoroutine(AlignPlayerThenPlayTimeline());
            else
            {
                _golemFollow?.DisableAgent();
                _leafDirector.stopped += OnLeafTimelineStopped;
                _leafDirector.Play();
            }
        }

        private IEnumerator AlignPlayerThenPlayTimeline()
        {
            // ── Phase 1: 이동 방향을 바라보며 목적지까지 이동 ──
            // inputY = 1f: 2D Blend Tree가 전진 방향으로 샘플링되도록 강제
            // (PlayerAnimation이 disabled 상태라 inputX/Y가 0으로 고정되기 때문)
            _playerAnimator?.SetFloat(_playerWalkParam, 1f);
            _playerAnimator?.SetFloat("inputY", 1f);

            // 1프레임 대기: Animator 파라미터 → Transition 조건 반영 대기
            yield return null;

            Vector3 startPos = _player.position;
            Vector3 endPos   = _playerStartPoint.position;

            // 이동 방향 회전 계산 (XZ 평면 기준)
            Vector3 moveDir = endPos - startPos;
            moveDir.y = 0f;
            Quaternion moveRotation = moveDir.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(moveDir)
                : _player.rotation;

            // 시작 시 이동 방향으로 즉시 회전 정렬
            _player.rotation = moveRotation;

            // WASD와 동일한 locomotionBlendSpeed로 서서히 올려 자연스러운 가속감 연출
            float currentMag = 0f;
            float currentY   = 0f;

            float elapsed = 0f;
            while (elapsed < _walkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _walkDuration));
                _player.position = Vector3.Lerp(startPos, endPos, t);
                // 이동 중에는 이동 방향 유지
                _player.rotation = moveRotation;

                // inputMagnitude / inputY 서서히 증가 (PlayerAnimation.locomotionBlendSpeed와 동일)
                currentMag = Mathf.Lerp(currentMag, 1f, 3f * Time.deltaTime);
                currentY   = Mathf.Lerp(currentY,   1f, 3f * Time.deltaTime);
                _playerAnimator?.SetFloat(_playerWalkParam, currentMag);
                _playerAnimator?.SetFloat("inputY", currentY);

                yield return null;
            }
            _player.position = endPos;

            // 걷기 애니메이션 OFF
            _playerAnimator?.SetFloat(_playerWalkParam, 0f);
            _playerAnimator?.SetFloat("inputY", 0f);
            _playerAnimation?.ResetBlendInput();

            // ── Phase 2: 도착 후 최종 rotation으로 보간 ──
            Quaternion finalRot = _playerStartPoint.rotation;
            elapsed = 0f;
            while (elapsed < _rotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _rotateDuration));
                _player.rotation = Quaternion.Slerp(moveRotation, finalRot, t);
                yield return null;
            }
            _player.rotation = finalRot;

            // 타임라인 재생
            _golemFollow?.DisableAgent();
            _leafDirector.stopped += OnLeafTimelineStopped;
            _leafDirector.Play();
        }

        /// <summary>
        /// Signal: 골렘이 나뭇잎을 줍는 타이밍
        /// 나뭇잎 활성화 + 골렘 손 bone에 부착
        /// Signal Receiver에서 이 메서드 연결
        /// </summary>
        public void OnGolemPickupLeaf()
        {
            if (_leafObject == null || _golemHandBone == null)
            {
                Debug.LogWarning("[LeafEventController] LeafObject 또는 GolemHandBone 없음");
                return;
            }
 
            _leafObject.SetActive(true);
            AttachLeaf(_golemHandBone, _golemHandPositionOffset, _golemHandRotationOffset);
            Debug.Log("[LeafEventController] 나뭇잎 → 골렘 손에 부착");
        }
 
        /// <summary>
        /// Signal: 골렘이 나뭇잎을 주인공에게 내밀기 직전 타이밍
        /// NPC 말풍선: "우산이에요!"
        /// Signal Receiver에서 이 메서드 연결
        /// </summary>
        public void OnNpcSpeech()
        {
            _npcSpeechBubble?.Show("우산이에요!");
            Debug.Log("[LeafEventController] NPC 말풍선: 우산이에요!");
        }

        /// <summary>
        /// Signal: 골렘이 주인공에게 나뭇잎을 건네는 타이밍
        /// 나뭇잎을 주인공 손 bone으로 이동
        /// Signal Receiver에서 이 메서드 연결
        /// </summary>
        public void OnPlayerReceiveLeaf()
        {
            if (_leafObject == null || _playerHandBone == null)
            {
                Debug.LogWarning("[LeafEventController] LeafObject 또는 PlayerHandBone 없음");
                return;
            }
 
            AttachLeaf(_playerHandBone, _playerHandPositionOffset, _playerHandRotationOffset);
            Debug.Log("[LeafEventController] 나뭇잎 → 주인공 손으로 이동");

            _npcSpeechBubble?.Hide();
            _playerSpeechBubble?.Show("앗... 고마워");
        }

        /// <summary>
        /// Signal: 아이템 획득 메세지 표시 + 주인공 나뭇잎 비활성화 타이밍.
        /// Signal Receiver에서 이 메서드 연결.
        /// </summary>
        public void OnItemReceived()
        {
            if (_state != LeafEventState.LeafTimeline) return;

            // 나뭇잎 주머니에 넣기 (부모 해제 후 비활성화)
            if (_leafObject != null)
            {
                _leafObject.transform.SetParent(null);
                _leafObject.SetActive(false);
            }

            _playerSpeechBubble?.Hide();
            Debug.Log("[LeafEventController] 아이템 획득!");

            // TODO: 한나 Quest/Inventory 시스템 머지 후 아래 주석 해제
            // Managers.Quest.AddItem("leaf");
            // Managers.Inventory.ShowItemGetMessage("leaf");
            Debug.Log("[LeafEventController] TODO: 아이템 획득 메세지 표시 (한나 시스템 연동 후)");
        }

        private void OnLeafTimelineStopped(PlayableDirector director)
        {
            director.stopped -= OnLeafTimelineStopped;
            _playerSpeechBubble?.Hide();
            _npcSpeechBubble?.Hide();
            EnterCompleteState();
        }

        // =============================================
        // 상태 전환
        // =============================================

        private void EnterCompleteState()
        {
            if (_state == LeafEventState.Complete) return;
            ChangeState(LeafEventState.Complete);
            SetPlayerMovement(true);
            _requestShowHUDEvent?.Raise();

            // 골렘 NavMesh Agent 재활성화 + 추적 재개
            _golemFollow?.EnableAgent();
            _golemFollow?.StartFollowingSmooth();

            Debug.Log("[LeafEventController] Leaf 이벤트 완료");
        }

        // =============================================
        // 유틸리티
        // =============================================

        /// <summary>
        /// 나뭇잎을 targetBone의 자식으로 붙이고 offset 적용
        /// </summary>
        private void AttachLeaf(Transform targetBone, Vector3 positionOffset, Vector3 rotationOffset)
        {
            _leafObject.transform.SetParent(targetBone);
            _leafObject.transform.localPosition = positionOffset;
            _leafObject.transform.localRotation = Quaternion.Euler(rotationOffset);
        }

        /// <summary>
        /// 플레이어 속도 및 걷기 애니메이션 즉시 리셋
        /// 타임라인 진입 직전 호출하여 이동 중 잔류 상태 제거
        /// </summary>
        private void ResetPlayerState()
        {
            _playerAnimator?.SetFloat(_playerWalkParam, 0f);
            _playerAnimation?.ResetBlendInput();
            (_playerControllerScript as PlayerController)?.ResetVelocity();
        }

        private void SetPlayerMovement(bool canMove)
        {
            if (_playerAnimation != null)
                _playerAnimation.enabled = canMove;
            if (_playerLocomotionInput != null)
                _playerLocomotionInput.enabled = canMove;
            if (_playerControllerScript != null)
                _playerControllerScript.enabled = canMove;
            Debug.Log($"[LeafEventController] PlayerMovement: {canMove}");
        }

        private void ChangeState(LeafEventState newState)
        {
            if (_state == newState) return;
            Debug.Log($"[LeafEventController] State: {_state} → {newState}");
            _state = newState;
        }

        private void ValidateComponents()
        {
            if (_leafDirector == null)
                Debug.LogWarning("[LeafEventController] LeafDirector 없음");
            if (_leafObject == null)
                Debug.LogWarning("[LeafEventController] LeafObject 없음");
            if (_golemHandBone == null)
                Debug.LogWarning("[LeafEventController] GolemHandBone 없음");
            if (_playerHandBone == null)
                Debug.LogWarning("[LeafEventController] PlayerHandBone 없음");
            if (_player == null)
                Debug.LogWarning("[LeafEventController] Player Transform 없음 → 위치 정렬 스킵");
            if (_playerStartPoint == null)
                Debug.LogWarning("[LeafEventController] PlayerStartPoint 없음 → 위치 정렬 스킵");
            if (_playerAnimator == null)
                Debug.LogWarning("[LeafEventController] PlayerAnimator 없음 → 걷기 애니메이션 스킵");
            if (_playerAnimation == null)
                Debug.LogWarning("[LeafEventController] PlayerAnimation 없음");
            if (_playerLocomotionInput == null)
                Debug.LogWarning("[LeafEventController] PlayerLocomotionInput 없음");
            if (_playerControllerScript == null)
                Debug.LogWarning("[LeafEventController] PlayerControllerScript 없음");
            if (_playerSpeechBubble == null)
                Debug.LogWarning("[LeafEventController] PlayerSpeechBubble 없음 → 주인공 말풍선 스킵");
            if (_npcSpeechBubble == null)
                Debug.LogWarning("[LeafEventController] NpcSpeechBubble 없음 → NPC 말풍선 스킵");
        }
    }
}