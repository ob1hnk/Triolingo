// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using UnityEngine;
using UnityEngine.UI;
using Mediapipe.Unity.Sample.UI;
using Mediapipe.Unity;
using Tasks = Mediapipe.Tasks;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// Gesture Config Window with improved modal handling
  /// </summary>
  public class GestureConfigWindow : ModalContents
  {
    [Header("Common Settings")]
    [SerializeField] private Dropdown _delegateInput;
    [SerializeField] private Dropdown _imageReadModeInput;
    [SerializeField] private Dropdown _runningModeInput;

    [Header("Hand Landmarker Settings")]
    [SerializeField] private InputField _numHandsInput;
    [SerializeField] private InputField _minHandDetectionConfidenceInput;
    [SerializeField] private InputField _minHandPresenceConfidenceInput;
    [SerializeField] private InputField _handMinTrackingConfidenceInput;

    [Header("Pose Landmarker Settings")]
    [SerializeField] private Dropdown _poseModelSelectionInput;
    [SerializeField] private InputField _numPosesInput;
    [SerializeField] private InputField _minPoseDetectionConfidenceInput;
    [SerializeField] private InputField _minPosePresenceConfidenceInput;
    [SerializeField] private InputField _poseMinTrackingConfidenceInput;
    [SerializeField] private Toggle _outputSegmentationMasksInput;

    [Header("Gesture Recognition Settings")]
    [SerializeField] private InputField _gestureDetectionThresholdInput;
    [SerializeField] private InputField _gestureHoldFramesInput;

    private GestureConfig _config;
    private bool _isChanged;
    private bool _isInitialized = false;

    private void Start()
    {
      InitializeConfig();
    }

    private void OnEnable()
    {
      // OnEnable에서도 초기화 시도 (Start 전에 호출될 수 있음)
      if (!_isInitialized)
      {
        InitializeConfig();
      }
    }

    private void InitializeConfig()
    {
      if (_isInitialized) return;

      // Solution GameObject를 찾아서 GestureRunner 컴포넌트 가져오기
      var solutionObject = GameObject.Find("Solution");
      if (solutionObject == null)
      {
        Debug.LogError("[GestureConfigWindow] Solution GameObject not found!");
        return;
      }

      var gestureRunner = solutionObject.GetComponent<GestureRunner>();
      if (gestureRunner == null)
      {
        Debug.LogError("[GestureConfigWindow] GestureRunner component not found on Solution GameObject!");
        return;
      }

      _config = gestureRunner.config;
      InitializeContents();
      _isInitialized = true;
      Debug.Log("GestureConfigWindow initialized successfully");
    }

    public override void Exit()
    {
      GetModal().CloseAndResume(_isChanged);
      _isChanged = false; // Reset after closing
    }

    #region Common Settings

    private void SwitchDelegate()
    {
      if (_config == null) return;
      _config.Delegate = (Tasks.Core.BaseOptions.Delegate)_delegateInput.value;
      _isChanged = true;
      Debug.Log($"Delegate changed to: {_config.Delegate}");
    }

    private void SwitchImageReadMode()
    {
      if (_config == null) return;
      _config.ImageReadMode = (ImageReadMode)_imageReadModeInput.value;
      _isChanged = true;
      Debug.Log($"ImageReadMode changed to: {_config.ImageReadMode}");
    }

    private void SwitchRunningMode()
    {
      if (_config == null) return;
      _config.RunningMode = (Tasks.Vision.Core.RunningMode)_runningModeInput.value;
      _isChanged = true;
      Debug.Log($"RunningMode changed to: {_config.RunningMode}");
    }

    #endregion

    #region Hand Landmarker Settings

    private void SetNumHands()
    {
      if (_config == null) return;
      if (int.TryParse(_numHandsInput.text, out var value))
      {
        _config.NumHands = value;
        _isChanged = true;
        Debug.Log($"NumHands changed to: {value}");
      }
    }

    private void SetMinHandDetectionConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_minHandDetectionConfidenceInput.text, out var value))
      {
        _config.MinHandDetectionConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinHandPresenceConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_minHandPresenceConfidenceInput.text, out var value))
      {
        _config.MinHandPresenceConfidence = value;
        _isChanged = true;
      }
    }

    private void SetHandMinTrackingConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_handMinTrackingConfidenceInput.text, out var value))
      {
        _config.HandMinTrackingConfidence = value;
        _isChanged = true;
      }
    }

    #endregion

    #region Pose Landmarker Settings

    private void SwitchPoseModelType()
    {
      if (_config == null) return;
      _config.PoseModel = (PoseModelType)_poseModelSelectionInput.value;
      _isChanged = true;
    }

    private void SetNumPoses()
    {
      if (_config == null) return;
      if (int.TryParse(_numPosesInput.text, out var value))
      {
        _config.NumPoses = value;
        _isChanged = true;
      }
    }

    private void SetMinPoseDetectionConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_minPoseDetectionConfidenceInput.text, out var value))
      {
        _config.MinPoseDetectionConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinPosePresenceConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_minPosePresenceConfidenceInput.text, out var value))
      {
        _config.MinPosePresenceConfidence = value;
        _isChanged = true;
      }
    }

    private void SetPoseMinTrackingConfidence()
    {
      if (_config == null) return;
      if (float.TryParse(_poseMinTrackingConfidenceInput.text, out var value))
      {
        _config.PoseMinTrackingConfidence = value;
        _isChanged = true;
      }
    }

    private void ToggleOutputSegmentationMasks()
    {
      if (_config == null) return;
      _config.OutputSegmentationMasks = _outputSegmentationMasksInput.isOn;
      _isChanged = true;
    }

    #endregion

    #region Gesture Recognition Settings

    private void SetGestureDetectionThreshold()
    {
      if (_config == null) return;
      if (float.TryParse(_gestureDetectionThresholdInput.text, out var value))
      {
        _config.GestureDetectionThreshold = value;
        _isChanged = true;
      }
    }

    private void SetGestureHoldFrames()
    {
      if (_config == null) return;
      if (int.TryParse(_gestureHoldFramesInput.text, out var value))
      {
        _config.GestureHoldFrames = value;
        _isChanged = true;
      }
    }

    #endregion

    #region Initialization

    private void InitializeContents()
    {
      if (_config == null)
      {
        Debug.LogError("[GestureConfigWindow] Cannot initialize - config is null");
        return;
      }

      // Remove existing listeners first to prevent duplicates
      RemoveAllListeners();

      // Common Settings
      InitializeDelegate();
      InitializeImageReadMode();
      InitializeRunningMode();

      // Hand Landmarker Settings
      InitializeNumHands();
      InitializeMinHandDetectionConfidence();
      InitializeMinHandPresenceConfidence();
      InitializeHandMinTrackingConfidence();

      // Pose Landmarker Settings
      InitializePoseModelSelection();
      InitializeNumPoses();
      InitializeMinPoseDetectionConfidence();
      InitializeMinPosePresenceConfidence();
      InitializePoseMinTrackingConfidence();
      InitializeOutputSegmentationMasks();

      // Gesture Recognition Settings
      InitializeGestureDetectionThreshold();
      InitializeGestureHoldFrames();
    }

    private void RemoveAllListeners()
    {
      _delegateInput?.onValueChanged.RemoveAllListeners();
      _imageReadModeInput?.onValueChanged.RemoveAllListeners();
      _runningModeInput?.onValueChanged.RemoveAllListeners();
      _numHandsInput?.onValueChanged.RemoveAllListeners();
      _minHandDetectionConfidenceInput?.onValueChanged.RemoveAllListeners();
      _minHandPresenceConfidenceInput?.onValueChanged.RemoveAllListeners();
      _handMinTrackingConfidenceInput?.onValueChanged.RemoveAllListeners();
      _poseModelSelectionInput?.onValueChanged.RemoveAllListeners();
      _numPosesInput?.onValueChanged.RemoveAllListeners();
      _minPoseDetectionConfidenceInput?.onValueChanged.RemoveAllListeners();
      _minPosePresenceConfidenceInput?.onValueChanged.RemoveAllListeners();
      _poseMinTrackingConfidenceInput?.onValueChanged.RemoveAllListeners();
      _outputSegmentationMasksInput?.onValueChanged.RemoveAllListeners();
      _gestureDetectionThresholdInput?.onValueChanged.RemoveAllListeners();
      _gestureHoldFramesInput?.onValueChanged.RemoveAllListeners();
    }

    private void InitializeDelegate()
    {
      if (_delegateInput == null) return;
      InitializeDropdown<Tasks.Core.BaseOptions.Delegate>(_delegateInput, _config.Delegate.ToString());
      _delegateInput.onValueChanged.AddListener(delegate { SwitchDelegate(); });
    }

    private void InitializeImageReadMode()
    {
      if (_imageReadModeInput == null) return;
      InitializeDropdown<ImageReadMode>(_imageReadModeInput, _config.ImageReadMode.ToString());
      _imageReadModeInput.onValueChanged.AddListener(delegate { SwitchImageReadMode(); });
    }

    private void InitializeRunningMode()
    {
      if (_runningModeInput == null) return;
      InitializeDropdown<Tasks.Vision.Core.RunningMode>(_runningModeInput, _config.RunningMode.ToString());
      _runningModeInput.onValueChanged.AddListener(delegate { SwitchRunningMode(); });
    }

    private void InitializeNumHands()
    {
      if (_numHandsInput == null) return;
      _numHandsInput.text = _config.NumHands.ToString();
      _numHandsInput.onValueChanged.AddListener(delegate { SetNumHands(); });
    }

    private void InitializeMinHandDetectionConfidence()
    {
      if (_minHandDetectionConfidenceInput == null) return;
      _minHandDetectionConfidenceInput.text = _config.MinHandDetectionConfidence.ToString();
      _minHandDetectionConfidenceInput.onValueChanged.AddListener(delegate { SetMinHandDetectionConfidence(); });
    }

    private void InitializeMinHandPresenceConfidence()
    {
      if (_minHandPresenceConfidenceInput == null) return;
      _minHandPresenceConfidenceInput.text = _config.MinHandPresenceConfidence.ToString();
      _minHandPresenceConfidenceInput.onValueChanged.AddListener(delegate { SetMinHandPresenceConfidence(); });
    }

    private void InitializeHandMinTrackingConfidence()
    {
      if (_handMinTrackingConfidenceInput == null) return;
      _handMinTrackingConfidenceInput.text = _config.HandMinTrackingConfidence.ToString();
      _handMinTrackingConfidenceInput.onValueChanged.AddListener(delegate { SetHandMinTrackingConfidence(); });
    }

    private void InitializePoseModelSelection()
    {
      if (_poseModelSelectionInput == null) return;
      InitializeDropdown<PoseModelType>(_poseModelSelectionInput, _config.PoseModelName);
      _poseModelSelectionInput.onValueChanged.AddListener(delegate { SwitchPoseModelType(); });
    }

    private void InitializeNumPoses()
    {
      if (_numPosesInput == null) return;
      _numPosesInput.text = _config.NumPoses.ToString();
      _numPosesInput.onValueChanged.AddListener(delegate { SetNumPoses(); });
    }

    private void InitializeMinPoseDetectionConfidence()
    {
      if (_minPoseDetectionConfidenceInput == null) return;
      _minPoseDetectionConfidenceInput.text = _config.MinPoseDetectionConfidence.ToString();
      _minPoseDetectionConfidenceInput.onValueChanged.AddListener(delegate { SetMinPoseDetectionConfidence(); });
    }

    private void InitializeMinPosePresenceConfidence()
    {
      if (_minPosePresenceConfidenceInput == null) return;
      _minPosePresenceConfidenceInput.text = _config.MinPosePresenceConfidence.ToString();
      _minPosePresenceConfidenceInput.onValueChanged.AddListener(delegate { SetMinPosePresenceConfidence(); });
    }

    private void InitializePoseMinTrackingConfidence()
    {
      if (_poseMinTrackingConfidenceInput == null) return;
      _poseMinTrackingConfidenceInput.text = _config.PoseMinTrackingConfidence.ToString();
      _poseMinTrackingConfidenceInput.onValueChanged.AddListener(delegate { SetPoseMinTrackingConfidence(); });
    }

    private void InitializeOutputSegmentationMasks()
    {
      if (_outputSegmentationMasksInput == null) return;
      _outputSegmentationMasksInput.isOn = _config.OutputSegmentationMasks;
      _outputSegmentationMasksInput.onValueChanged.AddListener(delegate { ToggleOutputSegmentationMasks(); });
    }

    private void InitializeGestureDetectionThreshold()
    {
      if (_gestureDetectionThresholdInput == null) return;
      _gestureDetectionThresholdInput.text = _config.GestureDetectionThreshold.ToString();
      _gestureDetectionThresholdInput.onValueChanged.AddListener(delegate { SetGestureDetectionThreshold(); });
    }

    private void InitializeGestureHoldFrames()
    {
      if (_gestureHoldFramesInput == null) return;
      _gestureHoldFramesInput.text = _config.GestureHoldFrames.ToString();
      _gestureHoldFramesInput.onValueChanged.AddListener(delegate { SetGestureHoldFrames(); });
    }

    #endregion
  }
}