using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using System.Collections.Generic;

namespace Demo.GestureDetection
{
  /// <summary>
  /// Hand와 Pose landmark를 분석하여 제스처를 인식하는 클래스
  /// </summary>
  public class GestureRecognizer
  {
    private readonly float _detectionThreshold;
    private readonly int _holdFrames;
    
    // 제스처 유지 카운터
    private Dictionary<GestureType, int> _gestureFrameCount = new Dictionary<GestureType, int>();
    
    // 이전 프레임 landmark 저장 (모션 감지용)
    private NormalizedLandmark[] _previousPoseLandmarks;

    public GestureRecognizer(float detectionThreshold = 0.7f, int holdFrames = 5)
    {
      _detectionThreshold = detectionThreshold;
      _holdFrames = holdFrames;
      
      _gestureFrameCount[GestureType.BothHandsDetected] = 0;
      _gestureFrameCount[GestureType.Jangpoong] = 0;
      _gestureFrameCount[GestureType.LiftUp] = 0;
    }

    /// <summary>
    /// 제스처 인식 메인 함수
    /// </summary>
    public GestureResult RecognizeGesture(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // 디버그: 매개변수 체크
      Debug.Log($"[GestureRecognizer] RecognizeGesture called!");
      Debug.Log($"  Hand landmarks count: {handResult.handLandmarks?.Count ?? 0}");
      Debug.Log($"  Pose landmarks count: {poseResult.poseLandmarks?.Count ?? 0}");

      // 테스트 제스처는 Hand만 체크 (Pose 무관)
      var testResult = DetectBothHands(handResult);
      if (testResult.IsDetected)
      {
        Debug.Log("[GestureRecognizer] ✅ TEST GESTURE SUCCEEDED");
        _gestureFrameCount[GestureType.Jangpoong] = 0;
        _gestureFrameCount[GestureType.LiftUp] = 0;
        return testResult;
      }

      // 나머지 제스처는 Hand와 Pose 둘 다 필요
      if (handResult.handLandmarks == null || handResult.handLandmarks.Count == 0 ||
          poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
      {
        Debug.Log("[GestureRecognizer] No landmarks detected for complex gestures - resetting");
        _gestureFrameCount[GestureType.Jangpoong] = 0;
        _gestureFrameCount[GestureType.LiftUp] = 0;
        return GestureResult.None;
      }

      Debug.Log("[GestureRecognizer] Landmarks detected! Checking complex gestures...");

      // 장풍 제스처 검사 (우선순위 2)
      var jangpoongResult = DetectJangpoong(handResult, poseResult);
      if (jangpoongResult.IsDetected)
      {
        _gestureFrameCount[GestureType.BothHandsDetected] = 0;
        _gestureFrameCount[GestureType.LiftUp] = 0;
        return jangpoongResult;
      }

      // 들어올리기 제스처 검사 (우선순위 3)
      var liftUpResult = DetectLiftUp(poseResult);
      if (liftUpResult.IsDetected)
      {
        _gestureFrameCount[GestureType.BothHandsDetected] = 0;
        _gestureFrameCount[GestureType.Jangpoong] = 0;
        return liftUpResult;
      }

      Debug.Log("[GestureRecognizer] No gesture detected this frame");
      _gestureFrameCount[GestureType.Jangpoong] = 0;
      _gestureFrameCount[GestureType.LiftUp] = 0;
      return GestureResult.None;
    }

    /// <summary>
    /// 테스트 제스처: 양손이 감지되기만 하면 성공
    /// </summary>
    private GestureResult DetectBothHands(HandLandmarkerResult handResult)
    {
      int handCount = handResult.handLandmarks?.Count ?? 0;
      Debug.Log($"[DetectBothHands] Hand count: {handCount}, Frame counter: {_gestureFrameCount[GestureType.BothHandsDetected]}/{_holdFrames}");

      // 양손이 감지되는지만 체크
      if (handResult.handLandmarks != null && handResult.handLandmarks.Count >= 2)
      {
        _gestureFrameCount[GestureType.BothHandsDetected]++;
        Debug.Log($"[DetectBothHands] ✅ Both hands detected! Counter: {_gestureFrameCount[GestureType.BothHandsDetected]}/{_holdFrames}");
        
        if (_gestureFrameCount[GestureType.BothHandsDetected] >= _holdFrames)
        {
          Debug.Log("TEST GESTURE DETECTED! Both hands visible");
          float confidence = 1.0f;
          return new GestureResult(GestureType.BothHandsDetected, confidence, true, Vector3.zero);
        }
      }
      else
      {
        if (_gestureFrameCount[GestureType.BothHandsDetected] > 0)
        {
          Debug.Log($"[DetectBothHands] ❌ Lost hands! Resetting counter from {_gestureFrameCount[GestureType.BothHandsDetected]}");
        }
        _gestureFrameCount[GestureType.BothHandsDetected] = 0;
      }

      return GestureResult.None;
    }

    /// <summary>
    /// 장풍 제스처 감지: 양손을 앞으로 밀어내는 동작
    /// - 양손의 손바닥이 앞을 향함 (손목에서 중지 끝까지의 벡터가 앞방향)
    /// - 양팔이 펴진 상태 (팔꿈치가 어깨와 손목 사이에 거의 일직선)
    /// - 손의 높이가 어깨~가슴 사이
    /// </summary>
    private GestureResult DetectJangpoong(HandLandmarkerResult handResult, PoseLandmarkerResult poseResult)
    {
      // 양손이 모두 감지되어야 함
      if (handResult.handLandmarks == null || handResult.handLandmarks.Count < 2)
      {
        _gestureFrameCount[GestureType.Jangpoong] = 0;
        return GestureResult.None;
      }

      // Pose landmark가 있어야 함
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
      {
        _gestureFrameCount[GestureType.Jangpoong] = 0;
        return GestureResult.None;
      }

      var poseLandmarks = poseResult.poseLandmarks[0];
      
      // 어깨 위치 (왼쪽: 11, 오른쪽: 12)
      var leftShoulder = GetVector3(poseLandmarks.landmarks[11]);
      var rightShoulder = GetVector3(poseLandmarks.landmarks[12]);
      var shoulderCenter = (leftShoulder + rightShoulder) / 2f;
      
      // 팔꿈치 위치 (왼쪽: 13, 오른쪽: 14)
      var leftElbow = GetVector3(poseLandmarks.landmarks[13]);
      var rightElbow = GetVector3(poseLandmarks.landmarks[14]);
      
      // 손목 위치 (왼쪽: 15, 오른쪽: 16)
      var leftWrist = GetVector3(poseLandmarks.landmarks[15]);
      var rightWrist = GetVector3(poseLandmarks.landmarks[16]);

      bool bothHandsForward = true;
      bool bothArmsExtended = true;
      bool handsAtChestHeight = true;

      foreach (var handLandmark in handResult.handLandmarks)
      {
        // 손목(0)과 중지 끝(12) 벡터로 손바닥 방향 판단
        var wrist = GetVector3(handLandmark.landmarks[0]);
        var middleFingerTip = GetVector3(handLandmark.landmarks[12]);
        var handDirection = (middleFingerTip - wrist).normalized;

        // 손바닥이 앞을 향하는지 (z 방향이 음수 = 카메라를 향함)
        if (handDirection.z > -0.3f) // 임계값 조정 가능
        {
          bothHandsForward = false;
          break;
        }

        // 손의 높이가 어깨 근처인지 확인
        if (Mathf.Abs(wrist.y - shoulderCenter.y) > 0.3f)
        {
          handsAtChestHeight = false;
        }
      }

      // 팔이 펴져있는지 확인 (어깨-팔꿈치-손목이 거의 일직선)
      float leftArmAngle = Vector3.Angle(leftElbow - leftShoulder, leftWrist - leftElbow);
      float rightArmAngle = Vector3.Angle(rightElbow - rightShoulder, rightWrist - rightElbow);
      
      if (leftArmAngle < 150f || rightArmAngle < 150f) // 거의 180도에 가까워야 함
      {
        bothArmsExtended = false;
      }

      // 모든 조건이 충족되면 프레임 카운터 증가
      if (bothHandsForward && bothArmsExtended && handsAtChestHeight)
      {
        _gestureFrameCount[GestureType.Jangpoong]++;
        
        if (_gestureFrameCount[GestureType.Jangpoong] >= _holdFrames)
        {
          float confidence = Mathf.Min(1f, _gestureFrameCount[GestureType.Jangpoong] / (float)_holdFrames);
          return new GestureResult(GestureType.Jangpoong, confidence, true, Vector3.forward);
        }
      }
      else
      {
        _gestureFrameCount[GestureType.Jangpoong] = 0;
      }

      return GestureResult.None;
    }

    /// <summary>
    /// 들어올리기 제스처 감지: 양팔을 위로 들어올리는 동작
    /// - 양 손목의 Y 좌표가 어깨보다 위에 있음
    /// - 팔꿈치도 어깨 높이 이상
    /// - 이전 프레임보다 Y 좌표가 증가 (상승 모션)
    /// </summary>
    private GestureResult DetectLiftUp(PoseLandmarkerResult poseResult)
    {
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
      {
        _gestureFrameCount[GestureType.LiftUp] = 0;
        return GestureResult.None;
      }

      var poseLandmarks = poseResult.poseLandmarks[0];
      
      // 어깨 위치 (왼쪽: 11, 오른쪽: 12)
      var leftShoulder = GetVector3(poseLandmarks.landmarks[11]);
      var rightShoulder = GetVector3(poseLandmarks.landmarks[12]);
      
      // 팔꿈치 위치 (왼쪽: 13, 오른쪽: 14)
      var leftElbow = GetVector3(poseLandmarks.landmarks[13]);
      var rightElbow = GetVector3(poseLandmarks.landmarks[14]);
      
      // 손목 위치 (왼쪽: 15, 오른쪽: 16)
      var leftWrist = GetVector3(poseLandmarks.landmarks[15]);
      var rightWrist = GetVector3(poseLandmarks.landmarks[16]);

      // 양 손목이 어깨보다 위에 있는지
      bool wristsAboveShoulders = leftWrist.y < leftShoulder.y && rightWrist.y < rightShoulder.y; // Y축은 위가 작은 값
      
      // 양 팔꿈치가 어깨 높이 이상인지
      bool elbowsRaised = leftElbow.y <= leftShoulder.y && rightElbow.y <= rightShoulder.y;

      // 상승 모션 감지 (이전 프레임과 비교)
      bool isRisingMotion = false;
      if (_previousPoseLandmarks != null && _previousPoseLandmarks.Length > 16)
      {
        var prevLeftWrist = GetVector3(_previousPoseLandmarks[15]);
        var prevRightWrist = GetVector3(_previousPoseLandmarks[16]);
        
        float leftWristDelta = prevLeftWrist.y - leftWrist.y; // Y축 증가량 (위로 = 음수 -> 양수)
        float rightWristDelta = prevRightWrist.y - rightWrist.y;
        
        isRisingMotion = leftWristDelta > 0.01f && rightWristDelta > 0.01f;
      }

      // 현재 프레임을 이전 프레임으로 저장
      _previousPoseLandmarks = new NormalizedLandmark[poseLandmarks.landmarks.Count];
      for (int i = 0; i < poseLandmarks.landmarks.Count; i++)
      {
        _previousPoseLandmarks[i] = poseLandmarks.landmarks[i];
      }

      // 모든 조건이 충족되면 프레임 카운터 증가
      if (wristsAboveShoulders && elbowsRaised)
      {
        _gestureFrameCount[GestureType.LiftUp]++;
        
        if (_gestureFrameCount[GestureType.LiftUp] >= _holdFrames)
        {
          float confidence = Mathf.Min(1f, _gestureFrameCount[GestureType.LiftUp] / (float)_holdFrames);
          return new GestureResult(GestureType.LiftUp, confidence, true, Vector3.up);
        }
      }
      else
      {
        _gestureFrameCount[GestureType.LiftUp] = 0;
      }

      return GestureResult.None;
    }

    private void ResetGestureCounters()
    {
      _gestureFrameCount[GestureType.BothHandsDetected] = 0;
      _gestureFrameCount[GestureType.Jangpoong] = 0;
      _gestureFrameCount[GestureType.LiftUp] = 0;
    }

    private Vector3 GetVector3(NormalizedLandmark landmark)
    {
      return new Vector3(landmark.x, landmark.y, landmark.z);
    }
  }
}