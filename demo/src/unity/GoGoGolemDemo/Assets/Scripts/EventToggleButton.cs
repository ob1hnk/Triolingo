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
    [SerializeField] private EventDetector[] _eventDetectors; // 여러 EventDetector 지원
    [SerializeField] private GameObject _avatar; // 버튼 클릭 시 나타날 avatar GameObject
    
    private Button _button;

    private void Awake()
    {
      _button = GetComponent<Button>();

      // 버튼 클릭 이벤트 연결
      if (_button != null)
      {
        _button.onClick.AddListener(OnButtonClick);
      }

      // avatar를 처음에는 비활성화 상태로 설정
      if (_avatar != null)
      {
        _avatar.SetActive(false);
      }
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
      // avatar를 활성화 (처음 클릭 시에만 나타나도록)
      if (_avatar != null && !_avatar.activeSelf)
      {
        _avatar.SetActive(true);
        Debug.Log("[EventToggleButton] ✅ Avatar activated");
      }

      if (_eventDetectors == null || _eventDetectors.Length == 0)
      {
        Debug.LogWarning("[EventToggleButton] ⚠️ EventDetectors array is empty!");
        return;
      }

      // 모든 EventDetector를 토글
      bool allActive = true;
      foreach (var detector in _eventDetectors)
      {
        if (detector != null)
        {
          detector.Toggle();
          if (!detector.IsActive)
          {
            allActive = false;
          }
        }
      }
    }
  }
}

