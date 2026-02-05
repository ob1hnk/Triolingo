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
  /// 제스처 감지 Runner
  /// </summary>
  public class SingleGestureRunner : Mediapipe.Unity.Sample.VisionTaskApiRunner<HandLandmarker>
  {
    [Header("Target Gesture")]
    [SerializeField] private GestureType _targetGesture = GestureType.Wind;
    
    [Header("3D Avatar")]
    [SerializeField] private AvatarLandmarkAnimator _avatarAnimator;
    
    [Header("Annotation Controllers")]
    [SerializeField] private HandLandmarkerResultAnnotationController _handAnnotationController;
    [SerializeField] private PoseLandmarkerResultAnnotationController _poseAnnotationController;
    [SerializeField] private bool _showAnnotations = true;
    
    [Header("Webcam Display")]
    [SerializeField] private RectTransform _webcamDisplayPanel; // UI Panel for webcam
    
    [Header("Gesture UI")]
    [SerializeField] private UI.SingleGestureUIController _gestureUIController;

    private Experimental.TextureFramePool _textureFramePoolHand;
    private Experimental.TextureFramePool _textureFramePoolPose;
    
    private HandLandmarker _handLandmarker;
    private PoseLandmarker _poseLandmarker;
    private GestureRecognizer _gestureRecognizer;
    
    private HandLandmarkerResult _latestHandResult;
    private PoseLandmarkerResult _latestPoseResult;
    private readonly object _resultLock = new object();

    private bool _isHandResultDirty = false;
    private bool _isPoseResultDirty = false;
    private float _lastDetectedTime = 0f;
    private float _debounceDuration = 0.2f;

    public readonly GestureConfig config = new GestureConfig();

    public override void Stop()
    {
      base.Stop();
      _textureFramePoolHand?.Dispose();
      _textureFramePoolHand = null;
      _textureFramePoolPose?.Dispose();
      _textureFramePoolPose = null;

      _handLandmarker = null;
      _poseLandmarker?.Close();
      _poseLandmarker = null;
    }

    protected override IEnumerator Run()
    {
      Debug.Log($"=== Single Gesture Detection Started: {_targetGesture} ===");
      
      // Gesture UI Controller에 타겟 제스처 설정
      if (_gestureUIController != null)
      {
        _gestureUIController.SetTargetGesture(_targetGesture);
        Debug.Log($"[SingleGestureRunner] Set target gesture to {_targetGesture} in UI Controller");
      }
      
      // Gesture Recognizer 초기화
      _gestureRecognizer = new GestureRecognizer();

      // 타겟 제스처 설정
      _gestureRecognizer.SetActiveGesture(_targetGesture);
      
      // ⭐ 기존 플러그인 제공 코드 (Pose + Hand 합침)
      // 모델 로드
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(config.HandModelPath);
      yield return Mediapipe.Unity.Sample.AssetLoader.PrepareAssetAsync(config.PoseModelPath);
      
      // Landmarker 생성
      var handOptions = config.GetHandLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnHandLandmarkDetectionOutput : null
      );
      _handLandmarker = HandLandmarker.CreateFromOptions(handOptions, GpuManager.GpuResources);
      
      var poseOptions = config.GetPoseLandmarkerOptions(
        config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null
      );
      _poseLandmarker = PoseLandmarker.CreateFromOptions(poseOptions, GpuManager.GpuResources);
      
      // taskApi는 HandLandmarker로 설정 (부모 클래스 호환성)
      taskApi = _handLandmarker;
      
      var imageSource = Mediapipe.Unity.Sample.ImageSourceProvider.ImageSource;
      yield return imageSource.Play();
      
      if (!imageSource.isPrepared)
      {
        Debug.LogError("Failed to start ImageSource, exiting...");
        yield break;
      }
      
      // TextureFramePool 초기화
      _textureFramePoolHand = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10
      );
      _textureFramePoolPose = new Experimental.TextureFramePool(
        imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10
      );
      
      // Annotation 설정
      if (_showAnnotations)
      {
        if (_handAnnotationController != null)
          SetupAnnotationController(_handAnnotationController, imageSource);
        if (_poseAnnotationController != null)
        {
          SetupAnnotationController(_poseAnnotationController, imageSource);
          _poseAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);
        }
      }

      // 화면 초기화
      screen.Initialize(imageSource);
      
      // 이미지 처리 옵션
      var transformationOptions = imageSource.GetTransformationOptions();
      var flipHorizontally = transformationOptions.flipHorizontally;
      var flipVertically = transformationOptions.flipVertically;
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

      // 데이터 보관할 변수 초기화
      _latestHandResult = HandLandmarkerResult.Alloc(handOptions.numHands);
      _latestPoseResult = PoseLandmarkerResult.Alloc(poseOptions.numPoses, poseOptions.outputSegmentationMasks);

      // 메인 스레드에서 UI 그릴 때 사용할 copy 변수 할당
      var handResultForUI = HandLandmarkerResult.Alloc(handOptions.numHands);
      var poseResultForUI = PoseLandmarkerResult.Alloc(poseOptions.numPoses, poseOptions.outputSegmentationMasks);
      
      // 메인 루프
      while (true)
      {
        if (isPaused)
        {
          yield return new WaitWhile(() => isPaused);
        }

        // Hand용 TextureFrame
        if (!_textureFramePoolHand.TryGetTextureFrame(out var textureFrame))
        {
          yield return new WaitForEndOfFrame();
          continue;
        }

        // Pose용 TextureFrame
        if (!_textureFramePoolPose.TryGetTextureFrame(out var textureFrameForPose))
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

        // ⭐ 제스처 인식 & 아바타 및 UI 업데이트
        // 1. 비동기 데이터 확인 및 복사
        bool updateHand = false;
        bool updatePose = false;

        lock (_resultLock)
        {
          if (_isHandResultDirty)
          {
            // UI용 변수로 사용하기 위해 복사
            _latestHandResult.CloneTo(ref handResultForUI);
            updateHand = true;
            _isHandResultDirty = false;
          }
          if (_isPoseResultDirty)
          {
            _latestPoseResult.CloneTo(ref poseResultForUI);
            updatePose = true;
            _isPoseResultDirty = false;
          }
        }

        bool isDetectedNow = false;

        // 데이터 유효성 검사
        bool hasHandData = handResultForUI.handLandmarks != null && handResultForUI.handLandmarks.Count >= 2;
        bool hasPoseData = poseResultForUI.poseLandmarks != null && poseResultForUI.poseLandmarks.Count > 0;

        if (hasHandData && hasPoseData)
        {
          // 2. 아바타 업데이트
          UpdateAvatar(poseResultForUI, handResultForUI);
          Debug.Log("Avatar updated - both hands detected");

          /* ⭐ 
          [SingleGestureRunner]
                    ↓
          MediaPipe Hand, Pose 감지 수행
          - Hand Data (21 point 좌표 * 2)
          - Pose Data (33 point 좌표)
                    ↓
          [GestureRecognizer]
          */
          // 3. 제스처 인식
          var gestureResult = _gestureRecognizer.RecognizeGesture(handResultForUI, poseResultForUI);

          if (gestureResult.Type == _targetGesture && gestureResult.IsDetected)
          {
            _lastDetectedTime = Time.time; // 마지막 감지 시간 갱신
            _gestureUIController?.UpdateGestureResult(gestureResult);
            isDetectedNow = true;
          }
        }
        else if (hasHandData && !hasPoseData)
        {
          // 양손 아님 -> 아바타 리셋
          _avatarAnimator?.ResetToIdle();
          Debug.Log("Hands missing - avatar reset");
        }

        /* ⭐
          [GestureRecognizer]
                  ↓
            gesture Result
                  ↓
          [SingleGestureUIController]
        */
        // 4. UI 업데이트 - 유예 로직 (ui 자주 깜빡이는 것 방지)
        if (!isDetectedNow)
        {
          if (Time.time - _lastDetectedTime > _debounceDuration)
          {
            // 설정 시간 지났는데 감지X -> UI 끄기
            _gestureUIController?.UpdateGestureResult(GestureResult.None);
          }
        }

        // 5. 웹캠 위에 annotation 그리기 (복사해둔 UI용 변수 사용)
        if (_showAnnotations)
        {
          if (updateHand) _handAnnotationController?.DrawNow(handResultForUI);
          if (updatePose) _poseAnnotationController?.DrawNow(poseResultForUI);
        }

        // 6. 메모리 정리
        if (updatePose)
        {
          DisposeAllMasks(poseResultForUI); // forUI 변수 정리
        }
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
      _handAnnotationController.DrawNow(handDetected ? handResult : default);
      _poseAnnotationController.DrawNow(poseDetected ? poseResult : default);

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

      _handAnnotationController.DrawNow(handDetected ? handResult : default);
      _poseAnnotationController.DrawNow(poseDetected ? poseResult : default);

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
    
    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        result.CloneTo(ref _latestHandResult);
        _isHandResultDirty = true;
      }
    }
    
    private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
    {
      lock (_resultLock)
      {
        result.CloneTo(ref _latestPoseResult);
        _isPoseResultDirty = true;
      }
    }
    
    // 제스처 인식 및 아바타 업데이트
    private void RecognizeGestureWithLatestResults()
    {
      HandLandmarkerResult handResult;
      PoseLandmarkerResult poseResult;
      
      lock (_resultLock)
      {
        handResult = _latestHandResult;
        poseResult = _latestPoseResult;
      }
      
      if (handResult.handLandmarks != null && handResult.handLandmarks.Count > 0 &&
          poseResult.poseLandmarks != null && poseResult.poseLandmarks.Count > 0)
      {
        // 3D 아바타 업데이트
        UpdateAvatar(poseResult, handResult);
        
        // 제스처 인식
        var gestureResult = _gestureRecognizer.RecognizeGesture(handResult, poseResult);
        
        // 타겟 제스처만 UI 업데이트
        if (gestureResult.Type == _targetGesture)
        {
          _gestureUIController?.UpdateGestureResult(gestureResult);
        }
      }
    }
    
    private void UpdateAvatar(PoseLandmarkerResult poseResult, HandLandmarkerResult handResult)
    {
      if (_avatarAnimator != null)
      {
        _avatarAnimator.UpdateAvatar(poseResult, handResult);
      }
    }
    
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