using UnityEngine;
using UnityEngine.UI;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 제스처 인식 결과를 UI에 표시하는 컨트롤러
  /// </summary>
  public class SingleGestureUIController : MonoBehaviour
  {
    [Header("Gesture Indicators")]
    [SerializeField] private Image _gestureIndicator;  // 씬별 설정하는 제스처 인디케이터

    [Header("Indicator Colors")]
    [SerializeField] private Color _inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); // 비활성 색상 (어두운 회색)
    [SerializeField] private Color _activeColor = new Color(1f, 0.85f, 0.4f, 1f);      // 활성 색상 (진한 노란색)
    [SerializeField] private Color _detectingColor = new Color(1f, 1f, 0f, 1f);        // 감지 중 색상 (밝은 노란색)

    [Header("Animation Settings")]
    [SerializeField] private float _fadeSpeed = 5f;        // 페이드 속도
    [SerializeField] private float _pulseSpeed = 2f;       // 펄스 속도
    [SerializeField] private float _pulseIntensity = 0.2f; // 펄스 강도

    [Header("Target Gesture")]
    [SerializeField] private GestureType _targetGesture = GestureType.Jangpoong; // 이 씬의 타겟 제스처 (설정 가능)

    private Color _currentColor;
    private bool _isActive;
    private float _pulseTime;

    private void Start()
    {
      InitializeIndicator();
    }

    private void Update()
    {
      // 펄스 애니메이션을 위한 시간 업데이트
      _pulseTime += Time.deltaTime * _pulseSpeed;

      // 색상 부드럽게 전환
      if (_gestureIndicator != null)
      {
        Color targetColor = _isActive ? _activeColor : _inactiveColor;
        _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * _fadeSpeed);
        
        // 활성화 시 펄스 효과
        if (_isActive)
        {
          float pulse = Mathf.Sin(_pulseTime) * _pulseIntensity;
          _gestureIndicator.color = _currentColor * (1f + pulse);
        }
        else
        {
          _gestureIndicator.color = _currentColor;
        }
      }
    }

    /// <summary>
    /// ⭐ 제스처 인식 결과 UI 반영 [Trigger]
    /// </summary>
    public void UpdateGestureResult(GestureResult result)
    {
      // 타겟 제스처만 반응
      if (result.Type == _targetGesture && result.IsDetected)
      {
        _isActive = true;
        Debug.Log($"[GestureUIController] {_targetGesture} detected - activating indicator");
      }
      else
      {
        _isActive = false;
        if (result.Type != GestureType.None)
        {
          Debug.Log($"[GestureUIController] Detected {result.Type}, but target is {_targetGesture}");
        }
      }
    }

    /// <summary>
    /// 타겟 제스처 설정 (런타임 설정 가능)
    /// </summary>
    public void SetTargetGesture(GestureType targetGesture)
    {
      _targetGesture = targetGesture;
      Debug.Log($"[GestureUIController] Target gesture changed to: {targetGesture}");
    }

    /// <summary>
    /// 인디케이터 강제 활성화 (테스트용)
    /// </summary>
    public void SetActive(bool active)
    {
      _isActive = active;
    }

    /// <summary>
    /// 인디케이터 초기화
    /// </summary>
    private void InitializeIndicator()
    {
      _currentColor = _inactiveColor;
      _isActive = false;

      if (_gestureIndicator != null)
      {
        _gestureIndicator.color = _inactiveColor;
      }
    }

    /// <summary>
    /// 인디케이터 리셋
    /// </summary>
    public void ResetIndicator()
    {
      _isActive = false;
      InitializeIndicator();
    }

    // 디버그용: Inspector에서 테스트 가능
    [ContextMenu("Test Activate")]
    private void TestActivate()
    {
      SetActive(true);
    }

    [ContextMenu("Test Deactivate")]
    private void TestDeactivate()
    {
      SetActive(false);
    }
  }
}