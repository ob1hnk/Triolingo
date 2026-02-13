// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.ComponentModel;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Tasks = Mediapipe.Tasks;

namespace Demo.GestureDetection
{
  public enum PoseModelType : int
  {
    [Description("Pose landmarker (lite)")]
    Lite = 0,
    [Description("Pose landmarker (Full)")]
    Full = 1,
    [Description("Pose landmarker (Heavy)")]
    Heavy = 2,
  }

  /// <summary>
  /// Hand와 Pose 통합 설정 클래스
  /// </summary>
  public class GestureConfig
  {
    // ========== 공통 설정 ==========
    public Tasks.Core.BaseOptions.Delegate Delegate { get; set; } =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
      Tasks.Core.BaseOptions.Delegate.CPU;
#else
      Tasks.Core.BaseOptions.Delegate.GPU;
#endif

    public ImageReadMode ImageReadMode { get; set; } = ImageReadMode.CPUAsync;
    public Tasks.Vision.Core.RunningMode RunningMode { get; set; } = Tasks.Vision.Core.RunningMode.LIVE_STREAM;

    // ========== Hand Landmarker 설정 ==========
    public int NumHands { get; set; } = 2; // 양손 감지를 위해 2로 설정
    public float MinHandDetectionConfidence { get; set; } = 0.3f;
    public float MinHandPresenceConfidence { get; set; } = 0.3f;
    public float HandMinTrackingConfidence { get; set; } = 0.3f;
    public string HandModelPath => "hand_landmarker.bytes";

    // ========== Pose Landmarker 설정 ==========
    public PoseModelType PoseModel { get; set; } = PoseModelType.Full;
    public int NumPoses { get; set; } = 1;
    public float MinPoseDetectionConfidence { get; set; } = 0.3f;
    public float MinPosePresenceConfidence { get; set; } = 0.3f;
    public float PoseMinTrackingConfidence { get; set; } = 0.3f;
    public bool OutputSegmentationMasks { get; set; } = false;

    public string PoseModelName
    {
      get
      {
        switch (PoseModel)
        {
          case PoseModelType.Lite:
            return "Pose landmarker (lite)";
          case PoseModelType.Full:
            return "Pose landmarker (Full)";
          case PoseModelType.Heavy:
            return "Pose landmarker (Heavy)";
          default:
            return PoseModel.ToString();
        }
      }
    }
    public string PoseModelPath
    {
      get
      {
        switch (PoseModel)
        {
          case PoseModelType.Lite:
            return "pose_landmarker_lite.bytes";
          case PoseModelType.Full:
            return "pose_landmarker_full.bytes";
          case PoseModelType.Heavy:
            return "pose_landmarker_heavy.bytes";
          default:
            return null;
        }
      }
    }

    // ========== 제스처 인식 설정 ==========
    public float GestureDetectionThreshold { get; set; } = 0.7f; // 제스처 인식 신뢰도 임계값
    public int GestureHoldFrames { get; set; } = 5; // 제스처 유지 프레임 수 (오인식 방지)

    // ========== Options 생성 메서드 ==========
    public HandLandmarkerOptions GetHandLandmarkerOptions(HandLandmarkerOptions.ResultCallback resultCallback = null)
    {
      return new HandLandmarkerOptions(
        new Tasks.Core.BaseOptions(Delegate, modelAssetPath: HandModelPath),
        runningMode: RunningMode,
        numHands: NumHands,
        minHandDetectionConfidence: MinHandDetectionConfidence,
        minHandPresenceConfidence: MinHandPresenceConfidence,
        minTrackingConfidence: HandMinTrackingConfidence,
        resultCallback: resultCallback
      );
    }

    public PoseLandmarkerOptions GetPoseLandmarkerOptions(PoseLandmarkerOptions.ResultCallback resultCallback = null)
    {
      return new PoseLandmarkerOptions(
        new Tasks.Core.BaseOptions(Delegate, modelAssetPath: PoseModelPath),
        runningMode: RunningMode,
        numPoses: NumPoses,
        minPoseDetectionConfidence: MinPoseDetectionConfidence,
        minPosePresenceConfidence: MinPosePresenceConfidence,
        minTrackingConfidence: PoseMinTrackingConfidence,
        outputSegmentationMasks: OutputSegmentationMasks,
        resultCallback: resultCallback
      );
    }
  }
}