using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬 이벤트 상태
    /// </summary>
    public enum ForestEventState
    {
        Idle,          // 대기 (트리거 진입 전)
        WalkIn,        // 위치 이동 중 (코루틴)
        Dialogue,      // Yarn 대화 중 (선택지 표시)
        PushTimeline,  // 밀기 Timeline 재생 중
        LiftTimeline,  // 들기 Timeline 재생 중
        Complete       // 이벤트 종료
    }

    /// <summary>
    /// Forest 씬 전체 흐름 제어 (Glue 코드)
    ///
    /// 흐름:
    ///   Idle
    ///    └─ 트리거 진입 → WalkIn (플레이어/골렘 지정 위치로 이동)
    ///         └─ 이동 완료 → Dialogue (Yarn 실행)
    ///               ├─ 밀기 선택(<<forest_choose_push>>) → PushTimeline
    ///               └─ 들기 선택(<<forest_choose_lift>>) → LiftTimeline
    ///                     └─ Timeline 종료 → Complete
    ///
    /// Inspector 세팅:
    ///   - Player Start Point: 플레이어가 이동할 목표 위치 Transform
    ///   - Golem Start Point:  골렘이 이동할 목표 위치 Transform
    ///   - Walk Duration:      이동에 걸리는 시간 (초)
    /// </summary>
    public class ForestEventController : MonoBehaviour
    {
        [Header("Timelines")]
        [Tooltip("골렘이 통나무를 미는 Timeline")]
        [SerializeField] private PlayableDirector _pushDirector;
        [Tooltip("골렘이 통나무를 드는 Timeline")]
        [SerializeField] private PlayableDirector _liftDirector;

        [Header("Walk In")]
        [Tooltip("플레이어가 이동할 목표 위치/회전 Transform (빈 오브젝트로 마커 설치)")]
        [SerializeField] private Transform _playerStartPoint;
        [Tooltip("골렘이 이동할 목표 위치/회전 Transform (빈 오브젝트로 마커 설치)")]
        [SerializeField] private Transform _golemStartPoint;
        [Tooltip("이동에 걸리는 시간 (초)")]
        [SerializeField] private float _walkDuration = 1.5f;

        [Header("Characters")]
        [Tooltip("플레이어 Transform")]
        [SerializeField] private Transform _player;
        [Tooltip("골렘 Transform")]
        [SerializeField] private Transform _golem;
        [Tooltip("플레이어 Animator")]
        [SerializeField] private Animator _playerAnimator;
        [Tooltip("골렘 Animator")]
        [SerializeField] private Animator _golemAnimator;
        [Tooltip("플레이어 걷기 파라미터 이름 (Float, inputMag)")]
        [SerializeField] private string _playerWalkParam = "inputMagnitude";
        [Tooltip("골렘 걷기 파라미터 이름 (Bool, isWalking)")]
        [SerializeField] private string _golemWalkParam = "isWalking";

        [Header("Yarn Dialogue")]
        [Tooltip("Yarn 노드명 (DLG_002)")]
        [SerializeField] private string _dialogueStartNode = "DLG_002";
        [SerializeField] private ForestDialogueCommands _dialogueCommands;
        [SerializeField] private GameObject _dialogueCanvas;

        [Header("Event Channels")]
        [Tooltip("DialogueManager의 onDialogueCompletedEvent SO와 동일한 것 연결")]
        [SerializeField] private GameEvent _onDialogueCompletedEvent;

        [Header("Player")]
        [Tooltip("이동 제어할 PlayerController")]
        [SerializeField] private PlayerAnimation _playerAnimation;
        [SerializeField] private PlayerLocomotionInput _playerLocomotionInput;
        [SerializeField] private MonoBehaviour _playerControllerScript;

        [Header("Debug")]
        [SerializeField] private bool _debugSkipIntro = false;

        // 내부 상태
        private ForestEventState _state = ForestEventState.Idle;

        // =============================================
        // Unity 생명주기
        // =============================================

        private void Start()
        {
            ValidateComponents();
            _dialogueCommands?.Register(this);

            if (_debugSkipIntro)
                EnterDialogueState();
        }

        private void OnDestroy()
        {
            UnsubscribeAllDirectorEvents();
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);
        }

        // =============================================
        // 트리거 진입 (TriggerZone에서 호출)
        // =============================================

        public void OnPlayerEnterTrigger()
        {
            if (_state != ForestEventState.Idle) return;
            EnterWalkInState();
        }

        // =============================================
        // 상태 전환
        // =============================================

        private void EnterWalkInState()
        {
            ChangeState(ForestEventState.WalkIn);
            SetPlayerMovement(false);
            StartCoroutine(WalkInRoutine());
        }

        private IEnumerator WalkInRoutine()
        {
            // 걷기 애니메이션 ON
            SetWalkAnimation(true);

            // 시작 위치/회전 저장
            Vector3 playerStartPos = _player != null ? _player.position : Vector3.zero;
            Quaternion playerStartRot = _player != null ? _player.rotation : Quaternion.identity;
            Vector3 golemStartPos = _golem != null ? _golem.position : Vector3.zero;
            Quaternion golemStartRot = _golem != null ? _golem.rotation : Quaternion.identity;

            float elapsed = 0f;
            while (elapsed < _walkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _walkDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, t); // 부드럽게 가속/감속

                if (_player != null && _playerStartPoint != null)
                {
                    _player.position = Vector3.Lerp(playerStartPos, _playerStartPoint.position, smooth);
                    _player.rotation = Quaternion.Slerp(playerStartRot, _playerStartPoint.rotation, smooth);
                }

                if (_golem != null && _golemStartPoint != null)
                {
                    _golem.position = Vector3.Lerp(golemStartPos, _golemStartPoint.position, smooth);
                    _golem.rotation = Quaternion.Slerp(golemStartRot, _golemStartPoint.rotation, smooth);
                }

                yield return null;
            }

            // 목표 위치 정확히 스냅
            if (_player != null && _playerStartPoint != null)
            {
                _player.position = _playerStartPoint.position;
                _player.rotation = _playerStartPoint.rotation;
            }
            if (_golem != null && _golemStartPoint != null)
            {
                _golem.position = _golemStartPoint.position;
                _golem.rotation = _golemStartPoint.rotation;
            }

            // 걷기 애니메이션 OFF
            SetWalkAnimation(false);
            _playerAnimation?.ResetBlendInput();

            // 바로 Dialogue 시작
            EnterDialogueState();
        }

        private void EnterDialogueState()
        {
            ChangeState(ForestEventState.Dialogue);
            _dialogueCanvas?.SetActive(true);

            if (Managers.Dialogue == null)
            {
                Debug.LogError("[ForestEventController] Managers.Dialogue가 없습니다. ManagersBootstrap이 씬에 있는지 확인하세요.");
                return;
            }

            _onDialogueCompletedEvent?.Register(OnDialogueComplete);
            Managers.Dialogue.StartDialogue(_dialogueStartNode);
        }

        /// <summary>
        /// Yarn <<forest_choose_push>> → ForestDialogueCommands가 호출
        /// </summary>
        public void OnChoicePush()
        {
            if (_state != ForestEventState.Dialogue) return;
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);
            ChangeState(ForestEventState.PushTimeline);
            PlayChoiceTimeline(_pushDirector);
        }

        /// <summary>
        /// Yarn <<forest_choose_lift>> → ForestDialogueCommands가 호출
        /// </summary>
        public void OnChoiceLift()
        {
            if (_state != ForestEventState.Dialogue) return;
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);
            ChangeState(ForestEventState.LiftTimeline);
            PlayChoiceTimeline(_liftDirector);
        }

        private void PlayChoiceTimeline(PlayableDirector director)
        {
            if (director == null)
            {
                Debug.LogWarning("[ForestEventController] 선택 Timeline 없음 → 바로 Complete");
                EnterCompleteState();
                return;
            }

            director.stopped += OnChoiceTimelineStopped;
            director.Play();
        }

        /// <summary>
        /// Push / Lift Timeline 끝 Signal Receiver → 이 메서드 연결 (선택 사항).
        /// </summary>
        public void OnChoiceTimelineEnd()
        {
            PlayableDirector current = (_state == ForestEventState.PushTimeline)
                ? _pushDirector
                : _liftDirector;

            UnsubscribeDirector(current, OnChoiceTimelineStopped);
            current?.Stop();
            EnterCompleteState();
        }

        private void OnChoiceTimelineStopped(PlayableDirector director)
        {
            director.stopped -= OnChoiceTimelineStopped;
            EnterCompleteState();
        }

        private void OnDialogueComplete()
        {
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);
            if (_state == ForestEventState.Dialogue)
            {
                Debug.LogWarning("[ForestEventController] 선택 커맨드 없이 대화 종료 → Complete fallback");
                EnterCompleteState();
            }
        }

        private void EnterCompleteState()
        {
            if (_state == ForestEventState.Complete) return;
            ChangeState(ForestEventState.Complete);
            SetPlayerMovement(true);
            _dialogueCanvas?.SetActive(false);

            // TODO: Quest 완료 이벤트 발행 (한나님 QuestManager 연동 후)
            // Managers.Quest.CompleteObjective("...");

            Debug.Log("[ForestEventController] 이벤트 완료");
        }

        // =============================================
        // 유틸리티
        // =============================================

        private void SetWalkAnimation(bool isWalking)
        {
            // 플레이어: inputMag float (0 = 정지, 1 = 걷기)
            _playerAnimator?.SetFloat(_playerWalkParam, isWalking ? 1f : 0f);
            // 골렘: isWalking bool
            _golemAnimator?.SetBool(_golemWalkParam, isWalking);
        }

        private void ChangeState(ForestEventState newState)
        {
            if (_state == newState) return;
            Debug.Log($"[ForestEventController] State: {_state} → {newState}");
            _state = newState;
        }

        private void SetPlayerMovement(bool canMove)
        {
            if (_playerAnimation != null)
                _playerAnimation.enabled = canMove;
            if (_playerLocomotionInput != null)
                _playerLocomotionInput.enabled = canMove;
            if (_playerControllerScript != null)
                _playerControllerScript.enabled = canMove;
            Debug.Log($"[ForestEventController] PlayerMovement: {canMove}");
        }

        private void UnsubscribeDirector(PlayableDirector director, System.Action<PlayableDirector> handler)
        {
            if (director != null)
                director.stopped -= handler;
        }

        private void UnsubscribeAllDirectorEvents()
        {
            UnsubscribeDirector(_pushDirector, OnChoiceTimelineStopped);
            UnsubscribeDirector(_liftDirector, OnChoiceTimelineStopped);
        }

        private bool ValidateComponents()
        {
            bool valid = true;

            if (_onDialogueCompletedEvent == null)
            {
                Debug.LogError("[ForestEventController] onDialogueCompletedEvent SO가 연결되지 않았습니다!");
                valid = false;
            }
            if (_dialogueCommands == null)
            {
                Debug.LogError("[ForestEventController] ForestDialogueCommands가 연결되지 않았습니다!");
                valid = false;
            }
            if (_player == null)
                Debug.LogWarning("[ForestEventController] Player Transform 없음");
            if (_golem == null)
                Debug.LogWarning("[ForestEventController] Golem Transform 없음");
            if (_playerStartPoint == null)
                Debug.LogWarning("[ForestEventController] PlayerStartPoint 없음 → 플레이어 이동 스킵");
            if (_golemStartPoint == null)
                Debug.LogWarning("[ForestEventController] GolemStartPoint 없음 → 골렘 이동 스킵");
            if (_pushDirector == null)
                Debug.LogWarning("[ForestEventController] PushDirector 없음 (선택 사항)");
            if (_liftDirector == null)
                Debug.LogWarning("[ForestEventController] LiftDirector 없음 (선택 사항)");

            return valid;
        }
    }
}