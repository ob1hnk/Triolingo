using UnityEngine;
using Mediapipe.Tasks.Components.Containers;

namespace Demo.GestureDetection
{
  /// <summary>
  /// MediaPipe Landmark를 Unity 3D 공간 좌표로 변환
  /// </summary>
  public static class LandmarkTo3D
  {
    // 좌표계 변환 설정
    private static readonly float _worldScale = 1.0f; // 월드 스케일 (조정 가능)
    private static readonly Vector3 _worldOffset = new Vector3(0, 0, 0); // 카메라로부터의 거리
    private static readonly float _shoulderWidthOffset = 0.0f; // 어깨 너비 offset
    
    /// <summary>
    /// Normalized Landmark를 Unity World Position으로 변환
    /// MediaPipe: (x: 0~1 left→right, y: 0~1 top→bottom, z: depth in meters)
    /// Unity: (x: left→right, y: bottom→top, z: near→far)
    /// </summary>
    public static Vector3 PoseLandmarkToWorldPosition(NormalizedLandmark landmark, int landmarkIndex = -1)
    {
      // X축: 그대로 사용하되 중앙을 0으로 (-0.5 ~ 0.5 범위로 변환)
      float x = (landmark.x - 0.5f) * _worldScale;
      
      // Y축: 반전 필요 (MediaPipe는 top=0, Unity는 bottom=0)
      float y = (0.5f - landmark.y) * _worldScale;
      
      // Z축: depth 값 사용 (음수 = 카메라에 가까움)
      float z = -landmark.z * _worldScale; // 부호 반전으로 앞뒤 맞춤

      // 어깨 landmark에 오프셋 적용 (11: 왼쪽 어깨, 12: 오른쪽 어깨)
      if (landmarkIndex == 11) // 왼쪽 어깨
      {
        x -= _shoulderWidthOffset;
      }
      else if (landmarkIndex == 12) // 오른쪽 어깨  
      {
        x += _shoulderWidthOffset;
      }
      
      return new Vector3(x, y, z) + _worldOffset;
    }

    public static Vector3 LandmarkToWorldPosition(NormalizedLandmark landmark)
    {
      float x = (landmark.x - 0.5f) * _worldScale;
      float y = (0.5f - landmark.y) * _worldScale;
      float z = -landmark.z * _worldScale;
      return new Vector3(x, y, z) + _worldOffset;
    }
    
    /// <summary>
    /// 두 Landmark 사이의 방향 벡터 계산
    /// </summary>
    public static Vector3 GetDirectionBetween(NormalizedLandmark from, NormalizedLandmark to)
    {
      Vector3 fromPos = LandmarkToWorldPosition(from);
      Vector3 toPos = LandmarkToWorldPosition(to);
      return (toPos - fromPos).normalized;
    }
    
    /// <summary>
    /// 두 Landmark 사이의 월드 거리 계산
    /// </summary>
    public static float GetDistance(NormalizedLandmark from, NormalizedLandmark to)
    {
      Vector3 fromPos = LandmarkToWorldPosition(from);
      Vector3 toPos = LandmarkToWorldPosition(to);
      return Vector3.Distance(fromPos, toPos);
    }
    
    /// <summary>
    /// 3점으로 Rotation 계산 (부모-자식-손자 bone chain용)
    /// </summary>
    public static Quaternion CalculateBoneRotation(
      NormalizedLandmark parent,
      NormalizedLandmark current,
      NormalizedLandmark child,
      Vector3 forwardReference = default)
    {
      if (forwardReference == default)
        forwardReference = Vector3.forward;
      
      Vector3 direction = GetDirectionBetween(current, child);
      Vector3 upDirection = GetDirectionBetween(parent, current);
      
      return Quaternion.LookRotation(direction, upDirection);
    }
    
    /// <summary>
    /// 설정값 조정 메서드 (런타임에서 테스트용)
    /// </summary>
    public static void SetWorldScale(float scale)
    {
      // _worldScale = scale; // readonly라 직접 수정 불가, 필요시 static field로 변경
    }
  }
}
