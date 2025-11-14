using UnityEngine;
using UnityEngine.UI;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 제스처 인식 결과를 UI에 표시하는 컨트롤러
  /// </summary>
  public class GestureUIController : MonoBehaviour
  {
    [Header("Gesture Indicators")]
    [SerializeField] private Image _jangpoongIndicator;  // 장풍 인디케이터
    [SerializeField] private Image _liftUpIndicator;     // 들어올리기 인디케이터

    [Header("Indicator Colors")]
    [SerializeField] private Color _inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f); // 비활성 색상 (어두운 회색)
    [SerializeField] private Color _activeColor = new Color(0f, 1f, 0f, 1f);           // 활성 색상 (밝은 녹색)
    [SerializeField] private Color _detectedColor = new Color(1f, 1f, 0f, 1f);        // 감지 중 색상 (노란색)

    [Header("Animation Settings")]
    [SerializeField] private float _fadeSpeed = 5f;        // 페이드 속도
    [SerializeField] private float _pulseSpeed = 2f;       // 펄스 속도
    [SerializeField] private float _pulseIntensity = 0.2f; // 펄스 강도

    private Color _currentJangpoongColor;
    private Color _currentLiftUpColor;
    private bool _isJangpoongActive;
    private bool _isLiftUpActive;
    private float _pulseTime;

    private void Start()
    {
      InitializeIndicators();
    }

    private void Update()
    {
      // 펄스 애니메이션을 위한 시간 업데이트
      _pulseTime += Time.deltaTime * _pulseSpeed;

      // 색상 부드럽게 전환
      if (_jangpoongIndicator != null)
      {
        Color targetColor = _isJangpoongActive ? _activeColor : _inactiveColor;
        _currentJangpoongColor = Color.Lerp(_currentJangpoongColor, targetColor, Time.deltaTime * _fadeSpeed);
        
        // 활성화 시 펄스 효과
        if (_isJangpoongActive)
        {
          float pulse = Mathf.Sin(_pulseTime) * _pulseIntensity;
          _jangpoongIndicator.color = _currentJangpoongColor * (1f + pulse);
        }
        else
        {
          _jangpoongIndicator.color = _currentJangpoongColor;
        }
      }

      if (_liftUpIndicator != null)
      {
        Color targetColor = _isLiftUpActive ? _activeColor : _inactiveColor;
        _currentLiftUpColor = Color.Lerp(_currentLiftUpColor, targetColor, Time.deltaTime * _fadeSpeed);
        
        // 활성화 시 펄스 효과
        if (_isLiftUpActive)
        {
          float pulse = Mathf.Sin(_pulseTime) * _pulseIntensity;
          _liftUpIndicator.color = _currentLiftUpColor * (1f + pulse);
        }
        else
        {
          _liftUpIndicator.color = _currentLiftUpColor;
        }
      }
    }

    /// <summary>
    /// 제스처 인식 결과 업데이트
    /// </summary>
    public void UpdateGestureResult(GestureResult result)
    {
      Debug.Log($"[GestureUIController] UpdateGestureResult called: {result.Type}, IsDetected: {result.IsDetected}");

      switch (result.Type)
      {
        case GestureType.BothHandsDetected:
          // 테스트: 양손 인디케이터 모두 켜기
          _isJangpoongActive = true;
          _isLiftUpActive = true;
          Debug.Log("[GestureUIController] ✅ Both hands detected - activating both indicators");
          break;

        case GestureType.Jangpoong:
          _isJangpoongActive = result.IsDetected;
          _isLiftUpActive = false;
          Debug.Log($"[GestureUIController] Jangpoong: {_isJangpoongActive}");
          break;
        
        case GestureType.LiftUp:
          _isLiftUpActive = result.IsDetected;
          _isJangpoongActive = false;
          Debug.Log($"[GestureUIController] LiftUp: {_isLiftUpActive}");
          break;
        
        case GestureType.None:
        default:
          // 제스처가 없으면 모두 비활성화 (단, 부드럽게 페이드 아웃)
          _isJangpoongActive = false;
          _isLiftUpActive = false;
          Debug.Log("[GestureUIController] No gesture - deactivating indicators");
          break;
      }

      Debug.Log($"[GestureUIController] Final state - Jangpoong: {_isJangpoongActive}, LiftUp: {_isLiftUpActive}");
    }

    /// <summary>
    /// 특정 제스처 인디케이터 강제 활성화 (테스트용)
    /// </summary>
    public void SetJangpoongActive(bool active)
    {
      _isJangpoongActive = active;
    }

    public void SetLiftUpActive(bool active)
    {
      _isLiftUpActive = active;
    }

    /// <summary>
    /// 인디케이터 초기화
    /// </summary>
    private void InitializeIndicators()
    {
      _currentJangpoongColor = _inactiveColor;
      _currentLiftUpColor = _inactiveColor;
      _isJangpoongActive = false;
      _isLiftUpActive = false;

      if (_jangpoongIndicator != null)
      {
        _jangpoongIndicator.color = _inactiveColor;
      }

      if (_liftUpIndicator != null)
      {
        _liftUpIndicator.color = _inactiveColor;
      }
    }

    /// <summary>
    /// 모든 인디케이터 리셋
    /// </summary>
    public void ResetAllIndicators()
    {
      _isJangpoongActive = false;
      _isLiftUpActive = false;
      InitializeIndicators();
    }

    // 디버그용: Inspector에서 테스트 가능하도록
    [ContextMenu("Test Jangpoong")]
    private void TestJangpoong()
    {
      UpdateGestureResult(new GestureResult(GestureType.Jangpoong, 1f, true));
    }

    [ContextMenu("Test LiftUp")]
    private void TestLiftUp()
    {
      UpdateGestureResult(new GestureResult(GestureType.LiftUp, 1f, true));
    }

    [ContextMenu("Reset")]
    private void TestReset()
    {
      ResetAllIndicators();
    }
  }
}