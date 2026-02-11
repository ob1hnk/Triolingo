using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 씬 상태
  /// </summary>
  public enum GestureSceneState
  {
    Entry,    // 진입 (초기화)
    Playing,  // 플레이 중 (제스처 인식)
    Success,  // 성공 (연출 준비)
    Exit      // 종료
  }

  /// <summary>
  /// 제스처 씬 전체 흐름 제어 Controller (Glue 코드)
  /// - 상태 관리
  /// - 이벤트 버스 통신
  /// - Entry → Playing → Exit
  /// </summary>
  public class GestureSceneController : MonoBehaviour
  {
    [Header("Scene Configuration")]
    [SerializeField] private GestureSceneConfig _sceneConfig;

    [Header("Core Components")]
    [SerializeField] private GestureDetector _gestureDetector;

    [Header("UI")]
    [SerializeField] private UI.GesturePlayView _gesturePlayView;

    [Header("Settings")]
    [SerializeField] private float _debounceDuration = 0.2f; // UI 깜빡임 방지

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
      // Presenter 정리
      if (_gesturePlayPresenter != null)
      {
        _gesturePlayPresenter.Cleanup();
      }
      
      // EventBus 구독 해제
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
      
      // 바로 Playing 상태로 전환
      EnterPlayingState();
    }
    
    /// <summary>
    /// Playing 상태 진입
    /// </summary>
    private void EnterPlayingState()
    {
      ChangeState(GestureSceneState.Playing);
      
      // 1. View 초기화 (Debounce 설정 포함)
      _gesturePlayView.Initialize(_targetGesture, _debounceDuration);
      
      // 2. Presenter 생성 및 초기화
      _gesturePlayPresenter = new UI.GesturePlayPresenter();
      _gesturePlayPresenter.Initialize(
        view: _gesturePlayView,
        detector: _gestureDetector,
        targetGesture: _targetGesture,
        thresholds: _sceneConfig?.thresholds,
        onSuccess: OnGestureSuccess
      );
      
      // 3. 플레이 시작
      _gesturePlayPresenter.StartPlay();
    }
    
    /// <summary>
    /// 제스처 성공 콜백
    /// </summary>
    private void OnGestureSuccess(GestureType gestureType)
    {
      Debug.Log($"[GestureSceneController] Gesture SUCCESS: {gestureType}");
      
      // 이벤트 발행
      GestureSceneEvents.RaiseGestureComplete(gestureType);
      
      // Phase 2에서는 바로 Exit
      // Phase 4에서는 Success 상태 추가 예정
      ExitScene();
    }
    
    /// <summary>
    /// Exit 상태 진입
    /// </summary>
    private void ExitScene()
    {
      if (_state == GestureSceneState.Exit) return;
      
      ChangeState(GestureSceneState.Exit);
      
      // 1. Presenter 정리
      if (_gesturePlayPresenter != null)
      {
        _gesturePlayPresenter.Cleanup();
        _gesturePlayPresenter = null;
      }
      
      // 2. View 정리
      if (_gesturePlayView != null)
      {
        _gesturePlayView.Cleanup();
      }
      
      // 3. 이벤트 발행
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
      
      // 상태별 이벤트 발행
      switch (_state)
      {
        case GestureSceneState.Playing:
          GestureSceneEvents.RaiseGestureStart(_targetGesture);
          break;
      }
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