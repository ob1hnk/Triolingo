using UnityEngine;
using UnityEngine.Playables;

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
  /// [Quest 연동 - TODO: 한나님 작업 완료 후]
  /// - Managers.Quest 연동 활성화
  /// - _requestCompletePhaseEvent SO 연결
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
    [SerializeField] private Tutorial.GestureTutorialManager _tutorialManager;
    [Tooltip("OBJ-02 전용 튜토리얼 Timeline. null이면 튜토리얼 없이 바로 Playing.")]
    [SerializeField] private PlayableDirector _tutorialDirector;

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

    // Quest 연동 (읽기용)
    [Header("Quest - Objective IDs")]
    [SerializeField] private string _objectiveID_NoFly = "MQ-02-OBJ-02";
    [SerializeField] private string _objectiveID_Fly   = "MQ-02-OBJ-04";

    // Quest 연동 (쓰기용 - SO 이벤트 버스)
    [Header("Quest - Write (한나님 작업 완료 후 연결)")]
    [Tooltip("QuestManager의 requestCompletePhaseEvent와 동일한 SO")]
    [SerializeField] private CompletePhaseGameEvent _requestCompletePhaseEvent;
    [SerializeField] private string _questID      = "MQ-02";
    [SerializeField] private string _phaseID_NoFly = "MQ-02-P05";
    [SerializeField] private string _phaseID_Fly   = "MQ-02-P09";

    // 씬 오브젝트
    [Header("Scene Objects (Quest-Driven)")]
    [SerializeField] private GameObject _flyItem;    // OBJ-04에서 활성화
    [SerializeField] private GameObject _noFlyItem;  // OBJ-02에서 활성화
    
    // 테스트용 ─────────────────────────────────────────
    [Header("--- DEBUG (테스트용) ---")]
    [Tooltip("체크하면 Managers.Quest 무시하고 아래 설정으로 강제 적용")]
    [SerializeField] private bool _debugOverride = true;
    [Tooltip("true = OBJ-04(Fly), false = OBJ-02(NoFly)")]
    [SerializeField] private bool _debugIsFly = false;

    // 내부 상태
    private UI.GesturePlayPresenter _gesturePlayPresenter;
    private GestureSceneState _state = GestureSceneState.Entry;
    private GestureType _targetGesture;
    private string _currentObjectiveID;

    private void Start()
    {
      EnterEntryState();
    }
    
    private void Update()
    {
      // ESC 키로 강제 종료
      if (Input.GetKeyDown(KeyCode.Escape) && _state != GestureSceneState.Exit)
      {
        ExitScene();
      }
      // DEBUG: Enter 키로 강제 성공
      if (_debugOverride && Input.GetKeyDown(KeyCode.Return) && _state == GestureSceneState.Playing)
        OnGestureSuccess(_targetGesture);
    }
    
    private void OnDestroy()
    {
      UnsubscribeTutorialEvents();
      UnsubscribeSuccessEvents();
      _gesturePlayPresenter?.Cleanup();
      GestureSceneEvents.ClearAllSubscribers();
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
      
      // OBJ-02일 때만 튜토리얼 먼저 실행
      bool needsTutorial = _tutorialManager != null
        && _tutorialDirector != null
        && _currentObjectiveID == _objectiveID_NoFly;

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

      if (Managers.Quest.IsQuestActive(_objectiveID_Fly) ||
        Managers.Quest.IsQuestCompleted(_objectiveID_NoFly))
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
        progressShowThreshold: _progressShowThreshold
      );
      
      // 3. 플레이 시작
      _gesturePlayPresenter.StartPlay();
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
      
      // TODO: 실제 씬 전환. 한나님
      // SceneManager.LoadScene("MainScene");
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