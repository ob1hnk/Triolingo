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
    ///               ├─ 밀기 선택(<<forest_choose_push>>) → PushTimeline + Push Dialogue
    ///               └─ 들기 선택(<<forest_choose_lift>>) → LiftTimeline + Lift Dialogue
    ///                     └─ Timeline 종료 → Complete
    ///
    /// 비 연출:
    ///   Zone 2 진입 → OnPlayerEnterRainStart(): 비 서서히 시작 + 말풍선
    ///   Zone 4 진입 → OnPlayerEnterRainStop():  비 서서히 멈춤
    ///
    /// MQ-01 퀘스트 연동:
    ///   P02: EnterDialogueState() 직전
    ///   P03: Yarn 대화 완료 직후 (Timeline 시작 전)
    ///   P04: EnterCompleteState() (Timeline 완료)
    ///
    /// 씬 재진입 복원:
    ///   MQ-01-P04 완료 → 통나무(Log) 오브젝트를 비활성화 상태로 복원
    ///   Zone 2 통과 + Zone 4 미통과 → 비 파티클 즉시 활성화
    ///
    /// Inspector 세팅:
    ///   - Player Start Point: 플레이어가 이동할 목표 위치 Transform
    ///   - Golem Start Point:  골렘이 이동할 목표 위치 Transform
    ///   - Walk Duration:      이동에 걸리는 시간 (초)
    ///   - Rotate Duration:    도착 후 최종 회전 정렬에 걸리는 시간 (초)
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
        [Tooltip("도착 후 최종 회전 정렬에 걸리는 시간 (초)")]
        [SerializeField] private float _rotateDuration = 0.4f;

        [Header("Characters")]
        [Tooltip("플레이어 Transform")]
        [SerializeField] private Transform _player;
        [Tooltip("골렘 Transform")]
        [SerializeField] private Transform _golem;
        [Tooltip("골렘 NavMesh 이동 제어 컴포넌트")]
        [SerializeField] private GolemFollow _golemFollow;
        [Tooltip("플레이어 Animator")]
        [SerializeField] private Animator _playerAnimator;
        [Tooltip("골렘 Animator")]
        [SerializeField] private Animator _golemAnimator;
        [Tooltip("플레이어 걷기 파라미터 이름 (Float, inputMag)")]
        [SerializeField] private string _playerWalkParam = "inputMagnitude";
        [Tooltip("골렘 걷기 파라미터 이름 (Bool, isWalking)")]
        [SerializeField] private string _golemWalkParam = "isWalking";

        [Header("Yarn Dialogue")]
        [Tooltip("Zone 0 진입 시 실행할 Yarn 노드명 (DLG_001)\n이 대화에서 <<start_quest MQ-01>>을 호출한다.")]
        [SerializeField] private string _introDialogueNode = "DLG_001";

        [Tooltip("통나무 앞 Yarn 노드명 (DLG_002)")]
        [SerializeField] private string _dialogueStartNode = "DLG_002";
        [SerializeField] private ForestDialogueCommands _dialogueCommands;

        [Tooltip("Lift Timeline 중간 대사 Yarn 노드명")]
        [SerializeField] private string _liftMidDialogueNode = "DLG_002_LIFT_MID";
        [Tooltip("Push Timeline 중간 대사 Yarn 노드명")]
        [SerializeField] private string _pushMidDialogueNode = "DLG_002_PUSH_MID";

        [Header("Zone 4 - 비 멈춤 NPC")]
        [Tooltip("Zone 4 진입 시 표시할 NPC(할아버지) 말풍선\n플레이어가 말 걸 때까지 유지 (DLG_006 시작 시 Hide)")]
        [SerializeField] private FollowSpeechBubbleView _zone4NpcSpeechBubble;
        [Tooltip("Zone 4 NPC 말풍선 대사")]
        [SerializeField] private string _zone4NpcMessage = "에구구... 강물이 이렇게나 불어나다니... 이를 어쩐다";
        [Tooltip("Zone 4 진입 시 실행할 Yarn 노드명 (DLG_006)\n<<start_quest MQ-02>>를 포함한다.")]
        [SerializeField] private string _zone4DialogueNode = "DLG_006";

        [Header("Event Channels")]
        [Tooltip("DialogueManager의 onDialogueCompletedEvent SO와 동일한 것 연결")]
        [SerializeField] private GameEvent _onDialogueCompletedEvent;

        [Header("Player")]
        [Tooltip("이동 제어할 PlayerController")]
        [SerializeField] private PlayerAnimation _playerAnimation;
        [SerializeField] private PlayerLocomotionInput _playerLocomotionInput;
        [SerializeField] private MonoBehaviour _playerControllerScript;

        // ─────────────────────────────────────────────
        // 비 연출
        // ─────────────────────────────────────────────
        [Header("Rain")]
        [Tooltip("비 파티클 시스템 (Rain01_HighPerformance 프리펩 인스턴스)")]
        [SerializeField] private ParticleSystem _rainParticle;

        [Tooltip("비 최대 Emission Rate (Start는 0, 이 값까지 서서히 올라감)")]
        [SerializeField] private float _rainMaxEmissionRate = 50f;

        [Tooltip("비가 하나둘 내리기 시작 → 최대치까지 걸리는 시간 (초)")]
        [SerializeField] private float _rainFadeInDuration = 4f;

        [Tooltip("비가 잦아들다 멈추기까지 걸리는 시간 (초)")]
        [SerializeField] private float _rainFadeOutDuration = 5f;

        [Tooltip("비 시작 시 표시할 주인공 말풍선 (FollowSpeechBubbleView)")]
        [SerializeField] private FollowSpeechBubbleView _rainSpeechBubble;

        [Tooltip("말풍선 표시 시간 (초)")]
        [SerializeField] private float _speechBubbleDuration = 3f;
        [Tooltip("비 시작 후 말풍선 표시까지 딜레이 (초)")]
        [SerializeField] private float _speechBubbleDelay = 1.5f;

        // ─────────────────────────────────────────────
        // MQ-01 퀘스트 연동
        // ─────────────────────────────────────────────
        [Header("Quest (MQ-01)")]
        [Tooltip("ForestQuestController 연결")]
        [SerializeField] private ForestQuestController _questController;

        [Tooltip("씬 재진입 복원 대상: 통나무 오브젝트 Transform")]
        [SerializeField] private Transform _logTransform;

        [Tooltip("타임라인 실행 전 통나무 Position\n(씬 재진입 시 P04 미완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logPositionBefore;
        [Tooltip("타임라인 실행 전 통나무 Rotation (Euler)\n(씬 재진입 시 P04 미완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logRotationBefore;
        [Tooltip("타임라인 실행 전 통나무 LocalScale\n(씬 재진입 시 P04 미완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logScaleBefore = Vector3.one;

        [Tooltip("타임라인 실행 후 통나무 Position\n(씬 재진입 시 P04 완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logPositionAfter;
        [Tooltip("타임라인 실행 후 통나무 Rotation (Euler)\n(씬 재진입 시 P04 완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logRotationAfter;
        [Tooltip("타임라인 실행 후 통나무 LocalScale\n(씬 재진입 시 P04 완료면 이 값으로 복원)")]
        [SerializeField] private Vector3 _logScaleAfter = Vector3.one;

        // ─────────────────────────────────────────────

        [Header("Debug")]
        [SerializeField] private bool _debugSkipIntro = false;

        // 내부 상태
        private ForestEventState _state = ForestEventState.Idle;

        /// <summary>
        /// Timeline이 먼저 끝났지만 Yarn 대화가 아직 진행 중일 때 true.
        /// OnDialogueComplete에서 이 플래그를 확인해 Complete로 진입한다.
        /// </summary>
        private bool _pendingComplete = false;

        /// <summary>
        /// 현재 Timeline 중 Yarn 대화가 실행 중인지 추적.
        /// Signal로 대화 시작 시 true, OnDialogueComplete 시 false.
        /// </summary>
        private bool _midDialogueActive = false;

        // 씬 시작 시 골렘 초기 위치/회전 저장용
        private Vector3 _golemInitialPosition;
        private Quaternion _golemInitialRotation;

        // 비 코루틴 핸들 (중복 실행 방지)
        private Coroutine _rainCoroutine;

        // =============================================
        // Unity 생명주기
        // =============================================

        private void Awake()
        {
            // Awake에서 초기 위치 저장 (Start보다 먼저 → Timeline 평가 전)
            if (_golem != null)
            {
                _golemInitialPosition = _golem.position;
                _golemInitialRotation = _golem.rotation;
            }
        }

        private void Start()
        {
            ValidateComponents();
            _dialogueCommands?.Register(this);
            _dialogueCommands?.RegisterCommands();

            // Timeline 완전 정지 + 골렘 위치 강제 복원
            ResetTimeline(_liftDirector);
            ResetTimeline(_pushDirector);
            ForceResetGolemTransform();

            // 비 파티클 초기 상태: GameObject 자체를 비활성화
            if (_rainParticle != null)
                _rainParticle.gameObject.SetActive(false);

            // 말풍선 초기 비활성화
            _rainSpeechBubble?.Hide();

            // ── 씬 재진입 복원 ──
            RestoreSceneState();

            if (_debugSkipIntro)
                EnterDialogueState();
        }

        private void OnDestroy()
        {
            UnsubscribeAllDirectorEvents();
            _onDialogueCompletedEvent?.Unregister(OnIntroDialogueComplete);
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);
        }

        // =============================================
        // 씬 재진입 복원
        // =============================================

        /// <summary>
        /// 씬 재로드 시 퀘스트 진행 상태에 따라 오브젝트/이펙트 상태를 복원한다.
        /// </summary>
        private void RestoreSceneState()
        {
            if (_questController == null) return;

            bool p04Done = _questController.IsPhaseCompleted("MQ-01-P04");

            // 통나무 transform 복원: P04 완료 여부에 따라 after/before 값 적용
            if (_logTransform != null)
            {
                if (p04Done)
                {
                    _logTransform.position   = _logPositionAfter;
                    _logTransform.rotation   = Quaternion.Euler(_logRotationAfter);
                    _logTransform.localScale = _logScaleAfter;
                    Debug.Log("[ForestEventController] 씬 재진입: MQ-01-P04 완료 → 통나무 After transform 복원");
                }
                else
                {
                    _logTransform.position   = _logPositionBefore;
                    _logTransform.rotation   = Quaternion.Euler(_logRotationBefore);
                    _logTransform.localScale = _logScaleBefore;
                    Debug.Log("[ForestEventController] 씬 재진입: MQ-01-P04 미완료 → 통나무 Before transform 복원");
                }
            }

            // P04 완료 = 이벤트 끝, Complete 상태로 간주
            if (p04Done)
                _state = ForestEventState.Complete;

            // Zone 2 통과(P04 완료) + Zone 4 미통과(P05 미완료) → 비 즉시 활성화
            if (p04Done && !_questController.IsPhaseCompleted("MQ-01-P05"))
                RestoreRain();
        }

        /// <summary>
        /// 비 파티클을 즉시 최대 emission으로 활성화 (씬 재진입 시 복원)
        /// </summary>
        private void RestoreRain()
        {
            if (_rainParticle == null) return;

            _rainParticle.gameObject.SetActive(true);
            var emission = _rainParticle.emission;
            emission.rateOverTime = _rainMaxEmissionRate;
            _rainParticle.Play();
            Debug.Log("[ForestEventController] 씬 재진입: 비 상태 복원 (즉시 최대치)");
        }

        // =============================================
        // 트리거 진입 (TriggerZone에서 호출)
        // =============================================

        /// <summary>
        /// Zone 0: 스폰 지점 진입 → DLG_001 실행 (<<start_quest MQ-01>> 포함)
        /// MQ-01-P01 미완료 조건은 TriggerZone의 Blocked Phase ID로 처리.
        /// </summary>
        public void OnPlayerEnterDialogue()
        {
            if (Managers.Dialogue == null)
            {
                Debug.LogError("[ForestEventController] Managers.Dialogue가 없습니다.");
                return;
            }
            _onDialogueCompletedEvent?.Register(OnIntroDialogueComplete);
            StartCoroutine(StartDialogueNextFrame(_introDialogueNode));
            Debug.Log($"[ForestEventController] Zone 0 진입 → {_introDialogueNode} 시작");
        }

        private void OnIntroDialogueComplete()
        {
            Debug.Log("[ForestEventController] OnIntroDialogueComplete 호출됨 (몇 번 찍히는지 확인)");
            if (this == null) return;
            StartCoroutine(IntroDialogueCompleteNextFrame());
        }

        private IEnumerator IntroDialogueCompleteNextFrame()
        {
            yield return null;
            _onDialogueCompletedEvent?.Unregister(OnIntroDialogueComplete);
            Debug.Log("[ForestEventController] DLG_001 완료");
        }

        /// <summary>Zone 1: 메인 이벤트 (통나무) 시작</summary>
        public void OnPlayerEnterTrigger()
        {
            if (_state != ForestEventState.Idle) return;
            _golemFollow?.StopFollowing();
            EnterWalkInState();
        }

        /// <summary>
        /// Zone 2: 비 시작 연출
        /// 파티클 Emission Rate를 0에서 서서히 올리고 말풍선 표시
        /// </summary>
        public void OnPlayerEnterRainStart()
        {
            if (_rainParticle == null)
            {
                Debug.LogWarning("[ForestEventController] Rain Particle이 연결되지 않았습니다!");
                return;
            }

            if (_rainCoroutine != null)
                StopCoroutine(_rainCoroutine);

            _rainCoroutine = StartCoroutine(RainFadeInRoutine());
        }

        /// <summary>
        /// Zone 4: 비 멈춤 연출 + NPC 말풍선 표시
        /// Emission Rate를 서서히 0으로 줄이고 남은 파티클이 사라지면 Stop.
        /// NPC 말풍선은 플레이어가 말을 걸 때까지 유지 (DLG_006 시작 시 Hide).
        /// </summary>
        public void OnPlayerEnterRainStop()
        {
            if (_rainParticle == null) return;

            if (_rainCoroutine != null)
                StopCoroutine(_rainCoroutine);

            _rainCoroutine = StartCoroutine(RainFadeOutRoutine());

            // NPC(할아버지) 말풍선 표시 — 플레이어가 말 걸 때까지 유지
            if (!string.IsNullOrEmpty(_zone4NpcMessage))
                _zone4NpcSpeechBubble?.Show(_zone4NpcMessage);
        }

        /// <summary>
        /// Zone 4 NPC 말풍선 숨김.
        /// DLG_006 시작 시 Yarn 커맨드 또는 UnityEvent에서 호출.
        /// </summary>
        public void HideZone4NpcSpeechBubble()
        {
            _zone4NpcSpeechBubble?.Hide();
        }

        /// <summary>
        /// Zone 4: DLG_006 대화 시작.
        /// NPC 말풍선을 숨기고 대화 캔버스를 열어 DLG_006을 실행한다.
        /// DLG_006 내부에서 <<start_quest MQ-02>>가 호출된다.
        /// Zone 4 TriggerZone의 OnPlayerEnter에 OnPlayerEnterRainStop과 함께 연결.
        /// </summary>
        public void OnPlayerEnterZone4Dialogue()
        {
            if (Managers.Dialogue == null)
            {
                Debug.LogError("[ForestEventController] Managers.Dialogue가 없습니다.");
                return;
            }

            HideZone4NpcSpeechBubble();
            _onDialogueCompletedEvent?.Register(OnZone4DialogueComplete);
            StartCoroutine(StartDialogueNextFrame(_zone4DialogueNode));
            Debug.Log($"[ForestEventController] Zone 4 진입 → {_zone4DialogueNode} 시작");
        }

        private void OnZone4DialogueComplete()
        {
            if (this == null) return;
            StartCoroutine(Zone4DialogueCompleteNextFrame());
        }

        private IEnumerator Zone4DialogueCompleteNextFrame()
        {
            yield return null;
            _onDialogueCompletedEvent?.Unregister(OnZone4DialogueComplete);
            Debug.Log("[ForestEventController] DLG_006 완료");
        }

        // =============================================
        // 비 연출 코루틴
        // =============================================

        private IEnumerator RainFadeInRoutine()
        {
            Debug.Log("[ForestEventController] 비 시작 연출");

            // GameObject 활성화 후 Emission Rate=0 상태로 Play
            _rainParticle.gameObject.SetActive(true);
            var emission = _rainParticle.emission;
            emission.rateOverTime = 0f;
            _rainParticle.Play();

            // 말풍선 딜레이 후 표시
            if (_rainSpeechBubble != null)
                StartCoroutine(ShowSpeechBubbleAfter(_speechBubbleDelay));

            // Emission Rate 0 → _rainMaxEmissionRate 로 서서히 증가
            float elapsed = 0f;
            while (elapsed < _rainFadeInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _rainFadeInDuration);
                // EaseIn: 처음엔 하나둘, 나중엔 쏟아지는 느낌
                float eased = t * t;
                emission.rateOverTime = Mathf.Lerp(0f, _rainMaxEmissionRate, eased);
                yield return null;
            }

            emission.rateOverTime = _rainMaxEmissionRate;
            _rainCoroutine = null;
            Debug.Log("[ForestEventController] 비 최대치 도달");
        }

        private IEnumerator RainFadeOutRoutine()
        {
            Debug.Log("[ForestEventController] 비 잦아드는 연출 시작");

            var emission = _rainParticle.emission;
            float startRate = emission.rateOverTime.constant;

            // Emission Rate를 현재값에서 0으로 서서히 감소
            float elapsed = 0f;
            while (elapsed < _rainFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _rainFadeOutDuration);
                // EaseOut: 처음엔 빠르게 줄고, 마지막엔 서서히 멈추는 느낌
                float eased = 1f - (1f - t) * (1f - t);
                emission.rateOverTime = Mathf.Lerp(startRate, 0f, eased);
                yield return null;
            }

            emission.rateOverTime = 0f;

            // 남은 파티클(공중에 있는 것들)이 수명대로 사라질 때까지 대기
            float lifetime = _rainParticle.main.startLifetime.constant;
            yield return new WaitForSeconds(lifetime);

            _rainParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _rainParticle.gameObject.SetActive(false);
            _rainCoroutine = null;
            Debug.Log("[ForestEventController] 비 완전히 멈춤");
        }

        private IEnumerator ShowSpeechBubbleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _rainSpeechBubble.Show("앗 비 온다! 내 짐!");
            StartCoroutine(HideSpeechBubbleAfter(_speechBubbleDuration));
        }

        private IEnumerator HideSpeechBubbleAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _rainSpeechBubble?.Hide();
        }

        private IEnumerator StartDialogueNextFrame(string node)
        {
            yield return null;
            Managers.Dialogue.StartDialogue(node);
        }

        // =============================================
        // 상태 전환
        // =============================================

        private void EnterWalkInState()
        {
            Debug.Log("[ForestEventController] WalkIn 시작");
            ChangeState(ForestEventState.WalkIn);
            SetPlayerMovement(false);
            StartCoroutine(WalkInRoutine());
        }

        private IEnumerator WalkInRoutine()
        {
            // 골렘: MoveToPoint로 golemStartPoint까지 이동
            bool golemArrived = _golemFollow == null || _golemStartPoint == null;
            if (_golemFollow != null && _golemStartPoint != null)
                _golemFollow.MoveToPoint(_golemStartPoint, () => golemArrived = true);

            // 1프레임 대기: Animator 파라미터 → Transition 조건 반영 대기
            yield return null;

            // ── Phase 1: 이동 방향을 바라보며 목적지까지 이동 ──
            if (_player != null && _playerStartPoint != null)
            {
                Vector3 startPos = _player.position;
                Vector3 endPos   = _playerStartPoint.position;

                // 이동 방향 회전 계산 (XZ 평면 기준)
                Vector3 moveDir = endPos - startPos;
                moveDir.y = 0f;
                Quaternion moveRotation = moveDir.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(moveDir)
                    : _player.rotation;

                // 시작 시 이동 방향으로 즉시(또는 빠르게) 회전 정렬
                _player.rotation = moveRotation;

                // WASD와 동일한 locomotionBlendSpeed로 서서히 올려 자연스러운 가속감 연출
                float currentMag = 0f;
                float currentY   = 0f;

                float elapsed = 0f;
                while (elapsed < _walkDuration)
                {
                    elapsed += Time.deltaTime;
                    float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _walkDuration));
                    _player.position = Vector3.Lerp(startPos, endPos, smooth);
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

                // ── Phase 2: 도착 후 최종 rotation으로 보간 ──
                Quaternion finalRot = _playerStartPoint.rotation;
                elapsed = 0f;
                while (elapsed < _rotateDuration)
                {
                    elapsed += Time.deltaTime;
                    float smooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _rotateDuration));
                    _player.rotation = Quaternion.Slerp(moveRotation, finalRot, smooth);
                    yield return null;
                }

                _player.rotation = finalRot;
            }

            // 골렘 도착 대기
            while (!golemArrived)
                yield return null;

            // 걷기 애니메이션 OFF
            _playerAnimator?.SetFloat(_playerWalkParam, 0f);
            _playerAnimator?.SetFloat("inputY", 0f);
            _playerAnimation?.ResetBlendInput();

            // 바로 Dialogue 시작
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
            StartCoroutine(StartDialogueNextFrame(_dialogueStartNode));
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

            _golemFollow?.DisableAgent();
            director.stopped += OnChoiceTimelineStopped;
            director.Play();
        }

        /// <summary>
        /// Lift Timeline Signal → 중간 대사 트리거
        /// Signal Receiver에서 이 메서드 연결
        /// </summary>
        public void OnLiftDialogueTrigger()
        {
            if (_state != ForestEventState.LiftTimeline) return;
            StartMidDialogue(_liftMidDialogueNode);
        }

        /// <summary>
        /// Push Timeline Signal → 중간 대사 트리거
        /// Signal Receiver에서 이 메서드 연결
        /// </summary>
        public void OnPushDialogueTrigger()
        {
            if (_state != ForestEventState.PushTimeline) return;
            StartMidDialogue(_pushMidDialogueNode);
        }

        /// <summary>
        /// Timeline 중간 Yarn 대화 시작.
        /// _midDialogueActive = true로 마킹하여, Timeline이 먼저 끝나도
        /// Complete 전환을 대화 완료 후로 미룬다.
        /// </summary>
        private void StartMidDialogue(string nodeName)
        {
            _midDialogueActive = true;
            _pendingComplete = false;
            _onDialogueCompletedEvent?.Register(OnDialogueComplete);
            StartCoroutine(StartDialogueNextFrame(nodeName));
            Debug.Log($"[ForestEventController] 중간 대화 시작: {nodeName}");
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
            TryComplete();
        }

        private void OnChoiceTimelineStopped(PlayableDirector director)
        {
            director.stopped -= OnChoiceTimelineStopped;
            TryComplete();
        }

        /// <summary>
        /// Yarn 대화 완료 콜백.
        /// - 초기 선택지 대화(Dialogue 상태): 선택 커맨드 없이 끝난 경우 fallback Complete.
        /// - Timeline 중간 대화(_midDialogueActive): 대화 완료 처리 후
        ///   Timeline이 이미 끝났으면(_pendingComplete) 즉시 Complete 진입.
        /// </summary>
        private void OnDialogueComplete()
        {
            // Raise() 도중 Unregister하면 GameEvent 내부 리스트 인덱스가 깨지므로
            // 1프레임 뒤에 처리
            if (this == null) return;
            StartCoroutine(DialogueCompleteNextFrame());
        }

        private IEnumerator DialogueCompleteNextFrame()
        {
            yield return null;
            _onDialogueCompletedEvent?.Unregister(OnDialogueComplete);

            if (_midDialogueActive)
            {
                _midDialogueActive = false;
                Debug.Log("[ForestEventController] 중간 대화 완료");

                if (_pendingComplete)
                {
                    Debug.Log("[ForestEventController] Timeline이 이미 종료됨 → Complete 진입");
                    _pendingComplete = false;
                    EnterCompleteState();
                }
                // pendingComplete가 false면 Timeline이 아직 재생 중이므로 대기
                yield break;
            }

            // 초기 선택지 대화가 선택 커맨드 없이 끝난 경우 fallback
            if (_state == ForestEventState.Dialogue)
            {
                Debug.LogWarning("[ForestEventController] 선택 커맨드 없이 대화 종료 → Complete fallback");
                EnterCompleteState();
            }
        }

        /// <summary>
        /// Timeline 종료 시 Complete 진입 시도.
        /// 중간 대화가 아직 진행 중이면 _pendingComplete = true로 마킹하고 대기.
        /// </summary>
        private void TryComplete()
        {
            if (_midDialogueActive)
            {
                _pendingComplete = true;
                Debug.Log("[ForestEventController] Timeline 종료, 대화 진행 중 → Complete 대기");
                return;
            }

            EnterCompleteState();
        }

        private void EnterCompleteState()
        {
            if (_state == ForestEventState.Complete) return;
            ChangeState(ForestEventState.Complete);

            // CharacterController 리셋 후 이동 복원
            (_playerControllerScript as PlayerController)?.ResetVelocity();
            SetPlayerMovement(true);

            // 골렘 NavMesh Agent 재활성화 + 추적 재개
            _golemFollow?.EnableAgent();
            _golemFollow?.StartFollowingSmooth();

            // 타임라인 후 통나무 transform → After 값으로 고정
            if (_logTransform != null)
            {
                _logTransform.position   = _logPositionAfter;
                _logTransform.rotation   = Quaternion.Euler(_logRotationAfter);
                _logTransform.localScale = _logScaleAfter;
            }

            // MQ-01-P04: 상호작용 연출 완료 (Timeline 끝)
            _questController?.CompleteByPhaseID("MQ-01-P04");

            Debug.Log("[ForestEventController] 이벤트 완료");
        }

        // =============================================
        // 유틸리티
        // =============================================

        private IEnumerator PostCompleteReset()
        {
            yield return null;  // 1프레임 대기
            _playerAnimation?.ResetBlendInput();
            (_playerControllerScript as PlayerController)?.ResetVelocity();
        }

        /// <summary>
        /// Timeline을 완전히 정지시키고 바인딩 영향 제거
        /// Stop()만으로는 Wrap Mode = Hold일 때 마지막 프레임 값이 남을 수 있으므로
        /// time을 0으로 리셋하고 Evaluate() 후 Stop()하여 초기 상태로 되돌린다.
        /// </summary>
        private void ResetTimeline(PlayableDirector director)
        {
            if (director == null) return;

            director.time = 0;
            director.Evaluate();
            director.Stop();
        }

        /// <summary>
        /// Awake에서 저장한 초기 위치/회전으로 골렘을 강제 복원
        /// Timeline 바인딩이 Transform을 오염시킨 경우의 안전장치
        /// </summary>
        private void ForceResetGolemTransform()
        {
            if (_golem == null) return;

            _golem.position = _golemInitialPosition;
            _golem.rotation = _golemInitialRotation;
            Debug.Log($"[ForestEventController] 골렘 초기 위치 복원: {_golemInitialPosition}");
        }

        private void SetWalkAnimation(bool isWalking)
        {
            // 플레이어: inputMag float (0 = 정지, 1 = 걷기)
            _playerAnimator?.SetFloat(_playerWalkParam, isWalking ? 1f : 0f);

            // 골렘: isWalking bool
            if (_golemAnimator != null)
            {
                _golemAnimator.SetBool(_golemWalkParam, isWalking);
                Debug.Log($"[ForestEventController] 골렘 {_golemWalkParam} = {isWalking}");
            }
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
            if (_questController == null)
                Debug.LogWarning("[ForestEventController] QuestController 없음 → MQ-01 퀘스트 연동 스킵");
            if (_player == null)
                Debug.LogWarning("[ForestEventController] Player Transform 없음");
            if (_golem == null)
                Debug.LogWarning("[ForestEventController] Golem Transform 없음");
            if (_golemFollow == null)
                Debug.LogWarning("[ForestEventController] GolemFollow 없음 → 골렘 NavMesh 이동 스킵");
            if (_playerStartPoint == null)
                Debug.LogWarning("[ForestEventController] PlayerStartPoint 없음 → 플레이어 이동 스킵");
            if (_golemStartPoint == null)
                Debug.LogWarning("[ForestEventController] GolemStartPoint 없음 → 골렘 이동 스킵");
            if (_pushDirector == null)
                Debug.LogWarning("[ForestEventController] PushDirector 없음 (선택 사항)");
            if (_liftDirector == null)
                Debug.LogWarning("[ForestEventController] LiftDirector 없음 (선택 사항)");
            if (_rainParticle == null)
                Debug.LogWarning("[ForestEventController] RainParticle 없음 → 비 연출 스킵");

            return valid;
        }
    }
}