using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 아무 키 또는 버튼으로 dismiss할 수 있는 전체화면 패널.
///
/// 사용법:
///   1. 이 컴포넌트를 패널 루트 GameObject에 추가
///   2. panelCanvasGroup에 같은 GameObject의 CanvasGroup 연결
///   3. 외부에서 Show()를 호출
///   4. OnDismissed 이벤트에 다음 동작 구독
///
/// 흐름: Show() → 페이드인 → 아무 키 또는 dismissButton 클릭 → 페이드아웃 → OnDismissed 발화
/// </summary>
public class DismissablePanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private Button dismissButton;
    [SerializeField] private float fadeDuration = 0.4f;

    /// <summary>패널이 완전히 사라진 직후 발화</summary>
    public event System.Action OnDismissed;

    private bool _active = false;
    private bool _suppressThisFrame = false;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        // SetActive 대신 alpha+blocksRaycasts로 숨김 처리
        // (SetActive(false)하면 코루틴이 실행 안 될 수 있음)
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        if (dismissButton != null)
            dismissButton.onClick.AddListener(Dismiss);
    }

    private void OnDisable()
    {
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(Dismiss);
    }

    /// <summary>패널을 페이드인하여 표시한다.</summary>
    public void Show()
    {
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeTo(1f));

        _active = true;
        _suppressThisFrame = true;
    }

    private void Update()
    {
        if (_suppressThisFrame)
        {
            _suppressThisFrame = false;
            return;
        }

        if (!_active) return;

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            Dismiss();
    }

    private void Dismiss()
    {
        if (!_active) return;
        _active = false;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutThenFire());
    }

    private IEnumerator FadeOutThenFire()
    {
        yield return FadeTo(0f);
        OnDismissed?.Invoke();
    }

    private IEnumerator FadeTo(float target)
    {
        float start = panelCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = target;
    }
}
