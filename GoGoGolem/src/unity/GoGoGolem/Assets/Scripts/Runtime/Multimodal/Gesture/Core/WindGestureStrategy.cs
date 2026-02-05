using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

namespace Demo.GestureDetection
{
  /// <summary>
    /// 바람 제스처 인식 Strategy
    /// - 양손 감지
    /// - 양손의 손바닥이 앞(카메라)을 향함
    /// - 양손의 2D 방향 벡터가 반대 (100~180도)
    /// - 손목 위치 가까움
    /// - 손가락들이 펴져있음
    /// </summary>
  public class WindGestureStrategy : IGestureStrategy
  {
    public GestureType GestureType => GestureType.Wind;

    private float _forwardThreshold; // Z 방향 (카메라 향함)
    private float _minHandsAngle;     // 두 손 최소 2D 각도
    private float _maxHandsAngle;     // 두 손 최대 2D 각도
    private float _maxWristDistance; // 최대 손목 거리
    private float _fingerRatio;      // 손가락 펴짐 비율
    private int _minFingers;            // 최소 펴진 손가락 수

    public void Initialize(GestureThresholdData thresholds)
    {
      _forwardThreshold = thresholds.forwardThreshold;
      _minHandsAngle = thresholds.minHandsAngle;
      _maxHandsAngle = thresholds.maxHandsAngle;
      _maxWristDistance = thresholds.maxWristDistance;
      _fingerRatio = thresholds.fingerRatio;
      _minFingers = thresholds.minFingers;
    }

    public GestureResult Recognize(
        HandLandmarkerResult handResult, 
        PoseLandmarkerResult poseResult)
    {
      // 1. 선행 조건: 양손이 감지되는가?
      if (handResult.handLandmarks == null || handResult.handLandmarks.Count < 2)
      {
        return GestureResult.None;
      }

      // 2. 데이터 추출 - 각 손목, 중지 손끝, 손 방향 
      var wrist1 = GetVector3(handResult.handLandmarks[0].landmarks[0]);
      var middleTip1 = GetVector3(handResult.handLandmarks[0].landmarks[12]);
      Vector3 hand1_dir = (middleTip1 - wrist1).normalized;

      var wrist2 = GetVector3(handResult.handLandmarks[1].landmarks[0]);
      var middleTip2 = GetVector3(handResult.handLandmarks[1].landmarks[12]);
      Vector3 hand2_dir = (middleTip2 - wrist2).normalized;

      // 3. 조건 검사
      // 조건 1: 양손이 모두 앞(카메라)을 향하는가?
      bool bothHandsForward = (hand1_dir.z < _forwardThreshold) && (hand2_dir.z < _forwardThreshold);

      // 조건 2: 두 손의 2D 방향(X, Y)이 100~180도(반대 방향)인가?
      Vector2 hand1_dir_2D = new Vector2(hand1_dir.x, hand1_dir.y).normalized;
      Vector2 hand2_dir_2D = new Vector2(hand2_dir.x, hand2_dir.y).normalized;
      float angle_2D = Vector2.Angle(hand1_dir_2D, hand2_dir_2D);
      bool angleIsOpposite_2D = (angle_2D >= _minHandsAngle) && (angle_2D <= _maxHandsAngle);

      // 조건 3: 두 손목의 위치가 가까운가?
      float wristDistance = Vector3.Distance(wrist1, wrist2);
      bool wristsClose = wristDistance < _maxWristDistance;

      // 조건 4: 손가락들이 펴져있는가?
      bool hand1FingersExtended = AreFingersExtended(handResult, 0);
      bool hand2FingersExtended = AreFingersExtended(handResult, 1);
      bool bothHandsFingerExtended = hand1FingersExtended && hand2FingersExtended;
      
      // 최종 조건
      bool success = bothHandsForward && angleIsOpposite_2D && wristsClose && bothHandsFingerExtended;
      
      return success 
          ? new GestureResult(GestureType.Wind, 1.0f, true, Vector3.forward)
          : GestureResult.None;
    }

    /// <summary>
    /// 손가락들이 펴져있는지 확인하는 메소드
    /// - 각 손가락의 TIP이 MCP보다 Wrist에서 멀리 있어야 함
    /// - 받아온 threshold 따라 조정
    /// </summary>
    private bool AreFingersExtended(HandLandmarkerResult handResult, int handIndex)
    { 
      if (handResult.handLandmarks == null || handIndex >= handResult.handLandmarks.Count)
      {
        return false;
      }

      var handLandmarks = handResult.handLandmarks[handIndex];
      var wrist = GetVector3(handLandmarks.landmarks[0]);

      int[] fingerTips = {4, 8, 12, 16, 20};
      int[] fingerMCPs = {2, 5, 9, 13, 17};
      int extendedCount = 0;

      for(int i = 0; i < fingerTips.Length; i++) // 각 손가락 확인
      {
        var fingerTip = GetVector3(handLandmarks.landmarks[fingerTips[i]]);
        var fingerMCP = GetVector3(handLandmarks.landmarks[fingerMCPs[i]]);

        float tipToWrist = Vector3.Distance(fingerTip, wrist);
        float mcpToWrist = Vector3.Distance(fingerMCP, wrist);

        if(tipToWrist > mcpToWrist * _fingerRatio)
        {
          extendedCount++;
        }
      }

      bool result = extendedCount >= _minFingers;
      // Debug.Log($"[AreFingersExtended] Hand {handIndex}: {extendedCount}/{totalFingers} fingers extended → {result}");
      return result;
    }

    private Vector3 GetVector3(NormalizedLandmark landmark)
    {
      return new Vector3(landmark.x, landmark.y, landmark.z);
    }

  }
}
