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
    [Header("Lights")]
    [SerializeField] private Light roomPointLight;
    [SerializeField] private Light roomDirectionalLight;
    [SerializeField] private Light windowLight;
    [SerializeField] private Light deskLight;

    [Header("Evening (저녁노을)")]
    [SerializeField] private float eveningRoomIntensity = 50f;
    [SerializeField] private Color eveningRoomColor = new Color(1f, 0.9f, 0.8f);
    [SerializeField] private float eveningWindowIntensity = 40f;
    [SerializeField] private Color eveningWindowColor = new Color(1f, 0.55f, 0.2f);
    [SerializeField] private Color eveningAmbientColor = new Color(0.4f, 0.25f, 0.15f);

    [Header("Night (깜깜한 밤)")]
    [SerializeField] private float nightRoomIntensity = 20f;
    [SerializeField] private Color nightRoomColor = new Color(0.7f, 0.7f, 0.85f);
    [SerializeField] private float nightWindowIntensity = 15f;
    [SerializeField] private Color nightWindowColor = new Color(0.6f, 0.7f, 0.9f);
    [SerializeField] private Color nightAmbientColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Morning (아침)")]
    [SerializeField] private float morningRoomIntensity = 100f;
    [SerializeField] private Color morningRoomColor = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private Color morningAmbientColor = new Color(0.6f, 0.6f, 0.7f);

    [Header("Transition")]
    [SerializeField] private float fadeDuration = 1.5f;

    private Coroutine _fadeCoroutine;

    private void Start()
    {
        SetEvening();
    }

    // ── 즉시 설정 ────────────────────────────────────────────────

    public void SetEvening()
    {
        StopFade();
        ApplyLighting(
            eveningRoomIntensity, eveningRoomColor, 0.5f,
            eveningWindowIntensity, eveningWindowColor, 1f,
            eveningAmbientColor);
    }

    public void SetNight()
    {
        StopFade();
        ApplyLighting(
            nightRoomIntensity, nightRoomColor, 0.1f,
            nightWindowIntensity, nightWindowColor, 1f,
            nightAmbientColor);
    }

    public void SetMorning()
    {
        StopFade();
        ApplyLighting(
            morningRoomIntensity, morningRoomColor, 1f,
            0f, Color.white, 0f,
            morningAmbientColor);
    }

    // ── 페이드 전환 ──────────────────────────────────────────────

    public void TransitionToEvening() => TransitionToEvening(null);
    public void TransitionToEvening(Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            eveningRoomIntensity, eveningRoomColor, 0.5f,
            eveningWindowIntensity, eveningWindowColor, 1f,
            eveningAmbientColor, onComplete));
    }

    public void TransitionToNight() => TransitionToNight(null);
    public void TransitionToNight(Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            nightRoomIntensity, nightRoomColor, 0.1f,
            nightWindowIntensity, nightWindowColor, 1f,
            nightAmbientColor, onComplete));
    }

    public void TransitionToMorning() => TransitionToMorning(null);
    public void TransitionToMorning(Action onComplete)
    {
        StopFade();
        _fadeCoroutine = StartCoroutine(FadeToPreset(
            morningRoomIntensity, morningRoomColor, 1f,
            0f, Color.white, 0f,
            morningAmbientColor, onComplete));
    }

    // ── Private ──────────────────────────────────────────────────

    private void ApplyLighting(
        float roomIntensity, Color roomColor,
        float dirIntensity,
        float windowIntensity, Color windowColor,
        float deskIntensity,
        Color ambientColor)
    {
        if (roomPointLight != null)
        {
            roomPointLight.intensity = roomIntensity;
            roomPointLight.color = roomColor;
        }
        if (roomDirectionalLight != null)
        {
            roomDirectionalLight.intensity = dirIntensity;
        }
        if (windowLight != null)
        {
            windowLight.gameObject.SetActive(windowIntensity > 0f);
            windowLight.intensity = windowIntensity;
            windowLight.color = windowColor;
        }
        if (deskLight != null)
        {
            deskLight.gameObject.SetActive(deskIntensity > 0f);
            deskLight.intensity = deskIntensity;
        }
        RenderSettings.ambientLight = ambientColor;
    }

    private void StopFade()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }

    private IEnumerator FadeToPreset(
        float targetRoomIntensity, Color targetRoomColor, float targetDirIntensity,
        float targetWindowIntensity, Color targetWindowColor,
        float targetDeskIntensity,
        Color targetAmbientColor, Action onComplete)
    {
        float startRoomIntensity = roomPointLight != null ? roomPointLight.intensity : 0f;
        Color startRoomColor = roomPointLight != null ? roomPointLight.color : Color.white;
        float startDirIntensity = roomDirectionalLight != null ? roomDirectionalLight.intensity : 0f;
        float startWindowIntensity = windowLight != null ? windowLight.intensity : 0f;
        Color startWindowColor = windowLight != null ? windowLight.color : Color.white;
        float startDeskIntensity = deskLight != null ? deskLight.intensity : 0f;
        Color startAmbient = RenderSettings.ambientLight;

        if (windowLight != null && targetWindowIntensity > 0f)
            windowLight.gameObject.SetActive(true);
        if (deskLight != null && targetDeskIntensity > 0f)
            deskLight.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            if (roomPointLight != null)
            {
                roomPointLight.intensity = Mathf.Lerp(startRoomIntensity, targetRoomIntensity, t);
                roomPointLight.color = Color.Lerp(startRoomColor, targetRoomColor, t);
            }
            if (roomDirectionalLight != null)
            {
                roomDirectionalLight.intensity = Mathf.Lerp(startDirIntensity, targetDirIntensity, t);
            }
            if (windowLight != null)
            {
                windowLight.intensity = Mathf.Lerp(startWindowIntensity, targetWindowIntensity, t);
                windowLight.color = Color.Lerp(startWindowColor, targetWindowColor, t);
            }
            if (deskLight != null)
            {
                deskLight.intensity = Mathf.Lerp(startDeskIntensity, targetDeskIntensity, t);
            }
            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbientColor, t);
            yield return null;
        }

        ApplyLighting(targetRoomIntensity, targetRoomColor, targetDirIntensity,
            targetWindowIntensity, targetWindowColor, targetDeskIntensity,
            targetAmbientColor);
        onComplete?.Invoke();
    }
}