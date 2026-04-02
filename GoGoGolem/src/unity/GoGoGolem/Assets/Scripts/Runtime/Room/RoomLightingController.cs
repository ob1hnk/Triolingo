using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Room 씬 조명 제어
///
/// 3가지 조명 프리셋:
///   Evening (BeforeLetter) — 저녁노을
///   Night   (AfterLetter)  — 깜깜한 밤
///   Morning (Morning)      — 아침
/// </summary>
public class RoomLightingController : MonoBehaviour
{
    [Header("Sun Light")]
    [SerializeField] private Light sunLight;

    [Header("Evening (저녁노을)")]
    [SerializeField] private float eveningLightIntensity = 0.8f;
    [SerializeField] private Color eveningLightColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField] private Color eveningAmbientColor = new Color(0.4f, 0.25f, 0.15f);

    [Header("Night (깜깜한 밤)")]
    [SerializeField] private float nightLightIntensity = 0f;
    [SerializeField] private Color nightAmbientColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Morning (아침)")]
    [SerializeField] private float morningLightIntensity = 1.2f;
    [SerializeField] private Color morningLightColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color morningAmbientColor = new Color(0.6f, 0.6f, 0.7f);

    [Header("Night View")]
    [SerializeField] private GameObject nightView;

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 1.5f;

    private Coroutine _fadeCoroutine;

    // ── 즉시 설정 ────────────────────────────────────────────────

    public void SetEvening()
    {
        StopFade();
        ApplyLighting(eveningLightIntensity, eveningLightColor, eveningAmbientColor, sunActive: true, nightViewActive: false);
    }

    public void SetNight()
    {
        StopFade();
        ApplyLighting(nightLightIntensity, Color.white, nightAmbientColor, sunActive: false, nightViewActive: true);
    }

    public void SetMorning()
    {
        StopFade();
        ApplyLighting(morningLightIntensity, morningLightColor, morningAmbientColor, sunActive: true, nightViewActive: false);
    }

    // ── 페이드 전환 ──────────────────────────────────────────────

    public void TransitionToEvening() => TransitionToEvening(null);
    public void TransitionToEvening(Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            eveningLightIntensity, eveningLightColor, eveningAmbientColor,
            sunActive: true, nightViewActive: false, onComplete));
    }

    public void TransitionToNight() => TransitionToNight(null);
    public void TransitionToNight(Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            nightLightIntensity, Color.white, nightAmbientColor,
            sunActive: false, nightViewActive: true, onComplete));
    }

    public void TransitionToMorning() => TransitionToMorning(null);
    public void TransitionToMorning(Action onComplete)
    {
        StopFade();
        if (sunLight != null) sunLight.gameObject.SetActive(true);
        if (nightView != null) nightView.SetActive(false);
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            morningLightIntensity, morningLightColor, morningAmbientColor,
            sunActive: true, nightViewActive: false, onComplete));
    }

    // ── Private ──────────────────────────────────────────────────

    private void ApplyLighting(float intensity, Color lightColor, Color ambientColor, bool sunActive, bool nightViewActive)
    {
        if (sunLight != null)
        {
            sunLight.gameObject.SetActive(sunActive);
            sunLight.intensity = intensity;
            sunLight.color = lightColor;
        }
        RenderSettings.ambientLight = ambientColor;
        if (nightView != null) nightView.SetActive(nightViewActive);
    }

    private void StopFade()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }

    private IEnumerator FadeToPreset(float targetIntensity, Color targetLightColor, Color targetAmbientColor,
        bool sunActive, bool nightViewActive, Action onComplete)
    {
        // 페이드 시작 전 sun 켜기 (꺼져있으면 페이드가 안 보이므로)
        if (sunActive && sunLight != null) sunLight.gameObject.SetActive(true);

        float startIntensity = sunLight != null ? sunLight.intensity : 0f;
        Color startLightColor = sunLight != null ? sunLight.color : Color.white;
        Color startAmbient = RenderSettings.ambientLight;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
                sunLight.color = Color.Lerp(startLightColor, targetLightColor, t);
            }
            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbientColor, t);
            yield return null;
        }

        // 최종값 적용
        ApplyLighting(targetIntensity, targetLightColor, targetAmbientColor, sunActive, nightViewActive);
        onComplete?.Invoke();
    }
}