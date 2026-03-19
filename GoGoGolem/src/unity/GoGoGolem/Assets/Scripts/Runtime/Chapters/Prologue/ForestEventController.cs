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
        WalkIn,        // 인트로 Timeline 재생 중
        Dialogue,      // Yarn 대화 중 (선택지 표시)
        PushTimeline,  // 밀기 Timeline 재생 중
        LiftTimeline,  // 들기 Timeline 재생 중
        Complete       // 이벤트 종료
    }

    /// <summary>
    /// Forest 씬 전체 흐름 제어 (Glue 코드 30%)
    ///
    /// 흐름:
    ///   Idle
    ///    └─ 트리거 진입 → WalkIn (인트로 Timeline)
    ///         └─ Signal → OnIntroTimelineEnd() → Dialogue (Yarn 실행)
    ///               ├─ 밀기 선택(<<forest_choose_push>>) → PushTimeline
    ///               └─ 들기 선택(<<forest_choose_lift>>) → LiftTimeline
    ///                     └─ Timeline 종료 → Complete
    ///
    /// Timeline Signal 연결:
    ///   - 인트로 Timeline 끝 Signal Receiver → OnIntroTimelineEnd()
    ///   - Push/Lift Timeline 끝 Signal Receiver → OnChoiceTimelineEnd()
    ///
    /// [PlayerController 연동 - TODO]
    ///   WalkIn 진입 시 canMove = false
    ///   Complete 진입 시 canMove = true
    /// </summary>
    public class ForestEventController : MonoBehaviour
    {
        [Header("Timelines")]
        [Tooltip("플레이어가 통나무에 다가가는 인트로 Timeline")]
        [SerializeField] private PlayableDirector _introDirector;
        [Tooltip("골렘이 통나무를 미는 Timeline")]
        [SerializeField] private PlayableDirector _pushDirector;
        [Tooltip("골렘이 통나무를 드는 Timeline")]
        [SerializeField] private PlayableDirector _liftDirector;

        [Header("Yarn Dialogue")]
        [Tooltip("Yarn 노드명 (DLG_002)")]
        [SerializeField] private string _dialogueStartNode = "DLG_002";
        [SerializeField] private ForestDialogueCommands _dialogueCommands;

        [Header("Event Channels")]
        [Tooltip("DialogueManager의 onDialogueCompletedEvent SO와 동일한 것 연결")]
        [SerializeField] private GameEvent _onDialogueCompletedEvent;

        [Header("Player")]
        [Tooltip("이동 제어할 PlayerController. TODO: 연동 완료 후 활성화")]
        [SerializeField] private MonoBehaviour _playerController; // TODO: PlayerController 타입으로 교체

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

        /// <summary>
        /// TriggerZone 오브젝트의 OnTriggerEnter에서 이 메서드를 연결.
        /// </summary>
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

            if (_introDirector == null)
            {
                Debug.LogWarning("[ForestEventController] IntroDirector 없음 → 바로 Dialogue로 진행");
                EnterDialogueState();
                return;
            }

            _introDirector.stopped += OnIntroTimelineStopped;
            _introDirector.Play();
        }

        /// <summary>
        /// 인트로 Timeline Signal Receiver → 이 메서드 연결.
        /// Timeline이 끝나기 전 특정 타이밍에 대화를 시작하고 싶을 때 사용.
        /// </summary>
        public void OnIntroTimelineEnd()
        {
            if (_state != ForestEventState.WalkIn) return;
            UnsubscribeDirector(_introDirector, OnIntroTimelineStopped);
            _introDirector?.Stop();
            EnterDialogueState();
        }

        private void OnIntroTimelineStopped(PlayableDirector _)
        {
            // Signal 없이 Timeline이 자연 종료된 경우 fallback
            if (_state != ForestEventState.WalkIn) return;
            UnsubscribeDirector(_introDirector, OnIntroTimelineStopped);
            EnterDialogueState();
        }

        private void EnterDialogueState()
        {
            ChangeState(ForestEventState.Dialogue);

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
        /// Push / Lift Timeline 끝 Signal Receiver → 이 메서드 연결.
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
            // 선택지 커맨드가 먼저 상태를 바꿔놓음.
            // 커맨드 없이 대화가 끝난 경우 (테스트 등) fallback.
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

            // TODO: Quest 완료 이벤트 발행 (한나님 QuestManager 연동 후)
            // Managers.Quest.CompleteObjective("...");

            Debug.Log("[ForestEventController] 이벤트 완료");
        }

        // =============================================
        // 유틸리티
        // =============================================

        private void ChangeState(ForestEventState newState)
        {
            if (_state == newState) return;
            Debug.Log($"[ForestEventController] State: {_state} → {newState}");
            _state = newState;
        }

        private void SetPlayerMovement(bool canMove)
        {
            // TODO: PlayerController 타입 확정 후 직접 호출로 교체
            // _playerController.canMove = canMove;
            Debug.Log($"[ForestEventController] PlayerMovement: {canMove}");
        }

        private void UnsubscribeDirector(PlayableDirector director, System.Action<PlayableDirector> handler)
        {
            if (director != null)
                director.stopped -= handler;
        }

        private void UnsubscribeAllDirectorEvents()
        {
            UnsubscribeDirector(_introDirector, OnIntroTimelineStopped);
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
            if (_introDirector == null)
                Debug.LogWarning("[ForestEventController] IntroDirector 없음 (선택 사항)");
            if (_pushDirector == null)
                Debug.LogWarning("[ForestEventController] PushDirector 없음 (선택 사항)");
            if (_liftDirector == null)
                Debug.LogWarning("[ForestEventController] LiftDirector 없음 (선택 사항)");

            return valid;
        }
    }
}