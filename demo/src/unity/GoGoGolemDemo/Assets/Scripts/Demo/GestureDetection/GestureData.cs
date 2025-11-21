using UnityEngine;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 타입 정의
  /// </summary>
  public enum GestureType
  {
    None,
    Jangpoong,          // 장풍: 양손을 앞으로 밀어내는 동작
    LiftUp              // 들어올리기: 양팔을 위로 들어올리는 동작
  }

  /// <summary>
  /// 제스처 인식 결과 데이터
  /// </summary>
  public struct GestureResult
  {
    public GestureType Type;
    public float Confidence;
    public bool IsDetected;
    public Vector3 Direction; // 제스처 방향 (필요시 사용)

    public GestureResult(GestureType type, float confidence, bool isDetected, Vector3 direction = default)
    {
      Type = type;
      Confidence = confidence;
      IsDetected = isDetected;
      Direction = direction;
    }

    public static GestureResult None => new GestureResult(GestureType.None, 0f, false);
  }
}