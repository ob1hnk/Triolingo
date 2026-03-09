using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 퀘스트 알림 View.
/// questNameText: 퀘스트 타입 + 이름 항상 표시
/// questStateText: 상태 메시지 ("새로운 목표!", "완료!") — 퀘스트 시작 시에는 숨김
/// </summary>
public class QuestNotificationView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI questNameText;
    [SerializeField] private TextMeshProUGUI questStateText;
    [SerializeField] private float displayDuration = 3f;

    private Coroutine _dismissCoroutine;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    /// <param name="questHeader">"&lt;메인&gt; 퀘스트명" 형식</param>
    /// <param name="stateText">null이면 상태 오브젝트 비활성화</param>
    public void ShowNotification(string questHeader, string stateText = null)
    {
        questNameText.text = questHeader;

        bool hasState = !string.IsNullOrEmpty(stateText);
        questStateText.gameObject.SetActive(hasState);
        if (hasState)
            questStateText.text = stateText;

        gameObject.SetActive(true);

        if (_dismissCoroutine != null)
            StopCoroutine(_dismissCoroutine);
        _dismissCoroutine = StartCoroutine(DismissAfterDelay());
    }

    private IEnumerator DismissAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        gameObject.SetActive(false);
        _dismissCoroutine = null;
    }
}