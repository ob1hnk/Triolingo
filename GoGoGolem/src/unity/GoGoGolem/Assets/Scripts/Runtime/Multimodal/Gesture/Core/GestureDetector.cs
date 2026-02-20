using System;
using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using UnityEngine;
using UnityEngine.Rendering;
using Image = Mediapipe.Image;
using Tasks = Mediapipe.Tasks;
using Experimental = Mediapipe.Unity.Experimental;

namespace Demo.GestureDetection
{
  /// <summary>
  /// MediaPipe Hand + Pose Landmark 감지 전용 컴포넌트
  /// VisionTaskApiRunner 상속으로 Bootstrap 시스템과 통합
  /// </summary>
  public class GestureDetector : Mediapipe.Unity.Sample.VisionTaskApiRunner<HandLandmarker>
  {
    // 이벤트: Landmark 데이터가 업데이트될 때마다 호출
    public event Action<HandLandmarkerResult, PoseLandmarkerResult> OnLandmarksUpdated;

    // Config (코드에서 생성)
    private GestureConfig _config;
    public GestureConfig Config => _config;

    // MediaPipe 컴포넌트
    private PoseLandmarker _poseLandmarker;
    private Experimental.TextureFramePool _textureFramePoolHand;
    private Experimental.TextureFramePool _textureFramePoolPose;

    // LIVE_STREAM 모드용 최신 결과
    private HandLandmarkerResult _latestHandResult;
    private PoseLandmarkerResult _latestPoseResult;
    private readonly object _resultLock = new object();
    private bool _isHandResultDirty = false;
    private bool _isPoseResultDirty = false;

    public override void Stop()
    {
      base.Stop();
      
      _textureFramePoolHand?.Dispose();
      _textureFramePoolHand = null;

      _textureFramePoolPose?.Dispose();
      _textureFramePoolPose = null;

      _poseLandmarker?.Close();
      _poseLandmarker = null;
    }

    /// <summary>
    /// MediaPipe 감지 메인 루프 (VisionTaskApiRunner의 추상 메서드 구현)
    /// </summary>
    protected override IEnumerator Run()
    {
      Debug.Log("[GestureDetector] Starting detection...");

      // Config 생성
      _config = new GestureConfig();

      // 1. 모델 로드
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(_config.HandModelPath);
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(_config.PoseModelPath);

      // 2. Landmarker 생성
      var handOptions = _config.GetHandLandmarkerOptions(
        _config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null
      );
      var handLandmarker = HandLandmarker.CreateFromOptions(handOptions, GpuManager.GpuResources);

      var poseOptions = _config.GetPoseLandmarkerOptions(
        _config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null
      );
      _poseLandmarker = PoseLandmarker.CreateFromOptions(poseOptions, GpuManager.GpuResources);

      // taskApi는 HandLandmarker로 설정 (부모 클래스 호환성)
      taskApi = handLandmarker;

      // 3. ImageSource 초기화
      var imageSource = Mediapipe.Unity.Sample.ImageSourceProvider.ImageSource;
      yield return imageSource.Play();

      if (!imageSource.isPrepared)
      {
        Debug.LogError("[GestureDetector] Failed to start ImageSource");
        yield break;
      }

      // 4. TextureFramePool 초기화
      _textureFramePoolHand = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10
      );
      _textureFramePoolPose = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10
      );

      // 5. Screen 초기화
      screen.Initialize(imageSource);

      // 6. 이미지 처리 옵션
      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
      var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

      // 7. 비동기 처리 준비
      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();

      var handResult = HandLandmarkerResult.Alloc(handOptions.numHands);
      var poseResult = PoseLandmarkerResult.Alloc(poseOptions.numPoses, poseOptions.outputSegmentationMasks);

      // GPU 이미지 사용 가능 여부
      var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
      using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

      // LIVE_STREAM 모드용 데이터 저장 변수 초기화
      if (_config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM)
      {
        _latestHandResult = HandLandmarkerResult.Alloc(handOptions.numHands);
        _latestPoseResult = PoseLandmarkerResult.Alloc(poseOptions.numPoses, poseOptions.outputSegmentationMasks);
      }

      Debug.Log("[GestureDetector] Entering main detection loop");

      // 8. 메인 루프
      while (true)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        // Hand용 TextureFrame
        if (!_textureFramePoolHand.TryGetTextureFrame(out var textureFrame))
        {
          yield return waitForEndOfFrame;
          continue;
        }

        // Pose용 TextureFrame
        if (!_textureFramePoolPose.TryGetTextureFrame(out var textureFrameForPose))
        {
          textureFrame.Release();
          yield return waitForEndOfFrame;
          continue;
        }

        // Hand용 이미지 빌드
        Image imageForHand;
        switch (_config.ImageReadMode)
        {
          case ImageReadMode.GPU:
            if (!canUseGpuImage)
            {
              throw new System.Exception("ImageReadMode.GPU is not supported");
            }
            textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            imageForHand = textureFrame.BuildGPUImage(glContext);
            yield return waitForEndOfFrame;
            break;

          case ImageReadMode.CPU:
            yield return waitForEndOfFrame;
            textureFrame.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            imageForHand = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;

          case ImageReadMode.CPUAsync:
          default:
            req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            yield return waitUntilReqDone;

            if (req.hasError)
            {
              Debug.LogWarning("[GestureDetector] Failed to read texture from the image source");
              textureFrameForPose.Release();
              continue;
            }
            imageForHand = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;
        }

        // Pose용 이미지 빌드
        Image imageForPose;
        switch (_config.ImageReadMode)
        {
          case ImageReadMode.GPU:
            textureFrameForPose.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            imageForPose = textureFrameForPose.BuildGPUImage(glContext);
            break;

          case ImageReadMode.CPU:
            textureFrameForPose.ReadTextureOnCPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            imageForPose = textureFrameForPose.BuildCPUImage();
            textureFrameForPose.Release();
            break;

          case ImageReadMode.CPUAsync:
          default:
            req = textureFrameForPose.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
            yield return waitUntilReqDone;

            if (req.hasError)
            {
              Debug.LogWarning("[GestureDetector] Failed to read texture from the image source (Pose)");
              continue;
            }
            imageForPose = textureFrameForPose.BuildCPUImage();
            textureFrameForPose.Release();
            break;
        }

        var timestamp = GetCurrentTimestampMillisec();

        // Running Mode에 따른 처리
        switch (_config.RunningMode)
        {
          case Tasks.Vision.Core.RunningMode.IMAGE:
            ProcessImageMode(imageForHand, imageForPose, imageProcessingOptions, ref handResult, ref poseResult);
            break;

          case Tasks.Vision.Core.RunningMode.VIDEO:
            ProcessVideoMode(imageForHand, imageForPose, timestamp, imageProcessingOptions, ref handResult, ref poseResult);
            break;

          case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
            ProcessLiveStreamMode(imageForHand, imageForPose, timestamp, imageProcessingOptions);
            break;
        }

        yield return waitForEndOfFrame;
      }
    }

    /// <summary>
    /// IMAGE 모드 처리
    /// </summary>
    private void ProcessImageMode(
      Image imageForHand,
      Image imageForPose,
      Tasks.Vision.Core.ImageProcessingOptions imageProcessingOptions,
      ref HandLandmarkerResult handResult,
      ref PoseLandmarkerResult poseResult)
    {
      bool handDetected = taskApi.TryDetect(imageForHand, imageProcessingOptions, ref handResult);
      bool poseDetected = _poseLandmarker.TryDetect(imageForPose, imageProcessingOptions, ref poseResult);

      if (handDetected && poseDetected)
      {
        OnLandmarksUpdated?.Invoke(handResult, poseResult);
      }
    }

    /// <summary>
    /// VIDEO 모드 처리
    /// </summary>
    private void ProcessVideoMode(
      Image imageForHand,
      Image imageForPose,
      long timestamp,
      Tasks.Vision.Core.ImageProcessingOptions imageProcessingOptions,
      ref HandLandmarkerResult handResult,
      ref PoseLandmarkerResult poseResult)
    {
      bool handDetected = taskApi.TryDetectForVideo(imageForHand, timestamp, imageProcessingOptions, ref handResult);
      bool poseDetected = _poseLandmarker.TryDetectForVideo(imageForPose, timestamp, imageProcessingOptions, ref poseResult);

      if (handDetected && poseDetected)
      {
        OnLandmarksUpdated?.Invoke(handResult, poseResult);
      }
    }

    /// <summary>
    /// LIVE_STREAM 모드 처리
    /// </summary>
    private void ProcessLiveStreamMode(
      Image imageForHand,
      Image imageForPose,
      long timestamp,
      Tasks.Vision.Core.ImageProcessingOptions imageProcessingOptions)
    {
      // 비동기로 두 가지 감지 동시 실행
      taskApi.DetectAsync(imageForHand, timestamp, imageProcessingOptions);
      _poseLandmarker.DetectAsync(imageForPose, timestamp, imageProcessingOptions);

      // Dirty 플래그 확인하여 이벤트 발생
      bool updateHand = false;
      bool updatePose = false;

      lock (_resultLock)
      {
        updateHand = _isHandResultDirty;
        updatePose = _isPoseResultDirty;

        if (updateHand) _isHandResultDirty = false;
        if (updatePose) _isPoseResultDirty = false;
      }

      if (updateHand && updatePose)
      {
        OnLandmarksUpdated?.Invoke(_latestHandResult, _latestPoseResult);
      }
    }

    /// <summary>
    /// Hand Landmark 감지 콜백 (LIVE_STREAM 모드)
    /// </summary>
    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        result.CloneTo(ref _latestHandResult);
        _isHandResultDirty = true;
      }
    }

    /// <summary>
    /// Pose Landmark 감지 콜백 (LIVE_STREAM 모드)
    /// </summary>
    private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        result.CloneTo(ref _latestPoseResult);
        _isPoseResultDirty = true;
      }
    }

    /// <summary>
    /// 현재 타임스탬프 (밀리초)
    /// </summary>
    private long GetCurrentTimestampMillisec()
    {
      return (long)(Time.time * 1000);
    }
  }
}