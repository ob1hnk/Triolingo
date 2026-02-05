using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 씬의 전체 흐름을 제어하는 Controller (Glue 코드 30%)
  /// - GestureDetector에서 Landmark 데이터 받기
  /// - GestureRecognizer로 제스처 판정
  /// - Avatar, UI, Annotation 업데이트
  /// </summary>
  public class GestureSceneController : MonoBehaviour
  {
    [Header("Target Gesture")]
    [SerializeField] private GestureType _targetGesture = GestureType.Wind;

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

    // Debounce 상태
    private float _lastDetectedTime = 0f;

    private void Start()
    {
      InitializeComponents();
      StartGestureDetection();
    }

    private void OnDestroy()
    {
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated -= OnLandmarksUpdated;
      }
    }

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    private void InitializeComponents()
    {
      // GestureRecognizer 초기화
      _gestureRecognizer = new GestureRecognizer();
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
  }
}