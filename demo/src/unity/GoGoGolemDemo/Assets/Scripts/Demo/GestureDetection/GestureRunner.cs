// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;
using UnityEngine.Rendering;
using Image = Mediapipe.Image;
using Tasks = Mediapipe.Tasks;
using Experimental = Mediapipe.Unity.Experimental;

namespace Demo.GestureDetection
{
  /// <summary>
  /// HandLandmarker와 PoseLandmarker를 동시에 실행하고 제스처를 인식하는 Runner
  /// </summary>
  public class GestureRunner : Mediapipe.Unity.Sample.VisionTaskApiRunner<HandLandmarker>
  {
    [Header("Annotation Controllers")]
    [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;
    [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

    [Header("Gesture UI")]
    [SerializeField] private UI.GestureUIController _gestureUIController;

    private Experimental.TextureFramePool _textureFramePool;
    private Experimental.TextureFramePool _textureFramePoolForPose;  // Pose 전용 추가
    
    // 두 개의 taskApi
    private HandLandmarker _handLandmarker;
    private PoseLandmarker _poseLandmarker;
    
    // 제스처 인식기
    private GestureRecognizer _gestureRecognizer;
    
    // 결과 저장
    private HandLandmarkerResult _latestHandResult;
    private PoseLandmarkerResult _latestPoseResult;
    private readonly object _resultLock = new object();

    public readonly GestureConfig config = new GestureConfig();

    public override void Stop()
    {
      base.Stop();
      _textureFramePool?.Dispose();
      _textureFramePool = null;
      _textureFramePoolForPose?.Dispose();
      _textureFramePoolForPose = null;
      
      try
      {
        if (_handLandmarker != null)
        {
          _handLandmarker.Close();
          _handLandmarker = null;
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"Error closing HandLandmarker: {e.Message}");
      }
      
      try
      {
        if (_poseLandmarker != null)
        {
          _poseLandmarker.Close();
          _poseLandmarker = null;
        }
      }
      catch (System.Exception e)
      {
        Debug.LogWarning($"Error closing PoseLandmarker: {e.Message}");
      }
    }

    protected override IEnumerator Run()
    {
      Debug.Log("=== Gesture Detection Started ===");
      Debug.Log($"Delegate = {config.Delegate}");
      Debug.Log($"Image Read Mode = {config.ImageReadMode}");
      Debug.Log($"Running Mode = {config.RunningMode}");
      Debug.Log($"NumHands = {config.NumHands}");
      Debug.Log($"NumPoses = {config.NumPoses}");

      // 제스처 인식기 초기화
      _gestureRecognizer = new GestureRecognizer(
        config.GestureDetectionThreshold,
        config.GestureHoldFrames
      );
      Debug.Log("GestureRecognizer initialized");

      // Hand 모델 로드
      Debug.Log("Loading Hand model...");
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(config.HandModelPath);
      Debug.Log("Hand model loaded!");
      
      // Pose 모델 로드
      Debug.Log("Loading Pose model...");
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(config.PoseModelPath);
      Debug.Log("Pose model loaded!");

      // HandLandmarker 생성
      var handOptions = config.GetHandLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null
      );
      _handLandmarker = HandLandmarker.CreateFromOptions(handOptions, GpuManager.GpuResources);

      // PoseLandmarker 생성
      var poseOptions = config.GetPoseLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null
      );
      _poseLandmarker = PoseLandmarker.CreateFromOptions(poseOptions, GpuManager.GpuResources);

      // taskApi는 HandLandmarker로 설정 (부모 클래스 호환성)
      taskApi = _handLandmarker;

      Debug.Log("Getting ImageSource...");
      var imageSource = Mediapipe.Unity.Sample.ImageSourceProvider.ImageSource;
      Debug.Log($"ImageSource obtained: {imageSource != null}");
      
      Debug.Log("Starting ImageSource...");
      yield return imageSource.Play();

      if (!imageSource.isPrepared)
      {
        Debug.LogError("Failed to start ImageSource, exiting...");
        yield break;
      }
      Debug.Log("ImageSource started successfully!");

      // TextureFramePool 초기화 (Hand용, Pose용 각각)
      _textureFramePool = new Experimental.TextureFramePool(
        imageSource.textureWidth,
        imageSource.textureHeight,
        TextureFormat.RGBA32,
        10
      );
      
      _textureFramePoolForPose = new Experimental.TextureFramePool(
        imageSource.textureWidth,
        imageSource.textureHeight,
        TextureFormat.RGBA32,
        10
      );

      // 화면 초기화
      Debug.Log($"Initializing screen... Size: {imageSource.textureWidth}x{imageSource.textureHeight}");
      screen.Initialize(imageSource);
      Debug.Log("Screen initialized!");

      // Annotation Controller 설정
      Debug.Log("Setting up annotation controllers...");
      SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);
      SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
      _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);
      Debug.Log("Annotation controllers ready!");

      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;

      // 이미지 처리 옵션
      var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

      // 비동기 처리 준비
      AsyncGPUReadbackRequest req = default;
      var waitUntilReqDone = new WaitUntil(() => req.done);
      var waitForEndOfFrame = new WaitForEndOfFrame();
      
      var handResult = HandLandmarkerResult.Alloc(handOptions.numHands);
      var poseResult = PoseLandmarkerResult.Alloc(poseOptions.numPoses, poseOptions.outputSegmentationMasks);

      // GPU 이미지 사용 가능 여부
      var canUseGpuImage = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && GpuManager.GpuResources != null;
      using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

      Debug.Log("=== Entering main detection loop ===");

      // 메인 루프
      while (true)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        // Hand용 TextureFrame
        if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
        {
          yield return new WaitForEndOfFrame();
          continue;
        }

        // Pose용 TextureFrame
        if (!_textureFramePoolForPose.TryGetTextureFrame(out var textureFrameForPose))
        {
          textureFrame.Release();
          yield return new WaitForEndOfFrame();
          continue;
        }

        // Hand용 이미지 빌드
        Image imageForHand;
        switch (config.ImageReadMode)
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
              Debug.LogWarning("Failed to read texture from the image source");
              textureFrameForPose.Release();
              continue;
            }
            imageForHand = textureFrame.BuildCPUImage();
            textureFrame.Release();
            break;
        }

        // Pose용 이미지 빌드
        Image imageForPose;
        switch (config.ImageReadMode)
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
              Debug.LogWarning("Failed to read texture from the image source (Pose)");
              continue;
            }
            imageForPose = textureFrameForPose.BuildCPUImage();
            textureFrameForPose.Release();
            break;
        }

        var timestamp = GetCurrentTimestampMillisec();

        // Running Mode에 따른 처리
        switch (config.RunningMode)
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

        DisposeAllMasks(poseResult);
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
      bool handDetected = _handLandmarker.TryDetect(imageForHand, imageProcessingOptions, ref handResult);
      bool poseDetected = _poseLandmarker.TryDetect(imageForPose, imageProcessingOptions, ref poseResult);

      // Annotation 그리기
      _handLandmarkerResultAnnotationController.DrawNow(handDetected ? handResult : default);
      _poseLandmarkerResultAnnotationController.DrawNow(poseDetected ? poseResult : default);

      // 제스처 인식
      Debug.Log($"[ProcessImageMode] Calling RecognizeGesture... Hand: {handDetected}, Pose: {poseDetected}");
      if (handDetected && poseDetected)
      {
        var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);
        Debug.Log($"[ProcessImageMode] Gesture result: {gestureResult.Type}, IsDetected: {gestureResult.IsDetected}");
        _gestureUIController?.UpdateGestureResult(gestureResult);
      }
      else
      {
        Debug.Log("[ProcessImageMode] Hand or Pose not detected - no gesture check");
        _gestureUIController?.UpdateGestureResult(GestureResult.None);
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
      bool handDetected = _handLandmarker.TryDetectForVideo(imageForHand, timestamp, imageProcessingOptions, ref handResult);
      bool poseDetected = _poseLandmarker.TryDetectForVideo(imageForPose, timestamp, imageProcessingOptions, ref poseResult);

      _handLandmarkerResultAnnotationController.DrawNow(handDetected ? handResult : default);
      _poseLandmarkerResultAnnotationController.DrawNow(poseDetected ? poseResult : default);

      if (handDetected && poseDetected)
      {
        var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);
        _gestureUIController?.UpdateGestureResult(gestureResult);
      }
      else
      {
        _gestureUIController?.UpdateGestureResult(GestureResult.None);
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
      _handLandmarker.DetectAsync(imageForHand, timestamp, imageProcessingOptions);
      _poseLandmarker.DetectAsync(imageForPose, timestamp, imageProcessingOptions);
    }

    /// <summary>
    /// Hand 감지 결과 콜백 (LIVE_STREAM 모드)
    /// </summary>
    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        _latestHandResult = result;
      }
      
      _handLandmarkerResultAnnotationController.DrawLater(result);
      
      // 제스처 인식 (Pose 결과와 함께)
      RecognizeGestureWithLatestResults();
    }

    /// <summary>
    /// Pose 감지 결과 콜백 (LIVE_STREAM 모드)
    /// </summary>
    private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        _latestPoseResult = result;
      }
      
      _poseLandmarkerResultAnnotationController.DrawLater(result);
      DisposeAllMasks(result);
      
      // 제스처 인식 (Hand 결과와 함께)
      RecognizeGestureWithLatestResults();
    }

    /// <summary>
    /// 최신 Hand와 Pose 결과로 제스처 인식
    /// </summary>
    private void RecognizeGestureWithLatestResults()
    {
      HandLandmarkerResult handResult;
      PoseLandmarkerResult poseResult;

      lock (_resultLock)
      {
        handResult = _latestHandResult;
        poseResult = _latestPoseResult;
      }

      // 결과가 유효한지 확인 (landmarks가 있는지 체크)
      if (handResult.handLandmarks != null && handResult.handLandmarks.Count > 0 &&
          poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0)
      {
        var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);
        _gestureUIController?.UpdateGestureResult(gestureResult);
      }
      else
      {
        _gestureUIController?.UpdateGestureResult(GestureResult.None);
      }
    }

    /// <summary>
    /// Segmentation Mask 해제
    /// </summary>
    private void DisposeAllMasks(PoseLandmarkerResult result)
    {
      if (result.segmentationMasks != null)
      {
        foreach (var mask in result.segmentationMasks)
        {
          mask?.Dispose();
        }
      }
    }
  }
}