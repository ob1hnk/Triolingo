using UnityEngine;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 인식 임계값 데이터
  /// 추후 ScriptableObject로 전환 예정
  /// </summary>
  [System.Serializable]
  public class GestureThresholdData
  {
    [Header("Wind Gesture Thresholds")]
    [Tooltip("Z 방향 임계값 (카메라 향함)")]
    public float forwardThreshold = 0.0f;
    
    [Tooltip("두 손 최소 2D 각도")]
    public float minHandsAngle = 100f;
    
    [Tooltip("두 손 최대 2D 각도")]
    public float maxHandsAngle = 180f;
    
    [Tooltip("최대 손목 거리")]
    public float maxWristDistance = 0.1f;
    
    [Tooltip("손가락 펴짐 비율")]
    public float fingerRatio = 1.2f;
    
    [Tooltip("최소 펴진 손가락 수")]
    public int minFingers = 5;

    [Header("Lift Gesture Thresholds")]
    [Tooltip("상승 감지 임계값")]
    public float risingThreshold = 0.01f;
    
    [Tooltip("상승 상태 기억 프레임")]
    public int risingMemory = 10;
    
    [Header("Common Settings")]
    [Tooltip("제스처 유지 필요 프레임 수 (오인식 방지)")]
    public int holdFrames = 5;
    
    [Tooltip("실패 유예 프레임 (끊김 방지)")]
    public int maxLostFrames = 3;

    /// <summary>
    /// 기본값으로 초기화된 인스턴스 생성
    /// </summary>
    public static GestureThresholdData Default()
    {
      return new GestureThresholdData();
    }

    /// <summary>
    /// Wind 제스처 전용 설정 (개별 튜닝용)
    /// </summary>
    // [SerializeField] 붙이면 Unity Inspector에서 실시간 조정 가능
    public static GestureThresholdData ForWind()
    {
      return new GestureThresholdData
      {
        forwardThreshold = 0.0f,
        minHandsAngle = 100f,
        maxHandsAngle = 180f,
        maxWristDistance = 0.1f,
        fingerRatio = 1.2f,
        minFingers = 5,
        holdFrames = 5,
        maxLostFrames = 3
      };
    }

    /// <summary>
    /// Lift 제스처 전용 설정 (개별 튜닝용)
    /// </summary>
    public static GestureThresholdData ForLift()
    {
      return new GestureThresholdData
      {
        risingThreshold = 0.01f,
        risingMemory = 10,
        holdFrames = 5,
        maxLostFrames = 3
      };
    }
  }
}

