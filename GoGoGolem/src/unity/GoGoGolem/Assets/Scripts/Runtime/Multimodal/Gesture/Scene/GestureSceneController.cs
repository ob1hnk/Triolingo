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
  /// - 상태 관리
  /// - 이벤트 버스 통신
  /// - Entry
  ///     ├─ IsFirstTime? → Tutorial → Playing(3초) → Success → Exit
  ///     └─ (no)                    → Playing(3초) → Success → Exit
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
    [Tooltip("씬별 튜토리얼 Timeline. null이면 튜토리얼 없이 바로 Playing.")]
    [SerializeField] private PlayableDirector _tutorialDirector;

    [Header("Success")]
    [Tooltip("성공 연출 Timeline. null이면 바로 Exit.")]
    [SerializeField] private PlayableDirector _successDirector;

    [Header("Settings")]
    [SerializeField] private float _debounceDuration = 0.2f; // UI 깜빡임 방지
    [SerializeField] private float _requiredHoldDuration = 3f; // 제스처 유지 시간

    // Presenter
    private UI.GesturePlayPresenter _gesturePlayPresenter;
    
    // 씬 상태
    private GestureSceneState _state = GestureSceneState.Entry;

    // 현재 타겟 제스처 (Config 또는 직접 설정)
    private GestureType _targetGesture;

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
    }
    
    private void OnDestroy()
    {
      UnsubscribeTutorialEvents();
      UnsubscribeSuccessEvents();
      _gesturePlayPresenter?.Cleanup();
      GestureSceneEvents.ClearAllSubscribers();
    }
    
    // ========== 상태 전환 메서드 ==========
    
    /// <summary>
    /// Entry 상태 진입
    /// </summary>
    private void EnterEntryState()
    {
      ChangeState(GestureSceneState.Entry);
      
      // Config 검증
      if (!ValidateComponents())
      {
        Debug.LogError("[GestureSceneController] Component validation failed!");
        return;
      }
      
      // Config에서 타겟 제스처 로드
      _targetGesture = _sceneConfig != null ? _sceneConfig.targetGesture : GestureType.Wind;
      
      bool needsTutorial = _tutorialManager != null
                && _tutorialDirector != null
                && _tutorialManager.IsFirstTime(_targetGesture);

      if (needsTutorial)
        EnterTutorialState();
      else
        EnterPlayingState();
    }

    /// <summary>
    /// Tutorial 상태 진입
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
      _tutorialManager.MarkAsLearned(_targetGesture);
      EnterPlayingState();
    }

    /// <summary>
    /// 스킵 버튼 → Timeline 마지막 프레임으로 점프 → stopped 이벤트 자동 발행
    /// </summary>
    public void SkipTutorial()
    {
      if (_state != GestureSceneState.Tutorial || _tutorialDirector == null) return;
        _tutorialDirector.time = _tutorialDirector.duration;
        _tutorialDirector.Evaluate();
        _tutorialDirector.Stop();  // stopped 이벤트 발행
    }

    private void UnsubscribeTutorialEvents()
    {
      if (_tutorialDirector != null)
        _tutorialDirector.stopped -= OnTutorialTimelineStopped;
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
        requiredHoldDuration:  _requiredHoldDuration
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

      EnterSuccessState();
    }

    private void EnterSuccessState()
    {
      // Success Timeline이 없으면 바로 Exit
      if (_successDirector == null)
      {
        Debug.Log("[GestureSceneController] No Success Timeline → Exit");
        ExitScene();
        return;
      }

      ChangeState(GestureSceneState.Success);

      _successDirector.stopped += OnSuccessTimelineStopped;
      _successDirector.Play();

      Debug.Log("[GestureSceneController] Success Timeline started");
    }

    private void OnSuccessTimelineStopped(PlayableDirector _)
    {
      UnsubscribeSuccessEvents();
      ExitScene();
    }

    private void UnsubscribeSuccessEvents()
    {
      if (_successDirector != null)
        _successDirector.stopped -= OnSuccessTimelineStopped;
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
      
      // TODO: 실제 씬 전환
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