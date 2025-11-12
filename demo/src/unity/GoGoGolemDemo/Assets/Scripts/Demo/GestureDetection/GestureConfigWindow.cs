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

    private void Start()
    {
      _config = GameObject.Find("Solution").GetComponent<GestureRunner>().config;
      InitializeContents();
    }

    public override void Exit() => GetModal().CloseAndResume(_isChanged);

    #region Common Settings

    private void SwitchDelegate()
    {
      _config.Delegate = (Tasks.Core.BaseOptions.Delegate)_delegateInput.value;
      _isChanged = true;
    }

    private void SwitchImageReadMode()
    {
      _config.ImageReadMode = (ImageReadMode)_imageReadModeInput.value;
      _isChanged = true;
    }

    private void SwitchRunningMode()
    {
      _config.RunningMode = (Tasks.Vision.Core.RunningMode)_runningModeInput.value;
      _isChanged = true;
    }

    #endregion

    #region Hand Landmarker Settings

    private void SetNumHands()
    {
      if (int.TryParse(_numHandsInput.text, out var value))
      {
        _config.NumHands = value;
        _isChanged = true;
      }
    }

    private void SetMinHandDetectionConfidence()
    {
      if (float.TryParse(_minHandDetectionConfidenceInput.text, out var value))
      {
        _config.MinHandDetectionConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinHandPresenceConfidence()
    {
      if (float.TryParse(_minHandPresenceConfidenceInput.text, out var value))
      {
        _config.MinHandPresenceConfidence = value;
        _isChanged = true;
      }
    }

    private void SetHandMinTrackingConfidence()
    {
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
      _config.PoseModel = (PoseModelType)_poseModelSelectionInput.value;
      _isChanged = true;
    }

    private void SetNumPoses()
    {
      if (int.TryParse(_numPosesInput.text, out var value))
      {
        _config.NumPoses = value;
        _isChanged = true;
      }
    }

    private void SetMinPoseDetectionConfidence()
    {
      if (float.TryParse(_minPoseDetectionConfidenceInput.text, out var value))
      {
        _config.MinPoseDetectionConfidence = value;
        _isChanged = true;
      }
    }

    private void SetMinPosePresenceConfidence()
    {
      if (float.TryParse(_minPosePresenceConfidenceInput.text, out var value))
      {
        _config.MinPosePresenceConfidence = value;
        _isChanged = true;
      }
    }

    private void SetPoseMinTrackingConfidence()
    {
      if (float.TryParse(_poseMinTrackingConfidenceInput.text, out var value))
      {
        _config.PoseMinTrackingConfidence = value;
        _isChanged = true;
      }
    }

    private void ToggleOutputSegmentationMasks()
    {
      _config.OutputSegmentationMasks = _outputSegmentationMasksInput.isOn;
      _isChanged = true;
    }

    #endregion

    #region Gesture Recognition Settings

    private void SetGestureDetectionThreshold()
    {
      if (float.TryParse(_gestureDetectionThresholdInput.text, out var value))
      {
        _config.GestureDetectionThreshold = value;
        _isChanged = true;
      }
    }

    private void SetGestureHoldFrames()
    {
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

    private void InitializeDelegate()
    {
      InitializeDropdown<Tasks.Core.BaseOptions.Delegate>(_delegateInput, _config.Delegate.ToString());
      _delegateInput.onValueChanged.AddListener(delegate { SwitchDelegate(); });
    }

    private void InitializeImageReadMode()
    {
      InitializeDropdown<ImageReadMode>(_imageReadModeInput, _config.ImageReadMode.ToString());
      _imageReadModeInput.onValueChanged.AddListener(delegate { SwitchImageReadMode(); });
    }

    private void InitializeRunningMode()
    {
      InitializeDropdown<Tasks.Vision.Core.RunningMode>(_runningModeInput, _config.RunningMode.ToString());
      _runningModeInput.onValueChanged.AddListener(delegate { SwitchRunningMode(); });
    }

    private void InitializeNumHands()
    {
      _numHandsInput.text = _config.NumHands.ToString();
      _numHandsInput.onValueChanged.AddListener(delegate { SetNumHands(); });
    }

    private void InitializeMinHandDetectionConfidence()
    {
      _minHandDetectionConfidenceInput.text = _config.MinHandDetectionConfidence.ToString();
      _minHandDetectionConfidenceInput.onValueChanged.AddListener(delegate { SetMinHandDetectionConfidence(); });
    }

    private void InitializeMinHandPresenceConfidence()
    {
      _minHandPresenceConfidenceInput.text = _config.MinHandPresenceConfidence.ToString();
      _minHandPresenceConfidenceInput.onValueChanged.AddListener(delegate { SetMinHandPresenceConfidence(); });
    }

    private void InitializeHandMinTrackingConfidence()
    {
      _handMinTrackingConfidenceInput.text = _config.HandMinTrackingConfidence.ToString();
      _handMinTrackingConfidenceInput.onValueChanged.AddListener(delegate { SetHandMinTrackingConfidence(); });
    }

    private void InitializePoseModelSelection()
    {
      InitializeDropdown<PoseModelType>(_poseModelSelectionInput, _config.PoseModelName);
      _poseModelSelectionInput.onValueChanged.AddListener(delegate { SwitchPoseModelType(); });
    }

    private void InitializeNumPoses()
    {
      _numPosesInput.text = _config.NumPoses.ToString();
      _numPosesInput.onValueChanged.AddListener(delegate { SetNumPoses(); });
    }

    private void InitializeMinPoseDetectionConfidence()
    {
      _minPoseDetectionConfidenceInput.text = _config.MinPoseDetectionConfidence.ToString();
      _minPoseDetectionConfidenceInput.onValueChanged.AddListener(delegate { SetMinPoseDetectionConfidence(); });
    }

    private void InitializeMinPosePresenceConfidence()
    {
      _minPosePresenceConfidenceInput.text = _config.MinPosePresenceConfidence.ToString();
      _minPosePresenceConfidenceInput.onValueChanged.AddListener(delegate { SetMinPosePresenceConfidence(); });
    }

    private void InitializePoseMinTrackingConfidence()
    {
      _poseMinTrackingConfidenceInput.text = _config.PoseMinTrackingConfidence.ToString();
      _poseMinTrackingConfidenceInput.onValueChanged.AddListener(delegate { SetPoseMinTrackingConfidence(); });
    }

    private void InitializeOutputSegmentationMasks()
    {
      _outputSegmentationMasksInput.isOn = _config.OutputSegmentationMasks;
      _outputSegmentationMasksInput.onValueChanged.AddListener(delegate { ToggleOutputSegmentationMasks(); });
    }

    private void InitializeGestureDetectionThreshold()
    {
      _gestureDetectionThresholdInput.text = _config.GestureDetectionThreshold.ToString();
      _gestureDetectionThresholdInput.onValueChanged.AddListener(delegate { SetGestureDetectionThreshold(); });
    }

    private void InitializeGestureHoldFrames()
    {
      _gestureHoldFramesInput.text = _config.GestureHoldFrames.ToString();
      _gestureHoldFramesInput.onValueChanged.AddListener(delegate { SetGestureHoldFrames(); });
    }

    #endregion
  }
}