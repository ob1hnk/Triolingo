using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;

namespace Demo.GestureDetection
{
  /// <summary>
  /// Hand와 Pose landmark를 분석하여 제스처를 인식하는 클래스
  /// Strategy 패턴으로 제스처별 로직 분리
  /// </summary>
  public class GestureRecognizer
  {
    // Strategy 패턴
    private IGestureStrategy _currentStrategy;
    private GestureType _currentGestureType = GestureType.None;

    // Threshold 데이터
    private GestureThresholdData _thresholds;
    
    // 프레임 카운터 (안정성 확보)
    private Dictionary<GestureType, int> _gestureFrameCount = new Dictionary<GestureType, int>();
    private Dictionary<GestureType, int> _gestureLostCount = new Dictionary<GestureType, int>();

    // 설정값
    private int _holdFrames;
    private int _maxLostFrames;

    /// <summary>
    /// 생성자: Threshold 데이터 초기화
    /// </summary>
    public GestureRecognizer(GestureThresholdData thresholds = null)
    {
      // Threshold 설정
      _thresholds = thresholds ?? GestureThresholdData.Default();
      _holdFrames = _thresholds.holdFrames;
      _maxLostFrames = _thresholds.maxLostFrames;
      
      // 카운터 초기화
      _gestureFrameCount[GestureType.Wind] = 0;
      _gestureFrameCount[GestureType.Lift] = 0;
      _gestureLostCount[GestureType.Wind] = 0;
      _gestureLostCount[GestureType.Lift] = 0;
      
      Debug.Log($"[GestureRecognizer] Initialized with holdFrames={_holdFrames}, maxLostFrames={_maxLostFrames}");
    }

    /// <summary>
    /// 활성 제스처 설정 (Strategy 교체)
    /// </summary>
    public void SetActiveGesture(GestureType gestureType)
    {
      if (gestureType == GestureType.None)
      {
        Debug.LogWarning("[GestureRecognizer] Cannot set GestureType.None as active gesture");
        return;
      }

      if (_currentGestureType == gestureType)
      {
        Debug.Log($"[GestureRecognizer] Gesture {gestureType} is already active");
        return;
      }

      // 이전 제스처 카운터 리셋
      if (_currentGestureType != GestureType.None)
      {
        _gestureFrameCount[_currentGestureType] = 0;
        _gestureLostCount[_currentGestureType] = 0;
      }

      // 새 Strategy 생성
      _currentStrategy = GestureStrategyFactory.Create(gestureType, _thresholds);
      _currentGestureType = gestureType;
      
      Debug.Log($"[GestureRecognizer] Active gesture changed to: {gestureType}");
    }

    /// <summary>
    /// 제스처 인식 메인 함수 (Strategy 기반)
    /// </summary>
    public GestureResult RecognizeGesture(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // Strategy가 설정되지 않았으면 None 반환
      if (_currentStrategy == null)
      {
        Debug.LogWarning("[GestureRecognizer] No active gesture strategy set");
        return GestureResult.None;
      }

      // 1. Strategy로 제스처 판정 (true/false만)
      var rawResult = _currentStrategy.Recognize(handResult, poseResult);
      
      // 2. 프레임 카운터 로직 적용
      if (rawResult.IsDetected)
      {
        // 성공 카운트 증가
        _gestureFrameCount[_currentGestureType]++;
        _gestureLostCount[_currentGestureType] = 0; // 실패 카운터 리셋
        
        // holdFrames 이상 유지되면 최종 성공
        if (_gestureFrameCount[_currentGestureType] >= _holdFrames)
        {
          // confidence 계산: 프레임 카운트 기반
          float confidence = Mathf.Min(1f, _gestureFrameCount[_currentGestureType] / (float)_holdFrames);
          
          Debug.Log($"[GestureRecognizer] {_currentGestureType} detected! Count={_gestureFrameCount[_currentGestureType]}, Confidence={confidence:F2}");
          
          return new GestureResult(_currentGestureType, confidence, true, rawResult.Direction);
        }
      }
      else
      {
        // 실패: 유예 카운터 증가
        _gestureLostCount[_currentGestureType]++;
        
        // 유예 프레임 초과 시 성공 카운터 리셋
        if (_gestureLostCount[_currentGestureType] > _maxLostFrames)
        {
          _gestureFrameCount[_currentGestureType] = 0;
          // Debug.Log($"[GestureRecognizer] {_currentGestureType} lost for {_maxLostFrames} frames, resetting counter");
        }
      }

      return GestureResult.None;
    }

    /// <summary>
    /// 현재 활성 제스처 타입 반환
    /// </summary>
    public GestureType GetCurrentGestureType()
    {
      return _currentGestureType;
    }

    /// <summary>
    /// 카운터 수동 리셋 (테스트용)
    /// </summary>
    public void ResetCounters()
    {
      foreach (var key in _gestureFrameCount.Keys)
      {
        _gestureFrameCount[key] = 0;
        _gestureLostCount[key] = 0;
      }
      Debug.Log("[GestureRecognizer] All counters reset");
    }

    /// <summary>
    /// Threshold 업데이트 (런타임 조정용)
    /// </summary>
    public void UpdateThresholds(GestureThresholdData newThresholds)
    {
      _thresholds = newThresholds;
      _holdFrames = _thresholds.holdFrames;
      _maxLostFrames = _thresholds.maxLostFrames;
      
      // 현재 Strategy 재초기화
      if (_currentStrategy != null)
      {
        _currentStrategy.Initialize(_thresholds);
        Debug.Log($"[GestureRecognizer] Thresholds updated for {_currentGestureType}");
      }
    }
  }
}