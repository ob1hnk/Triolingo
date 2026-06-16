using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 씬 상태
  /// </summary>
  public enum GestureSceneState
  {
    Entry,    // 진입 (초기화)
    Tutorial, // 튜토리얼 타임라인
    Playing,  // 플레이 중 (제스처 인식)
    Paused,   // 실패 타임아웃 팝업 표시 중 (인식 일시정지)
    Success,  // 성공 (연출 준비)
    Exit      // 종료
  }

  /// <summary>
  /// 제스처 씬 전체 흐름 제어 Controller
  ///
  /// - Entry
  ///     ├─ OBJ-02 → Tutorial → Playing(3초) → Success(Director1) → Exit
  ///     └─ OBJ-04            → Playing(3초) → Success(Director2) → Exit
  /// 
  /// [튜토리얼 재시청]
  /// Playing 중 왼쪽 위 버튼 → RewatchTutorial() → Tutorial → Playing
  /// 
  /// [Quest 연동]
  /// - _requestCompletePhaseEvent SO 연결 필요
  /// </summary>
  public class GestureSceneController : MonoBehaviour
  {
    [Header("Scene Configuration")]
    [SerializeField] private GestureSceneConfig _sceneConfig;

    [Header("Core Components")]
    [SerializeField] private GestureDetector _gestureDetector;

    [Header("UI - Play")]
    [SerializeField] private UI.GesturePlayView _gesturePlayView;

    [Header("Tutorial")]
    [Tooltip("OBJ-02 전용 튜토리얼 Timeline. null이면 튜토리얼 없이 바로 Playing.")]
    [SerializeField] private PlayableDirector _tutorialDirector;

    [Header("Tutorial UI Buttons")]
    [Tooltip("튜토리얼 건너뛰기 버튼 (Tutorial Canvas 내부, 타임라인이 Canvas 활성화 관리)")]
    [SerializeField] private Button _skipButton;
    [Tooltip("튜토리얼 재시청 버튼 (Main Canvas, Playing 상태에서만 표시)")]
    [SerializeField] private Button _rewatchButton;

    [Header("Success")]
    [Tooltip("OBJ-02(NoFly) 성공 연출 Timeline.")]
    [SerializeField] private PlayableDirector _successDirector1;
    [Tooltip("OBJ-04(Fly) 성공 연출 Timeline.")]
    [SerializeField] private PlayableDirector _successDirector2;

    [Header("Settings")]
    [SerializeField] private float _debounceDuration = 0.2f; // UI 깜빡임 방지
    [SerializeField] private float _requiredHoldDuration = 3f; // 제스처 유지 시간
    [Tooltip("진행 바가 나타나기 시작하는 홀드 시간 (초)")]
    [SerializeField] private float _progressShowThreshold = 2f;

    // ========== 실패 시 건너뛰기 ==========
    [Header("Fail Skip - Timeout")]
    [Tooltip("A: 손이 이 시간(초) 이상 안 보이면 위치 재조정 팝업")]
    [SerializeField] private float _noHandTimeout = 5f;
    [Tooltip("B: 양손은 보이지만 이 시간(초) 동안 한 번도 유지 성공 못하면 넘어가기 팝업")]
    [SerializeField] private float _holdFailTimeout = 15f;

    [Header("Fail Skip - UI")]
    [Tooltip("설정 패널 Presenter. 비우면 자동 탐색. 설정창이 열려 있는 동안 타임아웃을 중단한다.")]
    [SerializeField] private SettingsPresenter _settingsPresenter;
    [Tooltip("반투명 실패 안내 오버레이 (기본 비활성)")]
    [SerializeField] private GameObject _failOverlay;
    [Tooltip("실패 이유 텍스트")]
    [SerializeField] private TMP_Text _failReasonText;
    [Tooltip("다시해보기 버튼 (인식 재개)")]
    [SerializeField] private Button _retryButton;
    [Tooltip("넘어가기 버튼 (성공 처리) — B(유지 실패)에서만 표시")]
    [SerializeField] private Button _failSkipButton;

    [Header("Fail Skip - Messages")]
    [TextArea] [SerializeField] private string _msgNoHands         = "양손이 카메라 화면 안에 잘 보이도록 위치를 조정해 주세요.";
    [TextArea] [SerializeField] private string _msgPoseMissing     = "몸 전체가 보이도록 카메라에서 조금 물러나 주세요.";
    [TextArea] [SerializeField] private string _msgHandMissing     = "손이 화면 밖으로 자꾸 벗어나고 있어요. 손을 화면 안에 유지해 주세요.";
    [TextArea] [SerializeField] private string _msgPalmsNotForward = "손바닥이 카메라를 향하도록 펴 주세요.";
    [TextArea] [SerializeField] private string _msgHandsNotOpposite= "두 손을 마주보게 모아 주세요.";
    [TextArea] [SerializeField] private string _msgWristsTooFar    = "두 손을 더 가까이 모아 주세요.";
    [TextArea] [SerializeField] private string _msgFingersNotOpen  = "손가락을 쫙 펴 주세요.";
    [TextArea] [SerializeField] private string _msgNotRising       = "두 팔을 위로 쭉 들어올려 주세요.";
    [TextArea] [SerializeField] private string _msgGeneric         = "안내된 자세를 천천히 따라 해 보세요.";

    // Quest 연동 (읽기용)
    [Header("Quest - Objective IDs")]
    [SerializeField] private string _objectiveID_NoFly = "MQ-02-OBJ-02";
    [SerializeField] private string _objectiveID_Fly   = "MQ-02-OBJ-04";

    // Quest 연동 (쓰기용 - SO 이벤트 버스)
    [Header("Quest - Write")]
    [Tooltip("QuestManager의 requestCompletePhaseEvent와 동일한 SO")]
    [SerializeField] private CompletePhaseGameEvent _requestCompletePhaseEvent;
    [Tooltip("InventoryManager가 구독하는 RequestAcquireItem SO — P04 진입 시 스킬 지급")]
    [SerializeField] private StringGameEvent _requestAcquireItemEvent;
    [Tooltip("P04(NoFly 진입) 완료 시 지급할 스킬 ID")]
    [SerializeField] private string _skillID_NoFlyEntry = "SKILL-001";
    [SerializeField] private string _questID      = "MQ-02";
    [SerializeField] private string _phaseID_NoFly = "MQ-02-P05";
    [SerializeField] private string _phaseID_Fly   = "MQ-02-P09";

    [Header("Quest - Entry Phase (제스처 인식 진입 시 완료)")]
    [SerializeField] private string _entryPhaseID_NoFly = "MQ-02-P04";
    [SerializeField] private string _entryPhaseID_Fly   = "MQ-02-P08";

    // 씬 오브젝트
    [Header("Scene Objects (Quest-Driven)")]
    [SerializeField] private GameObject _flyItem;    // OBJ-04에서 활성화
    [SerializeField] private GameObject _noFlyItem;  // OBJ-02에서 활성화
    
    // 테스트용 ─────────────────────────────────────────
    [Header("--- DEBUG (테스트용) ---")]
    [Tooltip("체크하면 Managers.Quest 무시하고 아래 설정으로 강제 적용")]
    [SerializeField] private bool _debugOverride = false;
    [Tooltip("true = OBJ-04(Fly), false = OBJ-02(NoFly)")]
    [SerializeField] private bool _debugIsFly = false;

    // 내부 상태
    private UI.GesturePlayPresenter _gesturePlayPresenter;
    private GestureSceneState _state = GestureSceneState.Entry;
    private GestureType _targetGesture;
    private string _currentObjectiveID;

    private void Start()
    {
      SetupButtons();
      HideFailOverlay();

      // 설정창 가시성으로 타임아웃을 게이트 (열림 방식/씬 단독 실행과 무관하게 동작)
      if (_settingsPresenter == null)
        _settingsPresenter = FindFirstObjectByType<SettingsPresenter>(FindObjectsInactive.Include);

      EnterEntryState();
    }

    /// <summary>설정창 등으로 인식 타임아웃을 멈춰야 하는 상태인가</summary>
    private bool IsRecognitionBlocked()
    {
      if (_settingsPresenter != null && _settingsPresenter.IsVisible)
        return true;
      if (GameStateManager.Instance != null && !GameStateManager.Instance.IsInState(GameState.Gameplay))
        return true;
      return false;
    }
    
    private void Update()
    {
      // 설정창 등이 떠 있는 동안엔 타임아웃 타이머만 중단
      // (Detector는 계속 동작 → 웹캠 미리보기 유지. 오버레이가 설정창 뒤에 뜨는 문제 방지)
      if (_state == GestureSceneState.Playing && _gesturePlayPresenter != null)
      {
        _gesturePlayPresenter.SetTimersSuspended(IsRecognitionBlocked());
      }

      // DEBUG: ESC 키로 강제 종료
      if (_debugOverride && Input.GetKeyDown(KeyCode.Escape) && _state != GestureSceneState.Exit)
      {
        ExitScene();
      }
      // DEBUG: Enter 키로 강제 성공
      if (_debugOverride && Input.GetKeyDown(KeyCode.Return) && _state == GestureSceneState.Playing)
        OnGestureSuccess(_targetGesture);
    }
    
    private void OnDestroy()
    {
      CleanupButtons();
      UnsubscribeTutorialEvents();
      UnsubscribeSuccessEvents();
      _gesturePlayPresenter?.Cleanup();
      GestureSceneEvents.ClearAllSubscribers();
    }
    
    // ========== 버튼 설정 ==========

    private void SetupButtons()
    {
      if (_skipButton != null)
        _skipButton.onClick.AddListener(SkipTutorial);

      if (_rewatchButton != null)
        _rewatchButton.onClick.AddListener(RewatchTutorial);

      if (_retryButton != null)
        _retryButton.onClick.AddListener(OnRetryClicked);

      if (_failSkipButton != null)
        _failSkipButton.onClick.AddListener(OnFailSkipClicked);
    }

    private void CleanupButtons()
    {
      if (_skipButton != null)
        _skipButton.onClick.RemoveListener(SkipTutorial);

      if (_rewatchButton != null)
        _rewatchButton.onClick.RemoveListener(RewatchTutorial);

      if (_retryButton != null)
        _retryButton.onClick.RemoveListener(OnRetryClicked);

      if (_failSkipButton != null)
        _failSkipButton.onClick.RemoveListener(OnFailSkipClicked);
    }

    // ========== 상태 전환 메서드 ==========
    
    private void EnterEntryState()
    {
      ChangeState(GestureSceneState.Entry);
      
      // Config 검증
      if (!ValidateComponents())
      {
        Debug.LogError("[GestureSceneController] Component validation failed!");
        return;
      }

      // Quest 기반 씬 오브젝트 초기화
      SetupSceneObjectsByQuest();
      
      // Config에서 타겟 제스처 로드
      _targetGesture = _sceneConfig != null ? _sceneConfig.targetGesture : GestureType.Wind;

      // 제스처 인식 진입 phase 완료 (P04 또는 P08)
      NotifyEntryPhaseComplete();

      // OBJ-02일 때만 튜토리얼 먼저 실행
      bool needsTutorial =  _tutorialDirector != null && _currentObjectiveID == _objectiveID_NoFly;

      if (needsTutorial)
        EnterTutorialState();
      else
        EnterPlayingState();
    }

    /// <summary>
    /// Quest 세팅
    /// </summary>
    private void SetupSceneObjectsByQuest()
    {

      // ── 테스트 모드: Inspector에서 직접 제어 ──
      if (_debugOverride)
      {
        string objectiveID = _debugIsFly ? _objectiveID_Fly : _objectiveID_NoFly;
        ApplyObjectiveSetup(objectiveID);
        Debug.LogWarning($"[GestureSceneController] DEBUG OVERRIDE 활성 → {objectiveID}");
        return;
      }

      // ── 실제 Quest 연동 (한나님 작업 완료 후 사용) ──
      if (Managers.Quest == null)
      {
        Debug.LogWarning("[GestureSceneController] Managers.Quest가 없습니다. NoFly 기본값으로 진행합니다.");
        ApplyObjectiveSetup(_objectiveID_NoFly);
        return;
      }

      // OBJ-02가 완료됐으면 OBJ-04로, 아니면 OBJ-02로
      if (Managers.Quest.IsObjectiveCompleted(_questID, _objectiveID_NoFly))
        ApplyObjectiveSetup(_objectiveID_Fly);
      else
        ApplyObjectiveSetup(_objectiveID_NoFly);
    }

    private void ApplyObjectiveSetup(string objectiveID)
    {
      _currentObjectiveID = objectiveID;
      bool isFly = (objectiveID == _objectiveID_Fly);
      if (_flyItem != null)   _flyItem.SetActive(isFly);
      if (_noFlyItem != null) _noFlyItem.SetActive(!isFly);
      Debug.Log($"[GestureSceneController] Objective={objectiveID} → FlyItem={isFly}, NoFlyItem={!isFly}");
    }

    /// <summary>
    /// Tutorial
    /// </summary>
    private void EnterTutorialState()
    {
      ChangeState(GestureSceneState.Tutorial);
      _tutorialDirector.stopped += OnTutorialTimelineStopped;
      _tutorialDirector.Play();
      Debug.Log("[GestureSceneController] Tutorial Timeline started");
    }

    /// <summary>
    /// Timeline이 끝까지 재생되거나 스킵됐을 때 호출
    /// </summary>
    private void OnTutorialTimelineStopped(PlayableDirector _)
    {
      UnsubscribeTutorialEvents();
      EnterPlayingState();
    }

    private void UnsubscribeTutorialEvents()
    {
      if (_tutorialDirector != null)
        _tutorialDirector.stopped -= OnTutorialTimelineStopped;
    }

    /// <summary>
    /// 스킵 버튼: Timeline 마지막 프레임으로 점프 → stopped 이벤트 자동 발행
    /// </summary>
    public void SkipTutorial()
    {
      if (_state != GestureSceneState.Tutorial || _tutorialDirector == null) return;
        _tutorialDirector.time = _tutorialDirector.duration;
        _tutorialDirector.Evaluate();
        _tutorialDirector.Stop();
    }

    /// <summary>
    /// 재시청 버튼: Playing 중 언제든 튜토리얼로 돌아갈 수 있음.
    /// Presenter를 일시 정지하고 Tutorial 상태로 전환.
    /// </summary>
    public void RewatchTutorial()
    {
      if (_tutorialDirector == null) return;
      if (_state != GestureSceneState.Playing) return;

      // Presenter 일시 정지 (완전 Cleanup 아님 — 재생 재개를 위해 유지)
      _gesturePlayPresenter?.Cleanup();
      _gesturePlayPresenter = null;

      _tutorialDirector.time = 0;
      EnterTutorialState();
    }
    
    /// <summary>
    /// Playing 상태 진입
    /// </summary>
    private void EnterPlayingState()
    {
      ChangeState(GestureSceneState.Playing);
      
      // 1. View 초기화
      _gesturePlayView.Initialize(_targetGesture);
      
      // 2. Presenter 생성 및 초기화
      _gesturePlayPresenter = new UI.GesturePlayPresenter();
      _gesturePlayPresenter.Initialize(
        view: _gesturePlayView,
        detector: _gestureDetector,
        targetGesture: _targetGesture,
        thresholds: _sceneConfig?.thresholds,
        onSuccess: OnGestureSuccess,
        requiredHoldDuration:  _requiredHoldDuration,
        progressShowThreshold: _progressShowThreshold,
        onTimeout: OnRecognitionTimeout,
        noHandTimeout: _noHandTimeout,
        holdFailTimeout: _holdFailTimeout
      );
      
      // 3. 플레이 시작
      _gesturePlayPresenter.StartPlay();
    }
    
    // ========== 실패 시 건너뛰기 ==========

    /// <summary>
    /// 인식 타임아웃 콜백 (Presenter → Controller)
    /// A(NoHands): 위치 재조정 안내 / B(HoldFail): 가장 큰 실패 이유 + 넘어가기
    /// </summary>
    private void OnRecognitionTimeout(UI.GestureTimeoutInfo info)
    {
      if (_state != GestureSceneState.Playing) return;

      // 이중 안전장치: 설정창 등이 떠 있으면 오버레이를 띄우지 않음
      // (복귀 시 Presenter.SetTimersSuspended(false)가 타이머를 리셋하므로 재무장됨)
      if (IsRecognitionBlocked())
        return;

      // 모델 재로딩 없이 코루틴만 일시정지 (Stop 아님)
      _gestureDetector?.Pause();
      _gesturePlayPresenter?.PausePlay();

      bool isNoHands = info.Type == UI.GestureTimeoutType.NoHands;
      string message = isNoHands ? _msgNoHands : ReasonToText(info.Reason);

      // A는 다시해보기만, B는 넘어가기까지 표시
      ShowFailOverlay(message, showSkip: !isNoHands);
      ChangeState(GestureSceneState.Paused);
    }

    /// <summary>다시해보기: 일시정지 해제 후 Playing 재개 (타이머 리셋)</summary>
    private void OnRetryClicked()
    {
      if (_state != GestureSceneState.Paused) return;

      HideFailOverlay();
      _gestureDetector?.Resume();
      _gesturePlayPresenter?.ResumePlay();
      ChangeState(GestureSceneState.Playing);
      Debug.Log("[GestureSceneController] Retry → recognition resumed");
    }

    /// <summary>넘어가기: 성공 처리로 전환 (Quest 완료 + 성공 연출)</summary>
    private void OnFailSkipClicked()
    {
      if (_state != GestureSceneState.Paused) return;

      HideFailOverlay();
      Debug.Log("[GestureSceneController] Skip → forcing success");
      // OnGestureSuccess가 Presenter.Cleanup()(=Detector.Stop)을 호출하므로 별도 정지 불필요
      OnGestureSuccess(_targetGesture);
    }

    private void ShowFailOverlay(string message, bool showSkip)
    {
      if (_failReasonText != null) _failReasonText.text = message;
      if (_failSkipButton != null) _failSkipButton.gameObject.SetActive(showSkip);
      if (_failOverlay != null) _failOverlay.SetActive(true);
    }

    private void HideFailOverlay()
    {
      if (_failOverlay != null) _failOverlay.SetActive(false);
    }

    /// <summary>실패 이유 플래그 → 안내 문구 매핑</summary>
    private string ReasonToText(GestureFailReason reason)
    {
      switch (reason)
      {
        case GestureFailReason.PoseMissing:      return _msgPoseMissing;
        case GestureFailReason.HandMissing:      return _msgHandMissing;
        case GestureFailReason.PalmsNotForward:  return _msgPalmsNotForward;
        case GestureFailReason.HandsNotOpposite: return _msgHandsNotOpposite;
        case GestureFailReason.WristsTooFar:     return _msgWristsTooFar;
        case GestureFailReason.FingersNotOpen:   return _msgFingersNotOpen;
        case GestureFailReason.NotRising:        return _msgNotRising;
        default:                                 return _msgGeneric;
      }
    }

    /// <summary>
    /// 제스처 성공 콜백
    /// </summary>
    private void OnGestureSuccess(GestureType gestureType)
    {
      Debug.Log($"[GestureSceneController] Gesture SUCCESS (held {_requiredHoldDuration}s): {gestureType}");
      GestureSceneEvents.RaiseGestureComplete(gestureType);

      _gesturePlayPresenter?.Cleanup();
      _gesturePlayPresenter = null;

      NotifyQuestPhaseComplete();
      EnterSuccessState();
    }

    // Quest 완료 알림

    /// <summary>씬 진입 시 제스처 인식 phase 완료 (P04 또는 P08)</summary>
    private void NotifyEntryPhaseComplete()
    {
      if (_requestCompletePhaseEvent == null) return;

      string phaseID = (_currentObjectiveID == _objectiveID_Fly)
        ? _entryPhaseID_Fly
        : _entryPhaseID_NoFly;

      _requestCompletePhaseEvent.Raise(new CompletePhaseRequest
      {
        QuestID     = _questID,
        ObjectiveID = _currentObjectiveID,
        PhaseID     = phaseID
      });

      Debug.Log($"[GestureSceneController] Entry phase 완료: {phaseID}");

      // P04(NoFly 진입) 완료 시 스킬 지급
      if (phaseID == _entryPhaseID_NoFly && !string.IsNullOrEmpty(_skillID_NoFlyEntry))
      {
        _requestAcquireItemEvent?.Raise(_skillID_NoFlyEntry);
        Debug.Log($"[GestureSceneController] 스킬 지급: {_skillID_NoFlyEntry}");
      }
    }

    /// <summary>제스처 성공 시 결과 phase 완료 (P05 또는 P09)</summary>
    private void NotifyQuestPhaseComplete()
    {
      if (_requestCompletePhaseEvent == null)
      {
        Debug.LogWarning("[GestureSceneController] CompletePhaseGameEvent가 연결되지 않았습니다. Quest 진행이 업데이트되지 않습니다.");
        return;
      }

      string phaseID = (_currentObjectiveID == _objectiveID_Fly) ? _phaseID_Fly : _phaseID_NoFly;

      _requestCompletePhaseEvent.Raise(new CompletePhaseRequest
      {
        QuestID     = _questID,
        ObjectiveID = _currentObjectiveID,
        PhaseID     = phaseID
      });

      Debug.Log($"[GestureSceneController] Phase 완료 요청: Quest={_questID}, Obj={_currentObjectiveID}, Phase={phaseID}");
    }

    private void EnterSuccessState()
    {
      PlayableDirector director = (_currentObjectiveID == _objectiveID_Fly)
        ? _successDirector2
        : _successDirector1;

      if (director == null)
      {
        Debug.Log("[GestureSceneController] No Success Timeline → Exit");
        ExitScene();
        return;
      }

      ChangeState(GestureSceneState.Success);

      _gesturePlayView?.FreezeGolem();

      director.stopped += OnSuccessTimelineStopped;
      director.Play();
      Debug.Log($"[GestureSceneController] Success Timeline started: {director.name}");
    }

    private void OnSuccessTimelineStopped(PlayableDirector director)
    {
      director.stopped -= OnSuccessTimelineStopped;
      ExitScene();
    }

    private void UnsubscribeSuccessEvents()
    {
      if (_successDirector1 != null)
        _successDirector1.stopped -= OnSuccessTimelineStopped;
      if (_successDirector2 != null)
        _successDirector2.stopped -= OnSuccessTimelineStopped;
    }
    
    /// <summary>
    /// Exit 상태 진입
    /// </summary>
    private void ExitScene()
    {
      if (_state == GestureSceneState.Exit) return;
      
      ChangeState(GestureSceneState.Exit);
      
      _gesturePlayPresenter?.Cleanup();
      _gesturePlayPresenter = null;

      _gesturePlayView?.Cleanup();
      
      GestureSceneEvents.RaiseGestureSceneExit();
      Debug.Log("[GestureSceneController] Scene exit requested");
      
      UnityEngine.SceneManagement.SceneManager.LoadScene("Forest");
    }
    
    // ========== 유틸리티 메서드 ==========
    
    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(GestureSceneState newState)
    {
      if (_state == newState) return;
      Debug.Log($"[GestureSceneController] State: {_state} → {newState}");
      _state = newState;

      if (_state == GestureSceneState.Playing)
        GestureSceneEvents.RaiseGestureStart(_targetGesture);
    }
    
    /// <summary>
    /// 컴포넌트 검증
    /// </summary>
    private bool ValidateComponents()
    {
      if (_sceneConfig == null)
      {
        Debug.LogWarning("[GestureSceneController] Scene config not assigned! Using defaults.");
      }
      
      if (_gestureDetector == null)
      {
        Debug.LogError("[GestureSceneController] GestureDetector not assigned!");
        return false;
      }
      
      if (_gesturePlayView == null)
      {
        Debug.LogError("[GestureSceneController] GesturePlayView not assigned!");
        return false;
      }
      
      return true;
    }
    
    // ========== Public API (런타임 제어용) ==========
    
    /// <summary>
    /// 타겟 제스처 변경 (런타임)
    /// </summary>
    public void SetTargetGesture(GestureType newGesture)
    {
      _targetGesture = newGesture;
      _gesturePlayPresenter?.SetTargetGesture(newGesture);
      Debug.Log($"[GestureSceneController] Target gesture changed to: {newGesture}");
    }
    
    /// <summary>
    /// Annotation 표시 토글
    /// </summary>
    public void SetShowAnnotations(bool show)
    {
      _gesturePlayView?.SetShowAnnotations(show);
    }
  }
}