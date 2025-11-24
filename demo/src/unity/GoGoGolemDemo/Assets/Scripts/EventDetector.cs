// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using UnityColor = UnityEngine.Color;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  /// <summary>
  ///   특정 화면 영역을 일정 시간 이상 쳐다봤을 때 이벤트를 트리거하는 컴포넌트
  /// </summary>
  public class EventDetector : MonoBehaviour
  {
    [Header("Settings")]
    [SerializeField] private bool _isActive = false;
    [SerializeField, Min(0.1f)] private float _gazeDuration = 3f; // 타겟 영역을 쳐다봐야 하는 시간 (초)
    [SerializeField] private UnityColor _overlayColor = new UnityColor(1f, 0f, 0f, 0.2f); // 반투명 빨간색
    
    [Header("Target Region")]
    [SerializeField] private Vector2 _regionMin = new Vector2(0f, 0f); // 정규화된 좌표 (0~1)
    [SerializeField] private Vector2 _regionMax = new Vector2(0.5f, 1f); // 정규화된 좌표 (0~1)
    
    [Header("References")]
    [SerializeField] private GazePointAnnotationController _gazeController;
    [SerializeField] private Canvas _canvas;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onGazeTriggered = new UnityEvent();
    
    [Header("Debug")]
    [SerializeField] private bool _logGazeDetection = true;

    private float _gazeTimer = 0f;
    private bool _isGazingInRegion = false;
    private bool _hasTriggeredEvent = false;
    private GameObject _overlay;
    private RectTransform _canvasRect;

    private void Awake()
    {
      if (_gazeController == null)
      {
        Debug.LogError("[EventDetector] GazeController reference is required!");
        enabled = false;
        return;
      }

      if (_canvas == null)
      {
        Debug.LogError("[EventDetector] Canvas reference is required!");
        enabled = false;
        return;
      }

      _canvasRect = _canvas.GetComponent<RectTransform>();
      CreateOverlay();
    }

    private void Update()
    {
      if (!_isActive || _gazeController == null)
      {
        if (_overlay != null)
        {
          _overlay.SetActive(false);
        }
        return;
      }

      UpdateGazeDetection();
    }

    /// <summary>
    ///   활성화/비활성화 토글
    /// </summary>
    public void Toggle()
    {
      SetActive(!_isActive);
    }

    /// <summary>
    ///   활성화 상태 설정
    /// </summary>
    public void SetActive(bool active)
    {
      _isActive = active;
      
      if (_overlay != null)
      {
        _overlay.SetActive(_isActive);
      }

      if (!_isActive)
      {
        ResetDetection();
      }

      if (_logGazeDetection)
      {
        Debug.Log($"[EventDetector] {(active ? "Activated" : "Deactivated")} - Region: ({_regionMin.x:F2}, {_regionMin.y:F2}) ~ ({_regionMax.x:F2}, {_regionMax.y:F2})");
      }
    }

    /// <summary>
    ///   현재 활성화 상태 반환
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    ///   감지를 리셋합니다 (다시 트리거 가능하도록).
    /// </summary>
    public void ResetDetection()
    {
      _hasTriggeredEvent = false;
      _gazeTimer = 0f;
      _isGazingInRegion = false;
      UpdateOverlayAlpha(0f);
    }

    /// <summary>
    ///   타겟 영역에 반투명 오버레이를 생성합니다.
    /// </summary>
    private void CreateOverlay()
    {
      if (_canvas == null)
      {
        return;
      }

      // 이미 존재하면 재사용
      if (_overlay == null)
      {
        var existing = _canvas.transform.Find("EventOverlay");
        if (existing != null)
        {
          _overlay = existing.gameObject;
        }
        else
        {
          _overlay = new GameObject("EventOverlay");
          _overlay.transform.SetParent(_canvas.transform, false);
          var rectTransform = _overlay.AddComponent<RectTransform>();
          rectTransform.anchorMin = _regionMin;
          rectTransform.anchorMax = _regionMax;
          rectTransform.offsetMin = Vector2.zero;
          rectTransform.offsetMax = Vector2.zero;

          var image = _overlay.AddComponent<UnityEngine.UI.Image>();
          image.raycastTarget = false;
          image.color = _overlayColor;
        }
      }

      // 재사용 시에도 초기 region/색상 설정
      var overlayRect = _overlay.GetComponent<RectTransform>();
      if (overlayRect != null)
      {
        overlayRect.anchorMin = _regionMin;
        overlayRect.anchorMax = _regionMax;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
      }

      var overlayImage = _overlay.GetComponent<UnityEngine.UI.Image>();
      if (overlayImage != null)
      {
        overlayImage.color = _overlayColor;
      }

      _overlay.SetActive(_isActive);

      if (_logGazeDetection)
      {
        Debug.Log($"[EventDetector] ✅ Overlay created - Region: ({_regionMin.x:F2}, {_regionMin.y:F2}) ~ ({_regionMax.x:F2}, {_regionMax.y:F2}), Active: {_isActive}");
      }
    }

    /// <summary>
    ///   시선이 타겟 영역에 있는지 감지하고 타이머를 업데이트합니다.
    /// </summary>
    private void UpdateGazeDetection()
    {
      if (_gazeController == null)
      {
        if (_logGazeDetection)
        {
          Debug.LogWarning("[EventDetector] ⚠️ GazeController is null!");
        }
        return;
      }

      var currentGaze = GetCurrentGazePosition();
      if (!currentGaze.HasValue)
      {
        // 시선이 감지되지 않으면 타이머 리셋
        if (_isGazingInRegion)
        {
          _isGazingInRegion = false;
          _gazeTimer = 0f;
          UpdateOverlayAlpha(0f);
        }
        return;
      }

      // 타겟 영역 안에 있는지 확인
      var isInRegion = currentGaze.Value.x >= _regionMin.x && currentGaze.Value.x <= _regionMax.x &&
                       currentGaze.Value.y >= _regionMin.y && currentGaze.Value.y <= _regionMax.y;

      if (_logGazeDetection && Time.frameCount % 60 == 0) // 1초에 한 번 정도만 로그
      {
        Debug.Log($"[EventDetector] Gaze: ({currentGaze.Value.x:F3}, {currentGaze.Value.y:F3}) | Region: ({_regionMin.x:F2}, {_regionMin.y:F2})~({_regionMax.x:F2}, {_regionMax.y:F2}) | InRegion={isInRegion}");
      }

      if (isInRegion)
      {
        if (!_isGazingInRegion)
        {
          _isGazingInRegion = true;
          _gazeTimer = 0f;
        }

        _gazeTimer += Time.deltaTime;

        // 진행률 계산 (0~1)
        var progress = Mathf.Clamp01(_gazeTimer / _gazeDuration);
        UpdateOverlayAlpha(progress);

        // 지정된 시간 이상 지속되면 이벤트 트리거
        if (_gazeTimer >= _gazeDuration && !_hasTriggeredEvent)
        {
          if (_logGazeDetection)
          {
            Debug.Log($"[EventDetector] ✅ Trigger condition met (timer={_gazeTimer:F2}/{_gazeDuration:F2})");
          }
          TriggerEvent();
          _hasTriggeredEvent = true;
        }
      }
      else
      {
        // 타겟 영역을 벗어나면 타이머 리셋
        if (_isGazingInRegion)
        {
          _isGazingInRegion = false;
          _gazeTimer = 0f;
          _hasTriggeredEvent = false;
          UpdateOverlayAlpha(0f);
        }
      }
    }

    /// <summary>
    ///   GazeController에서 현재 시선 위치를 가져옵니다.
    /// </summary>
    private Vector2? GetCurrentGazePosition()
    {
      if (_gazeController == null)
      {
        return null;
      }

      return _gazeController.GetCurrentGazePosition();
    }

    /// <summary>
    ///   오버레이의 투명도를 업데이트합니다 (진행률에 따라).
    /// </summary>
    private void UpdateOverlayAlpha(float progress)
    {
      if (_overlay == null)
      {
        return;
      }

      var image = _overlay.GetComponent<UnityEngine.UI.Image>();
      if (image != null)
      {
        var color = _overlayColor;
        color.a = _overlayColor.a * (0.3f + progress * 0.7f); // 30% ~ 100% 투명도
        image.color = color;
      }
    }

    /// <summary>
    ///   타겟 영역을 지정된 시간 이상 쳐다봤을 때 이벤트를 트리거합니다.
    /// </summary>
    private void TriggerEvent()
    {
      if (_logGazeDetection)
      {
        Debug.Log($"[EventDetector] 🎯 Gaze triggered! Duration: {_gazeTimer:F2}s");
      }

      _onGazeTriggered?.Invoke();
    }
  }
}

