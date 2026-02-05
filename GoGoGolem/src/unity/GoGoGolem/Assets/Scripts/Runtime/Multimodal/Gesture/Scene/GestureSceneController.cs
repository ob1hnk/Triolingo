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
    Success,  // 성공 (연출 준비, Phase 4에서 사용)
    Exit      // 종료
  }

  /// <summary>
  /// 제스처 씬의 전체 흐름을 제어하는 Controller (Glue 코드)
  /// - GestureDetector에서 Landmark 데이터 받기
  /// - GestureRecognizer로 제스처 판정
  /// - Avatar, UI, Annotation 업데이트
  /// </summary>
  public class GestureSceneController : MonoBehaviour
  {
    [Header("Scene Configuration")]
    [SerializeField] private GestureSceneConfig _sceneConfig;

    [Header("Core Components")]
    [SerializeField] private GestureDetector _gestureDetector;

    [Header("3D Avatar")]
    [SerializeField] private AvatarLandmarkAnimator _avatarAnimator;

    [Header("UI")]
    [SerializeField] private UI.SingleGestureUIController _gestureUIController;

    [Header("Annotations (Optional)")]
    [SerializeField] private Component _handAnnotationController; // HandLandmarkerResultAnnotationController
    [SerializeField] private Component _poseAnnotationController; // PoseLandmarkerResultAnnotationController
    [SerializeField] private bool _showAnnotations = true;

    [Header("Settings")]
    [SerializeField] private float _debounceDuration = 0.2f; // UI 깜빡임 방지

    // 제스처 인식기
    private GestureRecognizer _gestureRecognizer;

    // 상태 관리
    private GestureSceneState _state = GestureSceneState.Entry;

    // Debounce 상태
    private float _lastDetectedTime = 0f;

    // 현재 타겟 제스처 (Config 또는 직접 설정)
    private GestureType _targetGesture;

    private void Start()
    {
      ChangeState(GestureSceneState.Entry);
      InitializeComponents();
      StartGestureDetection();
      ChangeState(GestureSceneState.Playing);
    }

    private void Update()
    {
      // ESC 키로 씬 종료
      if (Input.GetKeyDown(KeyCode.Escape) && _state != GestureSceneState.Exit)
      {
        ExitScene();
      }
    }

    private void OnDestroy()
    {
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated -= OnLandmarksUpdated;
      }

      // EventBus 구독 해제 (메모리 누수 방지)
      GestureSceneEvents.ClearAllSubscribers();
    }

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    private void InitializeComponents()
    {
      // Config에서 설정 로드
      if (_sceneConfig != null)
      {
        _targetGesture = _sceneConfig.targetGesture;
        Debug.Log($"[GestureSceneController] Loaded config - Target: {_targetGesture}");
      }
      else
      {
        Debug.LogWarning("[GestureSceneController] Scene config not assigned! Using default Wind gesture.");
        _targetGesture = GestureType.Wind;
      }

      // GestureRecognizer 초기화
      if (_sceneConfig != null && _sceneConfig.thresholds != null)
      {
        // Config의 Threshold 사용
        _gestureRecognizer = new GestureRecognizer(_sceneConfig.thresholds);
      }
      else
      {
        // 기본값 사용
        _gestureRecognizer = new GestureRecognizer();
      }
      
      _gestureRecognizer.SetActiveGesture(_targetGesture);

      // UI Controller에 타겟 제스처 설정
      if (_gestureUIController != null)
      {
        _gestureUIController.SetTargetGesture(_targetGesture);
        Debug.Log($"[GestureSceneController] Set target gesture to {_targetGesture} in UI Controller");
      }

      // GestureDetector 이벤트 구독
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated += OnLandmarksUpdated;
        Debug.Log("[GestureSceneController] Subscribed to GestureDetector events");
      }
      else
      {
        Debug.LogError("[GestureSceneController] GestureDetector is not assigned!");
      }

      Debug.Log($"[GestureSceneController] Initialized - Target: {_targetGesture}");
    }

    /// <summary>
    /// 제스처 감지 시작
    /// </summary>
    private void StartGestureDetection()
    {
      if (_gestureDetector != null)
      {
        // VisionTaskApiRunner의 Play() 메서드 사용
        _gestureDetector.Play();
        Debug.Log("[GestureSceneController] Gesture detection started");
      }
    }

    /// <summary>
    /// Landmark 데이터 업데이트 콜백 (GestureDetector에서 호출)
    /// </summary>
    private void OnLandmarksUpdated(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // 1. 데이터 유효성 검사
      bool hasHandData = handResult.handLandmarks != null && handResult.handLandmarks.Count >= 2;
      bool hasPoseData = poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0;

      if (!hasHandData || !hasPoseData)
      {
        // 데이터 부족 시 Avatar 리셋
        _avatarAnimator?.ResetToIdle();
        return;
      }

      // 2. Avatar 업데이트
      UpdateAvatar(poseResult, handResult);

      // 3. 제스처 인식
      var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);

      // 4. UI 업데이트 (타겟 제스처만)
      bool isDetectedNow = false;
      if (gestureResult.Type == _targetGesture && gestureResult.IsDetected)
      {
        _lastDetectedTime = Time.time;
        _gestureUIController?.UpdateGestureResult(gestureResult);
        isDetectedNow = true;

        // 제스처 성공 시 이벤트 발행 및 씬 종료
        if (_state == GestureSceneState.Playing)
        {
          OnGestureSuccess(gestureResult.Type);
        }
      }

      // 5. Debounce 로직 (UI 깜빡임 방지)
      if (!isDetectedNow)
      {
        if (Time.time - _lastDetectedTime > _debounceDuration)
        {
          _gestureUIController?.UpdateGestureResult(GestureResult.None);
        }
      }

      // 6. Annotation 그리기 (리플렉션 사용)
      if (_showAnnotations)
      {
        DrawAnnotation(_handAnnotationController, "DrawNow", handResult);
        DrawAnnotation(_poseAnnotationController, "DrawNow", poseResult);
      }

      // 7. 메모리 정리 (Pose segmentation masks)
      DisposeAllMasks(poseResult);
    }

    /// <summary>
    /// Avatar 업데이트
    /// </summary>
    private void UpdateAvatar(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
      if (_avatarAnimator != null)
      {
        _avatarAnimator.UpdateAvatar(poseResult, handResult);
      }
    }

    /// <summary>
    /// Annotation 그리기 (리플렉션 사용)
    /// </summary>
    private void DrawAnnotation(Component controller, string methodName, object result)
    {
      if (controller == null) return;

      var method = controller.GetType().GetMethod(methodName);
      if (method != null)
      {
        method.Invoke(controller, new object[] { result });
      }
    }

    /// <summary>
    /// Pose segmentation masks 메모리 정리
    /// </summary>
    private void DisposeAllMasks(PoseLandmarkerResult result)
    {
      if (result.segmentationMasks != null)
      {
        foreach (var mask in result.segmentationMasks)
        {
          mask?.Dispose();
        }
      }
    }

    /// <summary>
    /// 타겟 제스처 변경 (런타임)
    /// </summary>
    public void SetTargetGesture(GestureType newGesture)
    {
      _targetGesture = newGesture;
      _gestureRecognizer?.SetActiveGesture(newGesture);
      _gestureUIController?.SetTargetGesture(newGesture);
      Debug.Log($"[GestureSceneController] Target gesture changed to: {newGesture}");
    }

    /// <summary>
    /// Annotation 표시 토글
    /// </summary>
    public void SetShowAnnotations(bool show)
    {
      _showAnnotations = show;
    }

    // ========== 상태 관리 ==========

    /// <summary>
    /// 상태 변경
    /// </summary>
    private void ChangeState(GestureSceneState newState)
    {
      if (_state == newState) return;

      Debug.Log($"[GestureSceneController] State changed: {_state} → {newState}");
      _state = newState;

      switch (_state)
      {
        case GestureSceneState.Entry:
          // 진입 시 초기화 작업 (필요 시 추가)
          break;

        case GestureSceneState.Playing:
          // 제스처 인식 시작 이벤트 발행
          GestureSceneEvents.RaiseGestureStart(_targetGesture);
          break;

        case GestureSceneState.Success:
          // 성공 연출 (Phase 4에서 구현)
          break;

        case GestureSceneState.Exit:
          // 종료 처리
          break;
      }
    }

    /// <summary>
    /// 제스처 성공 처리
    /// </summary>
    private void OnGestureSuccess(GestureType gestureType)
    {
      Debug.Log($"[GestureSceneController] Gesture SUCCESS: {gestureType}");
      
      // 이벤트 발행
      GestureSceneEvents.RaiseGestureComplete(gestureType);

      // TODO: Phase 4에서 Success 상태로 전환 후 연출
      // ChangeState(GestureSceneState.Success);
      
      // 현재는 바로 종료
      ExitScene();
    }

    /// <summary>
    /// 씬 종료 (ESC 키 또는 성공 후)
    /// </summary>
    private void ExitScene()
    {
      if (_state == GestureSceneState.Exit) return;

      ChangeState(GestureSceneState.Exit);
      
      // 이벤트 발행
      GestureSceneEvents.RaiseGestureSceneExit();

      // GestureDetector 정리
      if (_gestureDetector != null)
      {
        _gestureDetector.Stop();
      }

      Debug.Log("[GestureSceneController] Scene exit requested");
      
      // TODO: 실제 씬 전환은 SceneManager에서 처리
      // SceneManager.LoadScene("MainScene");
    }
  }
}