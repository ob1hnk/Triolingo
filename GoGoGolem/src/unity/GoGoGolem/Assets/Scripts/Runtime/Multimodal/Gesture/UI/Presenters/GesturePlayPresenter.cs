using System.Collections.Generic;
using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 제스처 인식 타임아웃 종류
  /// </summary>
  public enum GestureTimeoutType
  {
    NoHands,   // A: 손이 일정 시간 동안 아예 안 보임 (위치 재조정 유도)
    HoldFail   // B: 손은 보이지만 일정 시간 동안 유지 성공을 못함 (넘어가기 유도)
  }

  /// <summary>
  /// 타임아웃 발생 정보 (Presenter → Controller)
  /// </summary>
  public struct GestureTimeoutInfo
  {
    public GestureTimeoutType Type;
    public GestureFailReason Reason; // HoldFail일 때 "가장 큰 실패 이유"

    public GestureTimeoutInfo(GestureTimeoutType type, GestureFailReason reason)
    {
      Type = type;
      Reason = reason;
    }
  }

  /// <summary>
  /// 제스처 플레이 로직 처리 (Presenter)
  /// - GestureDetector 이벤트 구독
  /// - GestureRecognizer 호출
  /// - View 업데이트 (View 내부 구조는 모름)
  /// - 제스처 일정 시간 유지 시 성공 콜백
  /// - 인식 실패가 길어지면 타임아웃 콜백(A: 손 미검출 / B: 유지 실패)
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

    // 성공 판정 (3초 유지)
    private float _holdStartTime = -1f;
    private float _requiredHoldDuration = 3f;
    private float _progressShowThreshold = 2f;
    private bool _successTriggered = false;
    
    // 성공 콜백
    private System.Action<GestureType> _onGestureSuccess;

    // ── 실패 타임아웃 ──
    private System.Action<GestureTimeoutInfo> _onTimeout;
    private float _noHandTimeout = 5f;    // A: 손 미검출 허용 시간
    private float _holdFailTimeout = 15f;  // B: 유지 실패 허용 시간
    private bool _sessionStarted = false;  // 양손이 처음 잡힌 이후 true
    private float _sessionStartTime = -1f;
    private float _lastHandSeenTime = -1f;
    private bool _timedOut = false;
    private bool _isPaused = false;
    private bool _timersSuspended = false; // 설정창 등 비-게임플레이 상태에서 타이머만 중단

    // 실패 이유별 누적 카운트 (고정 키 7개 — 컬렉션이 커지지 않음)
    private static readonly GestureFailReason[] _allReasons =
    {
      GestureFailReason.PalmsNotForward,
      GestureFailReason.HandsNotOpposite,
      GestureFailReason.WristsTooFar,
      GestureFailReason.FingersNotOpen,
      GestureFailReason.NotRising,
      GestureFailReason.PoseMissing,
      GestureFailReason.HandMissing,
    };
    private readonly Dictionary<GestureFailReason, int> _failCounts = new Dictionary<GestureFailReason, int>();

    /// <summary>
    /// 초기화
    /// </summary>
    public void Initialize(
      GesturePlayView view,
      GestureDetector detector,
      GestureType targetGesture,
      GestureThresholdData thresholds,
      System.Action<GestureType> onSuccess,
      float requiredHoldDuration = 3f,
      float progressShowThreshold = 2f,
      System.Action<GestureTimeoutInfo> onTimeout = null,
      float noHandTimeout = 5f,
      float holdFailTimeout = 15f)
    {
      _view = view;
      _gestureDetector = detector;
      _targetGesture = targetGesture;
      _onGestureSuccess = onSuccess;
      _requiredHoldDuration = requiredHoldDuration;
      _progressShowThreshold = progressShowThreshold;
      _onTimeout = onTimeout;
      _noHandTimeout = noHandTimeout;
      _holdFailTimeout = holdFailTimeout;

      // 상태 초기화
      _holdStartTime = -1f;
      _successTriggered = false;
      ResetTimeoutState();

      // GestureRecognizer 생성 및 설정
      _gestureRecognizer = new GestureRecognizer(thresholds ?? GestureThresholdData.Default());
      _gestureRecognizer.SetActiveGesture(targetGesture);
      
      // GestureDetector 이벤트 구독
      if (_gestureDetector != null)
      {
        _gestureDetector.OnLandmarksUpdated += OnLandmarksUpdated;
      }
      
      UnityEngine.Debug.Log($"[GesturePlayPresenter] Initialized - Target: {targetGesture}, HoldDuration: {requiredHoldDuration}s");
    }
    
    /// <summary>
    /// 플레이 시작
    /// </summary>
    public void StartPlay()
    {
      if (_gestureDetector != null)
      {
        _gestureDetector.Play();
        UnityEngine.Debug.Log("[GesturePlayPresenter] Play started");
      }
    }
    
    /// <summary>
    /// Landmark 데이터 업데이트 콜백 (GestureDetector에서 호출)
    /// </summary>
    private void OnLandmarksUpdated(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // 일시정지 중(타임아웃 팝업 표시 중)에는 들어오는 잔여 이벤트 무시
      if (_isPaused)
      {
        DisposeAllMasks(poseResult);
        return;
      }

      // 1. 데이터 유효성 검사
      bool hasHandData = IsValidHandData(handResult);
      bool hasPoseData = poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0;

      // 2. 세션 시작 / 손 추적 갱신 (양손이 보이는 순간부터 타이머 가동)
      float now = Time.time;
      if (hasHandData)
      {
        _lastHandSeenTime = now;
        if (!_sessionStarted)
        {
          _sessionStarted = true;
          _sessionStartTime = now;
          UnityEngine.Debug.Log("[GesturePlayPresenter] Session started (both hands detected)");
        }
      }

      // 3. 데이터 부족 분기
      if (!hasHandData || !hasPoseData)
      {
        // 실패 이유 집계 (B 타임아웃 텍스트용)
        AccumulateFailReason(hasHandData, hasPoseData, GestureFailReason.None);

        // 데이터 부족 시 홀드 타이머 리셋
        ResetHoldTimer();

        _view?.UpdateDisplay(new DisplayData { HasValidData = false });

        CheckTimeouts(now);
        DisposeAllMasks(poseResult);
        return;
      }

      // 4. 제스처 인식
      var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);

      // 5. 타겟 제스처 유지 판정
      bool isTargetDetected = gestureResult.Type == _targetGesture && gestureResult.IsDetected;

      if (isTargetDetected && !_successTriggered)
      {
        // 홀드 시작 또는 유지
        if (_holdStartTime < 0f)
        {
          _holdStartTime = now;
          UnityEngine.Debug.Log($"[GesturePlayPresenter] Hold started: {gestureResult.Type}");
        }
        else
        {
          float holdDuration = now - _holdStartTime;

          // 요구 시간 도달 시 성공 콜백
          if (holdDuration >= _requiredHoldDuration)
          {
            _successTriggered = true;
            UnityEngine.Debug.Log($"[GesturePlayPresenter] Gesture SUCCESS! Held for {holdDuration:F1}s");
            _onGestureSuccess?.Invoke(gestureResult.Type);
          }
        }
      }
      else if (!isTargetDetected)
      {
        // 제스처 끊김 → 홀드 타이머 리셋 + 실패 이유 집계
        ResetHoldTimer();
        AccumulateFailReason(hasHandData, hasPoseData, gestureResult.FailReason);
      }

      // 6. View 업데이트 (단일 진입점)
      _view?.UpdateDisplay(new DisplayData
      {
        PoseData = poseResult,
        HandData = handResult,
        GestureResult = gestureResult,
        HasValidData = true,
        HoldProgress = CalculateHoldProgress(),
        ShowProgress = ShouldShowProgress()
      });

      // 7. 타임아웃 체크
      CheckTimeouts(now);

      // 8. 메모리 정리 (Pose segmentation masks)
      DisposeAllMasks(poseResult);
    }

    /// <summary>
    /// A/B 타임아웃 검사. 세션 시작 후, 미성공·미타임아웃 상태에서만 동작.
    /// </summary>
    private void CheckTimeouts(float now)
    {
      if (_timersSuspended || !_sessionStarted || _timedOut || _successTriggered) return;

      // A: 손 미검출 — 손이 마지막으로 보인 뒤 _noHandTimeout 경과
      if (_lastHandSeenTime >= 0f && (now - _lastHandSeenTime) >= _noHandTimeout)
      {
        _timedOut = true;
        UnityEngine.Debug.Log("[GesturePlayPresenter] Timeout A (NoHands)");
        _onTimeout?.Invoke(new GestureTimeoutInfo(GestureTimeoutType.NoHands, GestureFailReason.None));
        return;
      }

      // B: 유지 실패 — 세션 시작 후 _holdFailTimeout 동안 한 번도 성공 못함
      if ((now - _sessionStartTime) >= _holdFailTimeout)
      {
        _timedOut = true;
        var reason = GetDominantFailReason();
        UnityEngine.Debug.Log($"[GesturePlayPresenter] Timeout B (HoldFail), reason={reason}");
        _onTimeout?.Invoke(new GestureTimeoutInfo(GestureTimeoutType.HoldFail, reason));
      }
    }

    /// <summary>
    /// 실패 이유 누적 (고정 키 카운터 — 가장 큰 실패 이유 산출용)
    /// </summary>
    private void AccumulateFailReason(bool hasHandData, bool hasPoseData, GestureFailReason strategyReason)
    {
      if (_timersSuspended || !_sessionStarted || _successTriggered) return;

      if (!hasPoseData)
      {
        _failCounts[GestureFailReason.PoseMissing]++;
      }
      else if (!hasHandData)
      {
        // 손이 잠깐씩 사라짐 (5초 이상 지속되면 A로 별도 처리)
        _failCounts[GestureFailReason.HandMissing]++;
      }
      else
      {
        // 손/몸 다 보이는데 미검출 → 전략이 알려준 조건들 집계
        foreach (var r in _allReasons)
        {
          if ((strategyReason & r) != 0)
            _failCounts[r]++;
        }
      }
    }

    /// <summary>
    /// 누적된 실패 이유 중 최다 항목 반환 (없으면 None)
    /// </summary>
    private GestureFailReason GetDominantFailReason()
    {
      GestureFailReason best = GestureFailReason.None;
      int bestCount = 0;
      foreach (var r in _allReasons)
      {
        if (_failCounts[r] > bestCount)
        {
          bestCount = _failCounts[r];
          best = r;
        }
      }
      return best;
    }

    /// <summary>
    /// 세션/타이머/실패 카운트 초기화 (키도 함께 보장)
    /// </summary>
    private void ResetTimeoutState()
    {
      _sessionStarted = false;
      _sessionStartTime = -1f;
      _lastHandSeenTime = -1f;
      _timedOut = false;
      foreach (var r in _allReasons)
        _failCounts[r] = 0;
    }

    /// <summary>
    /// 홀드 타이머 리셋
    /// </summary>
    private void ResetHoldTimer()
    {
      if (_holdStartTime >= 0f)
      {
        UnityEngine.Debug.Log("[GesturePlayPresenter] Hold interrupted");
        _holdStartTime = -1f;
      }
    }
    
    /// <summary>
    /// 홀드 진행도 계산 (0.0 ~ 1.0)
    /// </summary>
    private float CalculateHoldProgress()
    {
      if (_holdStartTime < 0f || _successTriggered)
        return 0f;
      
      float elapsed = Time.time - _holdStartTime;
      return Mathf.Clamp01(elapsed / _requiredHoldDuration);
    }

    private bool ShouldShowProgress()
    {
      if(_holdStartTime < 0f || _successTriggered) return false;
      return (Time.time - _holdStartTime) >= _progressShowThreshold;
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
      UnityEngine.Debug.Log($"[GesturePlayPresenter] Target gesture changed to: {newGesture}");
    }
    
    /// <summary>
    /// 인식 일시정지 (타임아웃 팝업 표시 중).
    /// Detector 자체의 Pause()는 Controller가 호출 — 여기선 잔여 이벤트만 무시.
    /// </summary>
    public void PausePlay()
    {
      _isPaused = true;
    }

    /// <summary>
    /// 타임아웃 타이머만 일시중단/재개 (설정창 등 비-게임플레이 상태용).
    /// Detector는 계속 동작(웹캠 미리보기가 라이브 텍스처를 빌려쓰므로).
    /// 재개 시 중단 동안 흐른 시간을 제외하기 위해 세션/타이머를 리셋한다.
    /// </summary>
    public void SetTimersSuspended(bool suspended)
    {
      if (_timersSuspended == suspended) return;
      _timersSuspended = suspended;

      if (!suspended)
      {
        // 재개: 중단 동안의 경과를 무효화하고 새 세션으로 재시작
        _holdStartTime = -1f;
        ResetTimeoutState();
        UnityEngine.Debug.Log("[GesturePlayPresenter] Timers resumed (state back to Gameplay)");
      }
      else
      {
        UnityEngine.Debug.Log("[GesturePlayPresenter] Timers suspended (non-gameplay state)");
      }
    }

    /// <summary>
    /// 인식 재개 (다시해보기). 세션/타이머/실패 카운트를 모두 리셋하여
    /// 양손이 다시 잡히는 순간부터 새 세션으로 시작한다.
    /// </summary>
    public void ResumePlay()
    {
      _holdStartTime = -1f;
      ResetTimeoutState();
      _isPaused = false;
      UnityEngine.Debug.Log("[GesturePlayPresenter] Resumed (timers/fail-counts reset)");
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
      
      UnityEngine.Debug.Log("[GesturePlayPresenter] Cleaned up");
    }
  }
}