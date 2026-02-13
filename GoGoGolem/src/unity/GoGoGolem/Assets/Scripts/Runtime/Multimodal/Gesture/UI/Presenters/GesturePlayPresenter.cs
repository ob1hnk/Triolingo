using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 제스처 플레이 로직 처리 (Presenter)
  /// - GestureDetector 이벤트 구독
  /// - GestureRecognizer 호출
  /// - View 업데이트 (View 내부 구조는 모름)
  /// </summary>
  public class GesturePlayPresenter
  {
    // View 참조 (인터페이스처럼 사용)
    private GesturePlayView _view;
    
    // Core 컴포넌트
    private GestureDetector _gestureDetector;
    private GestureRecognizer _gestureRecognizer;
    
    // 설정값
    private GestureType _targetGesture;
    
    // 성공 콜백
    private System.Action<GestureType> _onGestureSuccess;
    
    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize(
      GesturePlayView view,
      GestureDetector detector,
      GestureType targetGesture,
      GestureThresholdData thresholds,
      System.Action<GestureType> onSuccess)
    {
      _view = view;
      _gestureDetector = detector;
      _targetGesture = targetGesture;
      _onGestureSuccess = onSuccess;
      
      // GestureRecognizer 생성 및 설정
      _gestureRecognizer = new GestureRecognizer(thresholds ?? GestureThresholdData.Default());
      _gestureRecognizer.SetActiveGesture(targetGesture);
      
      // GestureDetector 이벤트 구독
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated += OnLandmarksUpdated;
      }
      
      Debug.Log($"[GesturePlayPresenter] Initialized - Target: {targetGesture}");
    }
    
    /// <summary>
    /// 플레이 시작
    /// </summary>
    public void StartPlay()
    {
      if (_gestureDetector != null)
      {
        _gestureDetector.Play();
        Debug.Log("[GesturePlayPresenter] Play started");
      }
    }
    
    /// <summary>
    /// Landmark 데이터 업데이트 콜백 (GestureDetector에서 호출)
    /// </summary>
    private void OnLandmarksUpdated(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // 1. 데이터 유효성 검사
      bool hasHandData = IsValidHandData(handResult);
      bool hasPoseData = poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0;
      
      if (!hasHandData || !hasPoseData)
      {
        // 데이터 부족 시 View에 전달
        _view?.UpdateDisplay(new DisplayData
        {
          HasValidData = false
        });
        
        DisposeAllMasks(poseResult);
        return;
      }
      
      // 2. 제스처 인식
      var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);
      
      // 3. 타겟 제스처 성공 시 콜백 호출
      if (gestureResult.Type == _targetGesture && gestureResult.IsDetected)
      {
        Debug.Log($"[GesturePlayPresenter] Gesture SUCCESS! Type={gestureResult.Type}, Confidence={gestureResult.Confidence:F2}");
        _onGestureSuccess?.Invoke(gestureResult.Type);
      }
      
      // 4. View 업데이트 (단일 진입점)
      _view?.UpdateDisplay(new DisplayData
      {
        PoseData = poseResult,
        HandData = handResult,
        GestureResult = gestureResult,
        HasValidData = true
      });
      
      // 5. 메모리 정리 (Pose segmentation masks)
      DisposeAllMasks(poseResult);
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
    /// Hand 데이터 유효성 검사
    /// </summary>
    private bool IsValidHandData(HandLandmarkerResult handResult)
    {
      // 1. handLandmarks null 체크
      if (handResult.handLandmarks == null)
        return false;
      
      // 2. 손이 2개 감지되었는지 확인
      if (handResult.handLandmarks.Count < 2)
        return false;
      
      // 3. 각 손의 landmarks가 21개 있는지 확인
      // handLandmarks[i].landmarks 구조 사용
      for (int i = 0; i < 2; i++)
      {
        var landmarks = handResult.handLandmarks[i].landmarks;
        if (landmarks == null || landmarks.Count < 21)
        {
          return false;
        }
      }
      
      return true;
    }
    
    /// <summary>
    /// 타겟 제스처 변경 (런타임)
    /// </summary>
    public void SetTargetGesture(GestureType newGesture)
    {
      _targetGesture = newGesture;
      _gestureRecognizer?.SetActiveGesture(newGesture);
      _view?.SetTargetGesture(newGesture);
      Debug.Log($"[GesturePlayPresenter] Target gesture changed to: {newGesture}");
    }
    
    /// <summary>
    /// 정리
    /// </summary>
    public void Cleanup()
    {
      // 이벤트 구독 해제
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated -= OnLandmarksUpdated;
      }
      
      // GestureDetector 정지
      if (_gestureDetector != null)
      {
        _gestureDetector.Stop();
      }
      
      Debug.Log("[GesturePlayPresenter] Cleaned up");
    }
  }
}