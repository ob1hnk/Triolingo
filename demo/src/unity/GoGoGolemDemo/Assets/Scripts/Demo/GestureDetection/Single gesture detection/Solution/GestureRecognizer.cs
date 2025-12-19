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
    
    // 제스처 실패(잃음) 유예 카운터
    private Dictionary<GestureType, int> _gestureLostCount = new Dictionary<GestureType, int>();
    // 몇 프레임까지 실패를 봐줄 것인가 (이 값이 작을수록 민감함)
    private readonly int _maxLostFrames = 3;

    // LiftUp 제스처 상승 상태 기억
    private int _liftUpRisingFramesRemaining = 0;

    // 이전 프레임 landmark 저장 (모션 감지용)
    private NormalizedLandmark[] _previousPoseLandmarks;

    // ==== Jangpoong 제스처 임계값 ====
    private float _jangpoongForwardThreshold = 0.0f; // Z 방향 (카메라 향함)
    private float _jangpoongMinHandsAngle = 100;     // 두 손 최소 2D 각도
    private float _jangpoongMaxHandsAngle = 180;     // 두 손 최대 2D 각도
    private float _jangpoongMaxWristDistance = 0.1f; // 최대 손목 거리
    private float _jangpoongFingerRatio = 1.2f;      // 손가락 펴짐 비율
    private int _jangpoongMinFingers = 5;            // 최소 펴진 손가락 수

    // ==== LiftUp 제스처 임계값 ====
    private float _liftUpRisingThreshold = 0.01f; // 상승 감지 임계값
    private const int _liftUpRisingMemory = 10; // 10프레임 동안 상승 상태 기억

    public GestureRecognizer(float detectionThreshold = 0.7f, int holdFrames = 5)
    {
      _detectionThreshold = detectionThreshold;
      _holdFrames = holdFrames;
      
      _gestureFrameCount[GestureType.Jangpoong] = 0;
      _gestureFrameCount[GestureType.LiftUp] = 0;

      _gestureLostCount[GestureType.Jangpoong] = 0;
      _gestureLostCount[GestureType.LiftUp] = 0;

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

      // 1. 장풍 제스처 검사 (HandLandmarkerResult만 필요)
      var jangpoongResult = DetectJangpoong(handResult);
      if (jangpoongResult.IsDetected)
      {
        _gestureFrameCount[GestureType.LiftUp] = 0;
        _gestureLostCount[GestureType.LiftUp] = 0;
        return jangpoongResult;
      }

      // 2. 들어올리기 제스처 검사 (PoseLandmarkerResult만 필요)
      if (poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0)
      {
        var liftUpResult = DetectLiftUp(poseResult);
        if (liftUpResult.IsDetected)
        {   
          _gestureFrameCount[GestureType.Jangpoong] = 0;
          _gestureFrameCount[GestureType.Jangpoong] = 0; // 다른 제스처 카운터 리셋
          return liftUpResult;
        }
      }

      // 3. 아무 제스처도 감지되지 않음
      Debug.Log("[GestureRecognizer] No gesture detected this frame");
      return GestureResult.None;
    }

    /// <summary>
    /// ⭐ 장풍 제스처 감지
    /// - 양손 감지
    /// - 양손의 손바닥이 앞(카메라)을 향함
    /// - 양손의 2D 방향 벡터가 반대 (100~180도)
    /// - 손목 위치 가까움
    /// - 손가락들이 펴져있음
    /// </summary>
    private GestureResult DetectJangpoong(HandLandmarkerResult handResult)
    {
      bool allConditionsMet = false;

      // 선행 조건. 양손이 감지되는가?
      if (handResult.handLandmarks != null && handResult.handLandmarks.Count >= 2)
      {
        // 계산 - 각 손목, 중지 손끝, 손 방향 
        var wrist1 = GetVector3(handResult.handLandmarks[0].landmarks[0]);
        var middleTip1 = GetVector3(handResult.handLandmarks[0].landmarks[12]);
        Vector3 hand1_dir = (middleTip1 - wrist1).normalized;

        var wrist2 = GetVector3(handResult.handLandmarks[1].landmarks[0]);
        var middleTip2 = GetVector3(handResult.handLandmarks[1].landmarks[12]);
        Vector3 hand2_dir = (middleTip2 - wrist2).normalized;

        // 조건 1: 양손이 모두 앞(카메라)을 향하는가?
        bool bothHandsForward = (hand1_dir.z < _jangpoongForwardThreshold) && (hand2_dir.z < _jangpoongForwardThreshold);

        // 조건 2: 두 손의 2D 방향(X, Y)이 100~180도(반대 방향)인가?
        Vector2 hand1_dir_2D = new Vector2(hand1_dir.x, hand1_dir.y).normalized;
        Vector2 hand2_dir_2D = new Vector2(hand2_dir.x, hand2_dir.y).normalized;
        float angle_2D = Vector2.Angle(hand1_dir_2D, hand2_dir_2D);
        bool angleIsOpposite_2D = (angle_2D >= _jangpoongMinHandsAngle) && (angle_2D <= _jangpoongMaxHandsAngle);

        // 조건 3: 두 손목의 위치가 가까운가?
        float wristDistance = Vector3.Distance(wrist1, wrist2);
        bool wristsClose = wristDistance < _jangpoongMaxWristDistance;

        // 조건 4: 손가락들이 펴져있는가?
        bool hand1FingersExtended = AreFingersExtended(handResult, 0);
        bool hand2FingersExtended = AreFingersExtended(handResult, 1);
        bool bothHandsFingerExtended = hand1FingersExtended && hand2FingersExtended;
        
        // 최종 조건
        allConditionsMet = bothHandsForward && angleIsOpposite_2D && wristsClose && bothHandsFingerExtended;

        Debug.Log($"[Jangpoong] Z: (H1: {hand1_dir.z:F2}, H2: {hand2_dir.z:F2}) | 2D Angle: {angle_2D:F2}");
        Debug.Log($"[Jangpoong] 조건: 앞={bothHandsForward}, 반대={angleIsOpposite_2D}, 가까움={wristsClose}, 손가락={bothHandsFingerExtended} | COUNT: {_gestureFrameCount[GestureType.Jangpoong]}");
      }

      /* ⭐ MediaPipe 이용 시 카운터 로직의 필요성
        현재 설정된 값(GestureHoldFrames으로는 5 frame 동안 제스처 인식이 되어야 제스처로 인정
        mediapipe가 중간에 1-2 frame 놓치면 끊김
      */
      // 실패 유예 로직
      if (allConditionsMet)
      {
        _gestureFrameCount[GestureType.Jangpoong]++;
        _gestureLostCount[GestureType.Jangpoong] = 0; // 성공 -> Lost 카운터 리셋
        
        if (_gestureFrameCount[GestureType.Jangpoong] >= _holdFrames)
        {
          float confidence = Mathf.Min(1f, _gestureFrameCount[GestureType.Jangpoong] / (float)_holdFrames);
          return new GestureResult(GestureType.Jangpoong, confidence, true, Vector3.forward);
        }
      }
      else // 실패 시
      {
        _gestureLostCount[GestureType.Jangpoong]++; // Lost 카운터 증가

        // 설정한 유예 프레임을 초과해서 실패 -> Frame 카운터 리셋
        if (_gestureLostCount[GestureType.Jangpoong] > _maxLostFrames)
        {
          _gestureFrameCount[GestureType.Jangpoong] = 0;
        }
      }

      return GestureResult.None;
    }

    /// <summary>
    /// ⭐ 들어올리기 제스처 감지: 양팔을 위로 들어올리는 동작
    /// - 이전 프레임보다 Y 좌표가 증가 (상승 모션)
    /// </summary>
    private GestureResult DetectLiftUp(PoseLandmarkerResult poseResult)
    {
      if (poseResult.poseLandmarks == null || poseResult.poseLandmarks.Count == 0)
      {
        _gestureFrameCount[GestureType.LiftUp] = 0;
        _gestureLostCount[GestureType.LiftUp] = 0;
        _liftUpRisingFramesRemaining = 0;
        return GestureResult.None;
      }

      var poseLandmarks = poseResult.poseLandmarks[0];
      
      // 손목 위치 (왼쪽: 15, 오른쪽: 16)
      var leftWrist = GetVector3(poseLandmarks.landmarks[15]);
      var rightWrist = GetVector3(poseLandmarks.landmarks[16]);

      // 조건 1. 상승 모션인가? (이전 프레임과 비교)
      bool isRisingMotion = false;
      if (_previousPoseLandmarks != null && _previousPoseLandmarks.Length > 16)
      {
        var prevLeftWrist = GetVector3(_previousPoseLandmarks[15]);
        var prevRightWrist = GetVector3(_previousPoseLandmarks[16]);
        
        float leftWristDelta = prevLeftWrist.y - leftWrist.y; // Y축 증가량 (위로 = 양수)
        float rightWristDelta = prevRightWrist.y - rightWrist.y;
        
        isRisingMotion = leftWristDelta > _liftUpRisingThreshold && rightWristDelta > _liftUpRisingThreshold;
      }

      // 현재 프레임을 이전 프레임으로 저장
      _previousPoseLandmarks = new NormalizedLandmark[poseLandmarks.landmarks.Count];
      for (int i = 0; i < poseLandmarks.landmarks.Count; i++)
      {
        _previousPoseLandmarks[i] = poseLandmarks.landmarks[i];
      }

      // 상승 모션 감지 시 정해진 프레임 동안 '상승 상태' 유지
      if (isRisingMotion) 
      {
        _liftUpRisingFramesRemaining = _liftUpRisingMemory; // 카운터 리셋
      }
      else if (_liftUpRisingFramesRemaining > 0)
      {
        _liftUpRisingFramesRemaining--; // 카운터 감소
      }

      // 최종 조건: 상승 상태 프레임 내이면
      bool liftUpDetected = _liftUpRisingFramesRemaining > 0;

      Debug.Log($"[LiftUp] 손목: L({leftWrist.y:F3}) R({rightWrist.y:F3}) | 상승모션={isRisingMotion}, 기억={_liftUpRisingFramesRemaining}, 최종={liftUpDetected} | COUNT: {_gestureFrameCount[GestureType.LiftUp]}");

      // 실패 유예 로직
      if (liftUpDetected)
      {
        _gestureFrameCount[GestureType.LiftUp]++;
        _gestureLostCount[GestureType.LiftUp] = 0; // 성공 시, '잃음' 카운터 리셋
        
        if (_gestureFrameCount[GestureType.LiftUp] >= _holdFrames)
        {
          float confidence = Mathf.Min(1f, _gestureFrameCount[GestureType.LiftUp] / (float)_holdFrames);
          return new GestureResult(GestureType.LiftUp, confidence, true, Vector3.up);
        }
      }
      else // 실패 시
      {
        _gestureLostCount[GestureType.LiftUp]++; // '잃음' 카운터 증가

        // 유예 기간(_maxLostFrames)을 초과해서 실패했을 때만 '성공' 카운터를 리셋
        if (_gestureLostCount[GestureType.LiftUp] > _maxLostFrames)
        {
          _gestureFrameCount[GestureType.LiftUp] = 0;
        }
      }

      return GestureResult.None;
    }

    /// <summary>
    /// 손가락들이 펴져있는지 확인하는 메소드
    /// - 각 손가락의 TIP이 MCP보다 Wrist에서 멀리 있어야 함
    /// - 받아온 threshold 따라 조정
    /// </summary>
    private bool AreFingersExtended(HandLandmarkerResult handResult, int handIndex)
    { 
      // 유효성 체크
      if (handResult.handLandmarks == null || handIndex >= handResult.handLandmarks.Count)
      {
        return false;
      }

      var handLandmarks = handResult.handLandmarks[handIndex];
      var wrist = GetVector3(handLandmarks.landmarks[0]);
      int[] fingerTips = {4, 8, 12, 16, 20};
      int[] fingerMCPs = {2, 5, 9, 13, 17};

      int extendedCount = 0;
      int totalFingers = fingerTips.Length;

      for(int i = 0; i < totalFingers; i++) // 각 손가락 확인
      {
        var fingerTip = GetVector3(handLandmarks.landmarks[fingerTips[i]]);
        var fingerMCP = GetVector3(handLandmarks.landmarks[fingerMCPs[i]]);

        float tipToWrist = Vector3.Distance(fingerTip, wrist);
        float mcpToWrist = Vector3.Distance(fingerMCP, wrist);

        if(tipToWrist > mcpToWrist * _jangpoongFingerRatio)
        {
          extendedCount++;
        }
      }

      bool result = extendedCount >= _jangpoongMinFingers;
      Debug.Log($"[AreFingersExtended] Hand {handIndex}: {extendedCount}/{totalFingers} fingers extended → {result}");
      return result;
    }

    private void ResetGestureCounters()
    {
      _gestureFrameCount[GestureType.Jangpoong] = 0;
      _gestureFrameCount[GestureType.LiftUp] = 0;

      _gestureLostCount[GestureType.Jangpoong] = 0;
      _gestureLostCount[GestureType.LiftUp] = 0;
    }

    private Vector3 GetVector3(NormalizedLandmark landmark)
    {
      return new Vector3(landmark.x, landmark.y, landmark.z);
    }
  }
}