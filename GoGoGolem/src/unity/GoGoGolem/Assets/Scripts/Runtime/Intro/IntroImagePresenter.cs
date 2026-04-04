using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 인트로 씬에서 <<show_image N>> / <<hide_image>> 커맨드로
/// 검은 배경 위에 2D 카드 이미지를 표시한다.
/// </summary>
public class IntroImagePresenter : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;

    [Tooltip("이미지 표시용 패널 (검은 배경 + Image 자식). CanvasGroup 필요")]
    [SerializeField] private CanvasGroup imagePanel;

    [Tooltip("실제 스프라이트가 표시되는 UI Image")]
    [SerializeField] private Image displayImage;

    [Tooltip("1_card ~ 8_card 스프라이트를 순서대로 할당 (index 0 = 1_card)")]
    [SerializeField] private Sprite[] cardSprites;

    [SerializeField] private float fadeDuration = 0.3f;

    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        imagePanel.alpha = 0f;
        imagePanel.gameObject.SetActive(false);

        dialogueRunner.AddCommandHandler<int>("show_image", ShowImage);
        dialogueRunner.AddCommandHandler("hide_image", HideImage);
    }

    private IEnumerator ShowImage(int cardNumber)
    {
        int index = cardNumber - 1;
        if (index < 0 || index >= cardSprites.Length)
        {
            Debug.LogWarning($"[IntroImagePresenter] 잘못된 카드 번호: {cardNumber}");
            yield break;
        }

        // 이미 표시 중이면 빠르게 페이드아웃 후 교체
        if (imagePanel.gameObject.activeSelf && imagePanel.alpha > 0f)
        {
            yield return FadeTo(0f);
        }

        displayImage.sprite = cardSprites[index];
        displayImage.preserveAspect = true;
        imagePanel.gameObject.SetActive(true);

        yield return FadeTo(1f);
    }

    private IEnumerator HideImage()
    {
        yield return FadeTo(0f);
        imagePanel.gameObject.SetActive(false);
    }

    private IEnumerator FadeTo(float target)
    {
        float start = imagePanel.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            imagePanel.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        imagePanel.alpha = target;
    }
}
