// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using UnityEngine;
using UnityEngine.UI;

using UnityColor = UnityEngine.Color;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  /// <summary>
  ///   시선 감지 이벤트를 처리하는 핸들러 컴포넌트
  ///   EventDetector의 OnGazeTriggered 이벤트에 연결하여 사용
  /// </summary>
  public class GazeEventHandler : MonoBehaviour
  {
    [Header("Debug")]
    [SerializeField] private bool _logEvents = true;

    /// <summary>
    ///   시선 감지 이벤트가 발생했을 때 호출되는 메서드
    ///   EventDetector의 OnGazeTriggered에 연결
    /// </summary>
    public void OnGazeEventTriggered()
    {
      // 로그 출력
      if (_logEvents)
      {
        Debug.Log("[GazeEventHandler] 🎯 시선 이벤트 발생!");
      }

    }

  }
}

