using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;

namespace Demo.GestureDetection
{
  public interface IGestureStrategy
  {
    GestureType GestureType {get;}
    
    // 제스처 인식
    GestureResult Recognize(
      Mediapipe.Tasks.Vision.HandLandmarker.HandLandmarkerResult handResult,
      Mediapipe.Tasks.Vision.PoseLandmarker.PoseLandmarkerResult poseResult
    );

    // 임계값 설정
    void Initialize(GestureThresholdData thresholds);
  } 
}
