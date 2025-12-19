// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using System.Collections.Generic;
using Mediapipe;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using mptcc = Mediapipe.Tasks.Components.Containers;
using UnityColor = UnityEngine.Color;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  public class GazePointAnnotationController : AnnotationController<GazePointAnnotation>
  {
    private const int _FaceLandmarkCount = 468;
    private const int _IrisLandmarkCount = 5;

    private const int _LeftIrisStart = _FaceLandmarkCount;
    private const int _RightIrisStart = _FaceLandmarkCount + _IrisLandmarkCount;

    [SerializeField] private bool _visualizeZ = false;
    [SerializeField, Range(0f, 0.95f)] private float _smoothingFactor = 0.35f;
    [SerializeField] private UnityColor _indicatorColor = new UnityColor(0.2f, 0.78f, 1.0f, 1.0f);
    [SerializeField, Min(1f)] private float _indicatorRadius = 25f;
    
    // 높은 sensitivity로 시작 (offset이 작기 때문)
    [SerializeField] private Vector2 _screenSensitivity = new Vector2(5.0f, 5.0f);
    // 상단 영역 인식 개선: 상단을 쳐다볼 때 더 optimistic하게 해석 (1.0 = 기본값, 1.5 = 50% 더 민감)
    [SerializeField, Range(1.0f, 10f)] private float _upperHalfSensitivityBoost = 1.5f;
    
    [Header("Direct Position Control")]
    [SerializeField] private bool _useDirectPositionControl = true;
    [SerializeField] private Canvas _canvas; // Inspector에서 할당
    
    [Header("Calibration")]
    [SerializeField] private bool _enableCalibration = true;
    [SerializeField] private bool _startCalibrationOnAwake = true;
    [SerializeField] private float _calibrationPointDisplayDuration = 3f; // 각 포인트 표시 시간 (초)
    [SerializeField] private float _calibrationSampleDuration = 1f; // 마지막 1초 동안 수집
    [SerializeField] private float _calibrationSampleRate = 30f; // 초당 샘플 수
    // Calibration Bias 영향력: bias의 영향을 얼마나 강하게 적용할지 (1.0 = 기본값, 2.0 = 2배 강하게)
    [SerializeField, Range(0.5f, 3.0f)] private float _calibrationBiasWeight = 2.0f;
    [SerializeField] private Vector2[] _calibrationTargets = new Vector2[]
    {
      new Vector2(0.1f, 0.1f),  // 왼쪽 위
      new Vector2(0.9f, 0.1f),  // 오른쪽 위
      new Vector2(0.5f, 0.5f),  // 중앙
      new Vector2(0.1f, 0.9f),  // 왼쪽 아래
      new Vector2(0.9f, 0.9f)   // 오른쪽 아래
    };
    [SerializeField] private UnityColor _calibrationIndicatorColor = UnityColor.red;
    [SerializeField, Min(1f)] private float _calibrationIndicatorRadius = 35f;
    [Header("Debug")]
    [SerializeField] private bool _logIrisSample = false;
    [SerializeField] private bool _logProjection = true;
    [SerializeField] private bool _logCalibration = true;
    
    private readonly EyeRegionIndices _leftEyeRegion = new EyeRegionIndices
    (
      outer: 33,
      inner: 133,
      upper: 159,
      lower: 145
    );

    private readonly EyeRegionIndices _rightEyeRegion = new EyeRegionIndices
    (
      outer: 362,
      inner: 263,
      upper: 386,
      lower: 374
    );

    private readonly object _gazeLock = new object();
    private Vector2? _pendingGaze;
    private Vector2? _smoothedGaze;
    private RectTransform _canvasRect;

    private bool _isCalibrating = false;
    private int _currentCalibrationPointIndex = 0;
    private float _currentPointTimer = 0f;
    private float _lastSampleTime = 0f;
    private readonly List<CalibrationSample> _calibrationSamples = new List<CalibrationSample>();
    private bool _hasCalibrationResult = false;
    private Vector2 _calibrationScale = new Vector2(0.5f, 0.5f);
    private Vector2 _calibrationBias = new Vector2(0.5f, 0.5f);
    private Vector2? _pendingCalibrationOffset = null; // 메인 스레드에서 처리할 offset

    private struct CalibrationSample
    {
      public Vector2 target;
      public Vector2 offset;
    }

    private void Awake()
    {
      ApplyStyle();
      if (annotation != null)
      {
        annotation.gameObject.SetActive(false);
      }
      
      // Canvas 자동 찾기
      if (_canvas == null)
      {
        _canvas = annotation?.GetComponentInParent<Canvas>();
      }
      
      if (_canvas != null)
      {
        _canvasRect = _canvas.GetComponent<RectTransform>();
      }
      else
      {
        Debug.LogWarning("[GazeTracking] Canvas not found! Direct control disabled.");
        _useDirectPositionControl = false;
      }
    }

    private void Start()
    {
      // 처음 실행 시 보정이 완료되지 않았으면 보정 시작
      if (_enableCalibration && _startCalibrationOnAwake && !_hasCalibrationResult)
      {
        StartCalibration();
      }
      else if (_hasCalibrationResult && _logCalibration)
      {
        Debug.Log($"[GazeTracking] Using existing calibration: Scale=({_calibrationScale.x:F4},{_calibrationScale.y:F4}) Bias=({_calibrationBias.x:F4},{_calibrationBias.y:F4})");
      }
    }

    private void OnValidate()
    {
      ApplyStyle();
    }

    private void Update()
    {
      if (_isCalibrating)
      {
        // 보정 중일 때는 매 프레임 SyncNow()가 호출되도록 isStale 설정
        isStale = true;
        
        _currentPointTimer += Time.deltaTime;
        
        // 메인 스레드에서 보정 샘플 수집
        lock (_gazeLock)
        {
          if (_pendingCalibrationOffset.HasValue)
          {
            CollectCalibrationSample(_pendingCalibrationOffset.Value);
            _pendingCalibrationOffset = null;
          }
        }
        
        // 각 포인트를 3초 동안 표시
        if (_currentPointTimer >= _calibrationPointDisplayDuration)
        {
          // 다음 포인트로 이동
          _currentCalibrationPointIndex++;
          _currentPointTimer = 0f;
          _lastSampleTime = 0f;

          if (_currentCalibrationPointIndex >= _calibrationTargets.Length)
          {
            // 모든 포인트 완료
            if (_logCalibration)
            {
              // Debug.Log($"[Calibration] All points completed. Total samples: {_calibrationSamples.Count}");
            }
            StopCalibration(applyResults: true);
          }
          else
          {
            if (_logCalibration)
            {
              var point = _calibrationTargets[_currentCalibrationPointIndex];
              // Debug.Log($"[Calibration] Point {_currentCalibrationPointIndex + 1}/{_calibrationTargets.Length}: ({point.x:F2}, {point.y:F2})");
            }
          }
        }
      }
    }

    public void DrawNow(FaceLandmarkerResult result)
    {
      UpdateGaze(result);
      SyncNow();
    }

    public void DrawLater(FaceLandmarkerResult result)
    {
      lock (_gazeLock)
      {
        UpdateGaze(result);
        isStale = true;
      }
    }

    protected override void SyncNow()
    {
      lock (_gazeLock)
      {
        isStale = false;

        if (_isCalibrating)
        {
          // 보정 모드: 빨간색 보정 포인트 표시
          var calibrationPoint = GetCurrentCalibrationPoint();
          if (calibrationPoint.HasValue)
          {
            DrawIndicator(calibrationPoint.Value, _calibrationIndicatorColor, _calibrationIndicatorRadius, false);
          }
          return;
        }

        if (_pendingGaze == null)
        {
          annotation?.gameObject.SetActive(false);
          _smoothedGaze = null;
          return;
        }

        if (annotation == null)
        {
          return;
        }

        var smoothed = Smooth(_pendingGaze.Value);
        var clamped = new Vector2(Mathf.Clamp01(smoothed.x), Mathf.Clamp01(smoothed.y));
        DrawIndicator(clamped, _indicatorColor, _indicatorRadius, true);
      }
    }

    private void UpdateGaze(FaceLandmarkerResult result)
    {
      _pendingGaze = TryGetGazePoint(result);
    }

    /// <summary>
    ///   현재 시선 위치를 반환합니다 (정규화된 좌표 0~1).
    ///   외부 컴포넌트에서 시선 위치를 확인할 때 사용합니다.
    /// </summary>
    public Vector2? GetCurrentGazePosition()
    {
      lock (_gazeLock)
      {
        return _smoothedGaze ?? _pendingGaze;
      }
    }

    private Vector2? TryGetGazePoint(FaceLandmarkerResult result)
    {
      if (result.faceLandmarks == null || result.faceLandmarks.Count == 0)
      {
        return null;
      }

      var landmarks = result.faceLandmarks[0].landmarks;
      if (landmarks == null || landmarks.Count == 0)
      {
        return null;
      }

      var leftEye = GetEyeOffsetVector(landmarks, _LeftIrisStart, _leftEyeRegion);
      var rightEye = GetEyeOffsetVector(landmarks, _RightIrisStart, _rightEyeRegion);

      var offset = CombineSamples(leftEye, rightEye);
      if (!offset.HasValue)
      {
        return null;
      }

      // 보정 중이면 offset을 저장 (메인 스레드에서 처리)
      if (_isCalibrating)
      {
        lock (_gazeLock)
        {
          _pendingCalibrationOffset = offset.Value;
        }
      }

      var projected = ApplyProjection(offset.Value);

      if (_logProjection)
      {
        // Debug.Log($"[Gaze] Projected=({projected.x:F4},{projected.y:F4})");
      }

      return projected;
    }
    private void DrawIndicator(Vector2 normalized, UnityColor color, float radius, bool logDirectControl)
    {
      if (annotation == null)
      {
        return;
      }

      annotation.gameObject.SetActive(true);
      annotation.SetColor(color);
      annotation.SetRadius(radius);

      if (_useDirectPositionControl && _canvasRect != null)
      {
        var rectTransform = annotation.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
          var canvasWidth = _canvasRect.rect.width;
          var canvasHeight = _canvasRect.rect.height;
          var x = (normalized.x - 0.5f) * canvasWidth;
          var y = (normalized.y - 0.5f) * canvasHeight;
          rectTransform.anchoredPosition = new Vector2(x, -y);

          if (_logProjection && logDirectControl)
          {
            // Debug.Log($"[GazeTracking] DIRECT CONTROL: normalized=({normalized.x:F4},{normalized.y:F4}) canvas=({x:F1},{-y:F1})");
          }
        }
        return;
      }

      var landmark = new NormalizedLandmark
      {
        X = normalized.x,
        Y = normalized.y,
        Z = 0f,
        Visibility = 1f,
        Presence = 1f
      };
      annotation.Draw(landmark, _visualizeZ);
    }

    private Vector2 ApplyProjection(Vector2 offset)
    {
      // offset은 이미 GetEyeOffsetVector에서 상단 영역에 대해 boost가 적용된 상태
      if (_hasCalibrationResult)
      {
        // Calibration Bias의 영향을 강화: bias에 가중치를 곱함
        var weightedBias = new Vector2(
          _calibrationBias.x * _calibrationBiasWeight,
          _calibrationBias.y * _calibrationBiasWeight
        );
        
        var calibrated = new Vector2(
          _calibrationScale.x * offset.x + weightedBias.x,
          _calibrationScale.y * offset.y + weightedBias.y
        );
        return new Vector2(Mathf.Clamp01(calibrated.x), Mathf.Clamp01(calibrated.y));
      }

      var projected = new Vector2(
        0.5f + offset.x * _screenSensitivity.x,
        0.5f + offset.y * _screenSensitivity.y
      );
      return new Vector2(Mathf.Clamp01(projected.x), Mathf.Clamp01(projected.y));
    }

    #region Calibration

    public void StartCalibration()
    {
      // 보정이 이미 완료되었으면 다시 시작하지 않음
      if (_hasCalibrationResult)
      {
        if (_logCalibration)
        {
          // Debug.Log("[Calibration] Calibration already completed. Skipping.");
        }
        return;
      }

      if (!_enableCalibration)
      {
        // Debug.LogWarning("[Calibration] Calibration is disabled. Enable it in the inspector.");
        return;
      }

      if (_calibrationTargets == null || _calibrationTargets.Length == 0)
      {
        // Debug.LogWarning("[Calibration] Calibration targets are not configured.");
        return;
      }

      _isCalibrating = true;
      _currentCalibrationPointIndex = 0;
      _currentPointTimer = 0f;
      _lastSampleTime = 0f;
      _calibrationSamples.Clear();
      _pendingGaze = null;
      _smoothedGaze = null;

      var firstPoint = _calibrationTargets[0];
      // Debug.Log($"[Calibration] Calibration started!");
      // Debug.Log($"[Calibration] {_calibrationTargets.Length} points, {_calibrationPointDisplayDuration}s per point, collecting last {_calibrationSampleDuration}s");
      // Debug.Log($"[Calibration] Point 1/{_calibrationTargets.Length}: ({firstPoint.x:F2}, {firstPoint.y:F2}) - Look at the RED point");
    }

    public void StopCalibration(bool applyResults = true)
    {
      _isCalibrating = false;
      _currentCalibrationPointIndex = 0;
      _currentPointTimer = 0f;
      _lastSampleTime = 0f;

      if (_logCalibration)
      {
        // Debug.Log($"[Calibration] Stopped. Collected {_calibrationSamples.Count} samples.");
      }

      if (applyResults && _calibrationSamples.Count > 0)
      {
        SolveCalibration();
      }
      else
      {
        _hasCalibrationResult = false;
        if (_logCalibration)
        {
          // Debug.LogWarning("[Calibration] No samples collected. Calibration not applied.");
        }
      }
    }

    private void CollectCalibrationSample(Vector2 offset)
    {
      if (!_isCalibrating)
      {
        return;
      }

      var target = GetCurrentCalibrationPoint();
      if (!target.HasValue)
      {
        return;
      }

      // 마지막 1초 동안만 샘플 수집
      var timeUntilNextPoint = _calibrationPointDisplayDuration - _currentPointTimer;
      if (timeUntilNextPoint > _calibrationSampleDuration)
      {
        // 아직 수집 시간이 아님 (처음 2초는 대기)
        return;
      }

      // 샘플링 간격 체크 (초당 _calibrationSampleRate개)
      var sampleInterval = 1f / _calibrationSampleRate;
      if (Time.time - _lastSampleTime < sampleInterval)
      {
        return;
      }

      // 샘플 수집
      _calibrationSamples.Add(new CalibrationSample
      {
        target = target.Value,
        offset = offset
      });
      _lastSampleTime = Time.time;

      if (_logCalibration && _calibrationSamples.Count % 10 == 0)
      {
        var remainingTime = _calibrationSampleDuration - timeUntilNextPoint;
      }
    }

    private Vector2? GetCurrentCalibrationPoint()
    {
      if (!_isCalibrating)
      {
        return null;
      }

      if (_calibrationTargets == null || _calibrationTargets.Length == 0)
      {
        return null;
      }

      if (_currentCalibrationPointIndex >= _calibrationTargets.Length)
      {
        return null;
      }

      return _calibrationTargets[_currentCalibrationPointIndex];
    }

    private void SolveCalibration()
    {
      // 최소 3개 포인트의 샘플이 있어야 보정 가능
      var minSamplesPerPoint = (int)(_calibrationSampleDuration * _calibrationSampleRate * 0.5f); // 최소 절반 이상
      var minTotalSamples = _calibrationTargets.Length * minSamplesPerPoint;
      
      if (_calibrationSamples.Count < minTotalSamples)
      {
        _hasCalibrationResult = false;
        // Debug.LogWarning($"[Calibration] Not enough samples. Need at least {minTotalSamples}, got {_calibrationSamples.Count}.");
        return;
      }

      if (TryFitAxis(_calibrationSamples, true, out var scaleX, out var biasX) &&
          TryFitAxis(_calibrationSamples, false, out var scaleY, out var biasY))
      {
        _calibrationScale = new Vector2(scaleX, scaleY);
        _calibrationBias = new Vector2(biasX, biasY);
        _hasCalibrationResult = true;
      }
      else
      {
        _hasCalibrationResult = false;
        // Debug.LogWarning("[Calibration] Failed to fit calibration matrix. Reverting to default sensitivity.");
      }
    }

    private bool TryFitAxis(List<CalibrationSample> samples, bool useX, out float scale, out float bias)
    {
      float sumOffset = 0f;
      float sumTarget = 0f;
      float sumOffsetSq = 0f;
      float sumOffsetTarget = 0f;
      int n = samples.Count;

      foreach (var sample in samples)
      {
        var offset = useX ? sample.offset.x : sample.offset.y;
        var target = useX ? sample.target.x : sample.target.y;
        sumOffset += offset;
        sumTarget += target;
        sumOffsetSq += offset * offset;
        sumOffsetTarget += offset * target;
      }

      var denominator = n * sumOffsetSq - sumOffset * sumOffset;
      if (Mathf.Abs(denominator) < 1e-5f)
      {
        scale = 0f;
        bias = 0.5f;
        return false;
      }

      scale = (n * sumOffsetTarget - sumOffset * sumTarget) / denominator;
      bias = (sumTarget - scale * sumOffset) / n;
      return true;
    }

    #endregion

    private static Vector2 Average(IReadOnlyList<mptcc.NormalizedLandmark> landmarks, int start, int count)
    {
      var sum = Vector2.zero;
      for (var i = 0; i < count; i++)
      {
        var current = landmarks[start + i];
        sum.x += current.x;
        sum.y += current.y;
      }
      return sum / count;
    }

    private Vector2 Smooth(Vector2 next)
    {
      var lerpT = 1f - Mathf.Clamp01(_smoothingFactor);
      if (_smoothedGaze.HasValue)
      {
        _smoothedGaze = Vector2.Lerp(_smoothedGaze.Value, next, lerpT);
      }
      else
      {
        _smoothedGaze = next;
      }
      return _smoothedGaze.Value;
    }

    private void ApplyStyle()
    {
      if (annotation == null)
      {
        return;
      }
      annotation.SetColor(_indicatorColor);
      annotation.SetRadius(_indicatorRadius);
    }

    private Vector2? GetEyeOffsetVector(IReadOnlyList<mptcc.NormalizedLandmark> landmarks, int irisStart, EyeRegionIndices eyeRegion)
    {
      var irisCenter = TryGetIrisCenter(landmarks, irisStart);
      var pupil = irisCenter ?? GetEyelidCenter(landmarks, eyeRegion);

      var outer = landmarks[eyeRegion.outer];
      var inner = landmarks[eyeRegion.inner];
      var upper = landmarks[eyeRegion.upper];
      var lower = landmarks[eyeRegion.lower];

      var horizontalHalf = Mathf.Max(Mathf.Abs(inner.x - outer.x) * 0.5f, 1e-5f);
      var verticalHalf = Mathf.Max(Mathf.Abs(lower.y - upper.y) * 0.5f, 1e-5f);

      var horizontalCenter = (outer.x + inner.x) * 0.5f;
      var verticalCenter = (upper.y + lower.y) * 0.5f;

      var horizontal = Mathf.Clamp((pupil.x - horizontalCenter) / horizontalHalf, -1f, 1f);
      var vertical = Mathf.Clamp((verticalCenter - pupil.y) / verticalHalf, -1f, 1f);

      // 상단 영역 인식 개선: 상단을 쳐다볼 때(negative vertical) 더 optimistic하게 해석
      // 원본 offset 값 자체를 조정하여 보정에도 반영되도록 함
      if (vertical < 0f)
      {
        vertical = vertical * _upperHalfSensitivityBoost;
        vertical = Mathf.Clamp(vertical, -1f, 1f);
      }

      if (_logIrisSample && irisCenter.HasValue)
      {
        // Debug.Log($"[Gaze] Iris offset=({horizontal:F4},{vertical:F4}) [upper boost: {_upperHalfSensitivityBoost}]");
      }

      return new Vector2(horizontal, vertical);
    }

    private Vector2? TryGetIrisCenter(IReadOnlyList<mptcc.NormalizedLandmark> landmarks, int irisStart)
    {
      if (landmarks.Count < irisStart + _IrisLandmarkCount)
      {
        return null;
      }
      return Average(landmarks, irisStart, _IrisLandmarkCount);
    }

    private Vector2 GetEyelidCenter(IReadOnlyList<mptcc.NormalizedLandmark> landmarks, EyeRegionIndices eyeRegion)
    {
      var outer = landmarks[eyeRegion.outer];
      var inner = landmarks[eyeRegion.inner];
      var upper = landmarks[eyeRegion.upper];
      var lower = landmarks[eyeRegion.lower];
      return new Vector2(
        (outer.x + inner.x + upper.x + lower.x) * 0.25f,
        (outer.y + inner.y + upper.y + lower.y) * 0.25f
      );
    }

    private Vector2? CombineSamples(Vector2? left, Vector2? right)
    {
      if (left.HasValue && right.HasValue)
      {
        return (left.Value + right.Value) * 0.5f;
      }
      return left ?? right;
    }

    [System.Serializable]
    private readonly struct EyeRegionIndices
    {
      public readonly int outer;
      public readonly int inner;
      public readonly int upper;
      public readonly int lower;

      public EyeRegionIndices(int outer, int inner, int upper, int lower)
      {
        this.outer = outer;
        this.inner = inner;
        this.upper = upper;
        this.lower = lower;
      }
    }

  }
}