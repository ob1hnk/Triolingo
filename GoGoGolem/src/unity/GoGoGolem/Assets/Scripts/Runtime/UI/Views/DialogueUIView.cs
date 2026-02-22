using UnityEngine;

/// <summary>
/// 대화 UI 패널의 부가 기능 관리 (표시/숨김은 LinePresenter의 CanvasGroup이 담당)
/// DialogueManager 이벤트를 받아서 추가 동작 처리 (카메라, 이동 잠금 등)
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("Event Channels")]
    [SerializeField] private GameEvent onDialogueStartedEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

    private void OnEnable()
    {
        if (onDialogueStartedEvent != null)
            onDialogueStartedEvent.Register(OnDialogueStarted);

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Register(OnDialogueCompleted);
    }

    private void OnDisable()
    {
        if (onDialogueStartedEvent != null)
            onDialogueStartedEvent.Unregister(OnDialogueStarted);

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Unregister(OnDialogueCompleted);
    }

    private void OnDialogueStarted()
    {
        Debug.Log("[DialogueUIView] 대화 시작됨");
        // 필요 시 플레이어 이동 잠금, 카메라 전환 등 추가
    }

    private void OnDialogueCompleted()
    {
        Debug.Log("[DialogueUIView] 대화 종료됨");
        // 필요 시 플레이어 이동 해제 등 추가
    }
}
