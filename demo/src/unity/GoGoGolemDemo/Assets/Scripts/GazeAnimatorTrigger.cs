// Copyright (c) 2025
//
// Licensed under the MIT License. See LICENSE file in the project root for full license text.

using UnityEngine;

namespace Mediapipe.Unity.Sample.FaceLandmarkDetection
{
  /// <summary>
  ///   EventDetector의 OnGazeTriggered 이벤트에 연결되어 Animator 트리거를 실행하는 컴포넌트.
  /// </summary>
  public class GazeAnimatorTrigger : MonoBehaviour
  {
    [Header("Animator")]
    [SerializeField] private Animator _animator;

    [Header("Trigger Names")]

    [SerializeField] private string _runTriggerName = "Run";
    [SerializeField] private string _idleTriggerName = "Idle";

    [Header("Debug")]
    [SerializeField] private bool _logAnimationEvents = true;

    private void Awake()
    {
      if (_animator == null)
      {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
          Debug.LogWarning("[GazeAnimatorTrigger] Animator reference is missing.");
        }
      }
    }


    /// <summary>
    ///   Run 트리거를 실행합니다. 왼쪽 영역 EventDetector의 이벤트에 연결하세요.
    /// </summary>
    public void TriggerRun()
    {
      PlayTrigger(_runTriggerName);
    }

    /// <summary>
    ///   Idle 트리거를 실행합니다. 오른쪽 영역 EventDetector의 이벤트에 연결하세요.
    /// </summary>
    public void TriggerIdle()
    {
      PlayTrigger(_idleTriggerName);
    }

    private void PlayTrigger(string triggerName)
    {
      if (_animator == null || string.IsNullOrEmpty(triggerName))
      {
        return;
      }

      _animator.ResetTrigger(triggerName);
      _animator.SetTrigger(triggerName);

      if (_logAnimationEvents)
      {
        Debug.Log($"[GazeAnimatorTrigger] Triggered '{triggerName}' on {_animator.gameObject.name}");
      }
    }
  }
}

