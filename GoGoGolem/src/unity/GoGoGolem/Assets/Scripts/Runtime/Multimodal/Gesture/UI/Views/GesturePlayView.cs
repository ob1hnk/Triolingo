using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 화면 표시 데이터 (Presenter → View 전달용)
  /// </summary>
  public struct DisplayData
  {
    public PoseLandmarkerResult PoseData;
    public HandLandmarkerResult HandData;
    public GestureResult GestureResult;
    public bool HasValidData;
  }

  /// <summary>
  /// 제스처 플레이 화면 (View) - Scene 오브젝트 및 시각적 요소 관리
  /// Presenter는 View 내부 구조를 알지 못함 (캡슐화)
  /// </summary>
  public class GesturePlayView : MonoBehaviour
  {
    [Header("3D Avatar")]
    [SerializeField] private AvatarLandmarkAnimator _avatarAnimator;
    
    [Header("UI")]
    [SerializeField] private GestureUIController _gestureUIController;
    
    [Header("Annotations (Optional)")]
    [SerializeField] private Component _handAnnotationController;
    [SerializeField] private Component _poseAnnotationController;
    [SerializeField] private bool _showAnnotations = true;
    
    [Header("Scene Objects (Optional)")]
    [SerializeField] private GameObject _backgroundObjects;
    [SerializeField] private GameObject _targetObjects;
    
    // Debounce 상태 (View 내부 관리)
    private float _lastDetectedTime = 0f;
    private float _debounceDuration = 0.2f;
    
    /// <summary>
    /// View 초기화
    /// </summary>
    public void Initialize(GestureType targetGesture, float debounceDuration)
    {
      _debounceDuration = debounceDuration;
      
      // UI Controller 설정
      if (_gestureUIController != null)
      {
        _gestureUIController.SetTargetGesture(targetGesture);
      }
      
      // Scene 오브젝트 활성화
      if (_backgroundObjects != null)
        _backgroundObjects.SetActive(true);
      
      if (_targetObjects != null)
        _targetObjects.SetActive(true);
      
      Debug.Log("[GesturePlayView] Initialized");
    }
    
    /// <summary>
    /// 화면 업데이트 (Presenter → View 단일 진입점)
    /// </summary>
    public void UpdateDisplay(DisplayData data)
    {
      // 1. 데이터 유효성에 따른 Avatar 업데이트
      if (!data.HasValidData)
      {
        ResetAvatar();
        return;
      }
      
      // 2. Avatar 업데이트
      UpdateAvatar(data.PoseData, data.HandData);
      
      // 3. UI 업데이트 (Debounce 적용)
      UpdateGestureUI(data.GestureResult);
      
      // 4. Annotation 그리기
      DrawAnnotations(data.HandData, data.PoseData);
    }
    
    /// <summary>
    /// Avatar 업데이트 (내부 메서드)
    /// </summary>
    private void UpdateAvatar(PoseLandmarkerResult poseData, HandLandmarkerResult handData)
    {
      if (_avatarAnimator != null)
      {
        _avatarAnimator.UpdateAvatar(poseData, handData);
      }
    }
    
    /// <summary>
    /// Avatar 리셋 (내부 메서드)
    /// </summary>
    private void ResetAvatar()
    {
      if (_avatarAnimator != null)
      {
        _avatarAnimator.ResetToIdle();
      }
    }
    
    /// <summary>
    /// UI 업데이트 (Debounce 적용)
    /// </summary>
    private void UpdateGestureUI(GestureResult result)
    {
      if (_gestureUIController == null) return;
      
      // Debounce 로직: UI 깜빡임 방지
      bool isDetectedNow = result.IsDetected;
      
      if (isDetectedNow)
      {
        _lastDetectedTime = Time.time;
        _gestureUIController.UpdateGestureResult(result);
      }
      else
      {
        // Debounce 시간 경과 후에만 "미인식" 상태 표시
        if (Time.time - _lastDetectedTime > _debounceDuration)
        {
          _gestureUIController.UpdateGestureResult(GestureResult.None);
        }
      }
    }
    
    /// <summary>
    /// Annotation 그리기 (리플렉션 사용 - 기존 컨트롤러 호환)
    /// </summary>
    private void DrawAnnotations(HandLandmarkerResult handData, PoseLandmarkerResult poseData)
    {
      if (!_showAnnotations) return;
      
      DrawAnnotation(_handAnnotationController, "DrawNow", handData);
      DrawAnnotation(_poseAnnotationController, "DrawNow", poseData);
    }
    
    /// <summary>
    /// Annotation 그리기 헬퍼 (리플렉션)
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
    /// 타겟 제스처 변경 (Public API)
    /// </summary>
    public void SetTargetGesture(GestureType newGesture)
    {
      if (_gestureUIController != null)
      {
        _gestureUIController.SetTargetGesture(newGesture);
      }
    }
    
    /// <summary>
    /// Annotation 표시 토글
    /// </summary>
    public void SetShowAnnotations(bool show)
    {
      _showAnnotations = show;
    }
    
    /// <summary>
    /// View 정리
    /// </summary>
    public void Cleanup()
    {
      // Avatar 리셋
      ResetAvatar();
      
      // Scene 오브젝트 비활성화
      if (_backgroundObjects != null)
        _backgroundObjects.SetActive(false);
      
      Debug.Log("[GesturePlayView] Cleaned up");
    }
  }
}