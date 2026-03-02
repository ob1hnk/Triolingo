using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// OptionItem 프리팹에 함께 붙이는 컴포넌트.
/// Unity EventSystem의 Select/Deselect 이벤트를 받아 SelectTriangleImage와
/// SelectUnderlineImage를 제어한다.
/// Underline은 선택 시 텍스트 너비에 맞춰 왼쪽에서 오른쪽으로 늘어나는 애니메이션이 재생된다.
/// </summary>
public class OptionItemView : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image selectTriangleImage;
    [SerializeField] private Image selectUnderlineImage;
    [SerializeField] private TMP_Text optionText;

    [SerializeField] private float underlineAnimDuration = 0.2f;

    private RectTransform _underlineRect;
    private Coroutine _underlineCoroutine;

    private void Awake()
    {
        if (selectUnderlineImage != null)
            _underlineRect = selectUnderlineImage.GetComponent<RectTransform>();

        SetTriangleVisible(false);
        SetUnderlineAlpha(0f);
        SetUnderlineWidth(0f);
    }

    public void OnSelect(BaseEventData eventData)
    {
        SetTriangleVisible(true);
        PlayUnderlineIn();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        SetTriangleVisible(false);
        StopUnderline();
        SetUnderlineAlpha(0f);
        SetUnderlineWidth(0f);
    }

    private void PlayUnderlineIn()
    {
        if (_underlineRect == null) return;

        StopUnderline();
        SetUnderlineAlpha(1f);
        SetUnderlineWidth(0f);

        float targetWidth = optionText != null ? optionText.preferredWidth : _underlineRect.parent.GetComponent<RectTransform>().rect.width;
        _underlineCoroutine = StartCoroutine(AnimateWidth(0f, targetWidth, underlineAnimDuration));
    }

    private void StopUnderline()
    {
        if (_underlineCoroutine != null)
        {
            StopCoroutine(_underlineCoroutine);
            _underlineCoroutine = null;
        }
    }

    private IEnumerator AnimateWidth(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetUnderlineWidth(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetUnderlineWidth(to);
    }

    private void SetUnderlineWidth(float width)
    {
        if (_underlineRect == null) return;
        var sd = _underlineRect.sizeDelta;
        sd.x = width;
        _underlineRect.sizeDelta = sd;
    }

    private void SetUnderlineAlpha(float alpha)
    {
        if (selectUnderlineImage == null) return;
        var c = selectUnderlineImage.color;
        c.a = alpha;
        selectUnderlineImage.color = c;
    }

    private void SetTriangleVisible(bool visible)
    {
        if (selectTriangleImage == null) return;
        var c = selectTriangleImage.color;
        c.a = visible ? 1f : 0f;
        selectTriangleImage.color = c;
    }
}