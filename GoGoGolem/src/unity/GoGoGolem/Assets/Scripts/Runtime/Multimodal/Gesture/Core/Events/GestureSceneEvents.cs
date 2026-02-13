using System;

namespace Demo.GestureDetection
{
  /// <summary>
  /// 제스처 씬 관련 이벤트 버스
  /// - 시스템 간 통신
  /// - 다른 시스템에서 구독하여 씬 전환 처리 가능
  /// </summary>
  public static class GestureSceneEvents
  {
    /// <summary>
    /// 제스처 성공 시 발행
    /// </summary>
    public static event Action<GestureType> OnGestureComplete;

    /// <summary>
    /// 제스처 씬 종료 요청 시 발행 (ESC, 뒤로가기 등)
    /// </summary>
    public static event Action OnGestureSceneExit;

    /// <summary>
    /// 제스처 인식 시작 시 발행 (옵션)
    /// </summary>
    public static event Action<GestureType> OnGestureStart;

    // 이벤트 발행 헬퍼 메서드
    public static void RaiseGestureComplete(GestureType gestureType)
    {
      OnGestureComplete?.Invoke(gestureType);
    }

    public static void RaiseGestureSceneExit()
    {
      OnGestureSceneExit?.Invoke();
    }

    public static void RaiseGestureStart(GestureType gestureType)
    {
      OnGestureStart?.Invoke(gestureType);
    }

    /// <summary>
    /// 모든 이벤트 구독 해제 (씬 언로드 시 메모리 누수 방지)
    /// </summary>
    public static void ClearAllSubscribers()
    {
      OnGestureComplete = null;
      OnGestureSceneExit = null;
      OnGestureStart = null;
    }
  }
}