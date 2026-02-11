using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 들어올리기 제스처 인식 Strategy
  /// - 양팔을 위로 들어올리는 동작
  /// - 이전 프레임과 비교하여 상승 모션 감지 (Y좌표 증가)
  /// </summary>
  public class LiftGestureStrategy : IGestureStrategy
  {
    public GestureType GestureType => GestureType.Wind;

    private float _risingThreshold;
    private int _risingMemory;

    // 이전 프레임 저장
    private NormalizedLandmark[] _previousPoseLandmarks;
    private int _risingFramesRemaining = 0;

    public void Initialize(GestureThresholdData thresholds)
    {
      _risingThreshold = thresholds.risingThreshold;
      _risingMemory = thresholds.risingMemory;
    }

    public GestureResult Recognize(
        HandLandmarkerResult handResult,
        PoseLandmarkerResult poseResult)
    {
      // 1. Pose 데이터 유효성 검사
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
      {
        ResetState();
        return GestureResult.None;
      }

      var poseLandmarks = poseResult.poseLandmarks[0];

      // 1-1. Pose landmark 개수 검증 (최소 17개 필요: 인덱스 0-16)
      if (poseLandmarks.landmarks.Count < 17)
      {
        Debug.LogWarning($"[LiftGesture] Insufficient pose landmarks: {poseLandmarks.landmarks.Count}/17");
        ResetState();
        return GestureResult.None;
      }

      // 2. 현재 손목 위치 (왼쪽: 15, 오른쪽: 16)
      var leftWrist = GetVector3(poseLandmarks.landmarks[15]);
      var rightWrist = GetVector3(poseLandmarks.landmarks[16]);

      // 3. 상승 모션 감지 (이전 프레임과 비교)
      bool isRisingMotion = false;
      if (_previousPoseLandmarks != null && _previousPoseLandmarks.Length > 16)
      {
        var prevLeftWrist = GetVector3(_previousPoseLandmarks[15]);
        var prevRightWrist = GetVector3(_previousPoseLandmarks[16]);
        
        // Y축 증가량 계산 (위로 = 양수)
        float leftWristDelta = prevLeftWrist.y - leftWrist.y;
        float rightWristDelta = prevRightWrist.y - rightWrist.y;
        
        isRisingMotion = leftWristDelta > _risingThreshold && rightWristDelta > _risingThreshold;
      }

      // 4. 현재 프레임을 이전 프레임으로 저장
      _previousPoseLandmarks = new NormalizedLandmark[poseLandmarks.landmarks.Count];
      for (int i = 0; i < poseLandmarks.landmarks.Count; i++)
      {
        _previousPoseLandmarks[i] = poseLandmarks.landmarks[i];
      }

      // 5. 상승 상태 기억 (일정 프레임 동안 유지)
      if (isRisingMotion) 
      {
        _risingFramesRemaining = _risingMemory; // 카운터 리셋
      }
      else if (_risingFramesRemaining > 0)
      {
        _risingFramesRemaining--; // 카운터 감소
      }

      // 6. 최종 판정: 상승 상태 프레임 내에 있는가?
      bool detected = _risingFramesRemaining > 0;

      // Debug.Log($"[LiftUp] 손목: L({leftWrist.y:F3}) R({rightWrist.y:F3}) | 상승={isRisingMotion}, 기억={_risingFramesRemaining}, 최종={detected}");

      return detected 
          ? new GestureResult(GestureType.Lift, 1.0f, true, Vector3.up)
          : GestureResult.None;
    }

    /// <summary>
    /// 내부 상태 초기화
    /// </summary>
    private void ResetState()
    {
      _previousPoseLandmarks = null;
      _risingFramesRemaining = 0;
    }

    private Vector3 GetVector3(NormalizedLandmark landmark)
    {
      return new Vector3(landmark.x, landmark.y, landmark.z);
    }
  }
}


