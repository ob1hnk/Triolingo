// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  /// <summary>
  ///   Footer에 있는 버튼으로 이벤트 감지 기능을 토글하는 컴포넌트
  /// </summary>
  [RequireComponent(typeof(Button))]
  public class EventToggleButton : MonoBehaviour
  {
    [Header("References")]
    [SerializeField] private EventDetector _eventDetector;
    
    [Header("Button Text")]
    [SerializeField] private TMPro.TextMeshProUGUI _buttonText;
    [SerializeField] private string _activeText = "감지 활성화";
    [SerializeField] private string _inactiveText = "감지 비활성화";

    private Button _button;

    private void Awake()
    {
      _button = GetComponent<Button>();

      // 버튼 클릭 이벤트 연결
      if (_button != null)
      {
        _button.onClick.AddListener(OnButtonClick);
      }

      UpdateButtonText();
    }

    private void OnDestroy()
    {
      if (_button != null)
      {
        _button.onClick.RemoveListener(OnButtonClick);
      }
    }

    private void OnButtonClick()
    {
      if (_eventDetector != null)
      {
        _eventDetector.Toggle();
        UpdateButtonText();
      }
      else
      {
        Debug.LogWarning("[EventToggleButton] ⚠️ EventDetector not found!");
      }
    }

    private void UpdateButtonText()
    {
      if (_buttonText != null && _eventDetector != null)
      {
        _buttonText.text = _eventDetector.IsActive ? _inactiveText : _activeText;
      }
    }
  }
}

