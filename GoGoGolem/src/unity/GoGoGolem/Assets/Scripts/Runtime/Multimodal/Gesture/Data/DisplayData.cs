using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// Presenter → View 전달 데이터
  /// - View는 이 데이터만 받아서 UI 업데이트
  /// </summary>
  public struct DisplayData
  {
    public HandLandmarkerResult HandData;
    public PoseLandmarkerResult PoseData;
    public GestureResult GestureResult;
    public bool HasValidData;
    
    /// <summary>
    /// 홀드 진행도 (0.0 ~ 1.0)
    /// - View가 프로그레스 바를 표시할 때 사용 (옵셔널)
    /// </summary>
    public float HoldProgress;
  }
}
