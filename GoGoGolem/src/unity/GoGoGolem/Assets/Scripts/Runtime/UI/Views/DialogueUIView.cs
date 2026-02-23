using UnityEngine;

/// <summary>
/// 대화 UI 패널의 가시성을 관리
/// DialogueManager의 이벤트를 받아서 UI를 표시/숨김
/// </summary>
public class DialogueUIView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("Event Channels")]
    [SerializeField] private GameEvent onDialogueStartedEvent;
    [SerializeField] private GameEvent onDialogueCompletedEvent;

    private void Awake()
    {
        // 시작 시 대화 UI 숨김
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (onDialogueStartedEvent != null)
        {
            onDialogueStartedEvent.Register(ShowDialogueUI);
            Debug.Log("[DialogueUIView] OnEnable: 이벤트 등록 완료");
        }
        else
        {
            Debug.LogError("[DialogueUIView] OnEnable: onDialogueStartedEvent가 null입니다!");
        }

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Register(HideDialogueUI);
    }

    private void OnDisable()
    {
        if (onDialogueStartedEvent != null)
            onDialogueStartedEvent.Unregister(ShowDialogueUI);

        if (onDialogueCompletedEvent != null)
            onDialogueCompletedEvent.Unregister(HideDialogueUI);
    }

    private void ShowDialogueUI()
    {
        Debug.Log("[DialogueUIView] ShowDialogueUI 호출됨!");
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
            Debug.Log("[DialogueUIView] 대화 패널 표시 완료");
        }
        else
        {
            Debug.LogError("[DialogueUIView] dialoguePanel이 null입니다!");
        }
    }

    private void HideDialogueUI()
    {
        Debug.Log("[DialogueUIView] HideDialogueUI 호출됨!");
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
            Debug.Log("[DialogueUIView] 대화 패널 숨김 완료");
        }
    }
}
