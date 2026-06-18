using UnityEngine;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 타입 정의
  /// </summary>
  public enum GestureType
  {
    None,
    Wind,         // 바람: 양손을 앞으로 밀어내는 동작
    Lift          // 들어올리기: 양팔을 위로 들어올리는 동작
  }

  /// <summary>
  /// 제스처 인식 실패 이유 (가장 큰 실패 원인 피드백용)
  /// [Flags]로 한 프레임에 여러 조건이 동시에 실패할 수 있음.
  /// </summary>
  [System.Flags]
  public enum GestureFailReason
  {
    None             = 0,
    // ── Wind 전용 ──
    PalmsNotForward  = 1 << 0, // 손바닥이 카메라를 향하지 않음
    HandsNotOpposite = 1 << 1, // 두 손이 마주보지 않음(각도)
    WristsTooFar     = 1 << 2, // 두 손목이 너무 떨어짐
    FingersNotOpen   = 1 << 3, // 손가락이 덜 펴짐
    // ── Lift 전용 ──
    NotRising        = 1 << 4, // 팔을 위로 들어올리지 않음
    // ── 공통(Presenter가 채움) ──
    PoseMissing      = 1 << 5, // 몸(상체)이 화면에 안 보임
    HandMissing      = 1 << 6, // 손이 잠깐씩 사라짐
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
    public GestureFailReason FailReason; // 미검출 시 어떤 조건이 틀렸는지 (성공 시 None)

    public GestureResult(GestureType type, float confidence, bool isDetected, Vector3 direction = default,
                         GestureFailReason failReason = GestureFailReason.None)
    {
      Type = type;
      Confidence = confidence;
      IsDetected = isDetected;
      Direction = direction;
      FailReason = failReason;
    }

    public static GestureResult None => new GestureResult(GestureType.None, 0f, false);

    public static GestureResult Fail(GestureType type, GestureFailReason reason)
      => new GestureResult(type, 0f, false, default, reason);
  }
}