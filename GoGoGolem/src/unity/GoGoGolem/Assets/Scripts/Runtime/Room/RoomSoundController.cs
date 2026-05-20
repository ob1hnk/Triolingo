using System.Collections;
using UnityEngine;

/// <summary>
/// Room 씬 BGM/앰비언트 레이어 제어
///
/// 레이어:
///   mainBGMSource  — 저녁/아침 브금
///   birdSource     — 아침(Morning)에만 mainBGM 위에 겹치는 새소리
///   nightBGMSource — 밤(AfterLetter) 브금
///
/// Inspector에서 설정한 volume을 최대 볼륨으로 유지.
/// RoomStateManager.OnStateChanged 이벤트를 구독하여 상태별로 크로스페이드.
/// </summary>
public class RoomSoundController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] AudioSource mainBGMSource;
    [SerializeField] AudioSource birdSource;
    [SerializeField] AudioSource nightBGMSource;

    [Header("Settings")]
    [SerializeField] float fadeDuration = 1.5f;

    [Header("References")]
    [SerializeField] RoomStateManager roomStateManager;

    const int MAIN = 0, BIRD = 1, NIGHT = 2;
    AudioSource[] _sources;
    Coroutine[] _fades;
    float[] _maxVolumes;

    void Awake()
    {
        _sources = new[] { mainBGMSource, birdSource, nightBGMSource };
        _fades = new Coroutine[3];
        // Inspector에서 설정한 volume을 최대값으로 저장
        _maxVolumes = new float[_sources.Length];
        for (int i = 0; i < _sources.Length; i++)
            _maxVolumes[i] = _sources[i] != null ? _sources[i].volume : 1f;
    }

    void OnEnable()
    {
        if (roomStateManager != null)
            roomStateManager.OnStateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (roomStateManager != null)
            roomStateManager.OnStateChanged -= HandleStateChanged;
    }

    void Start()
    {
        if (roomStateManager != null)
            ApplyState(roomStateManager.CurrentState, instant: true);
    }

    void HandleStateChanged(RoomStateManager.RoomState state) => ApplyState(state, instant: false);

    void ApplyState(RoomStateManager.RoomState state, bool instant)
    {
        switch (state)
        {
            case RoomStateManager.RoomState.BeforeLetter:
                FadeTo(MAIN,  _maxVolumes[MAIN],  instant);
                FadeTo(BIRD,  0f,                 instant);
                FadeTo(NIGHT, 0f,                 instant);
                break;
            case RoomStateManager.RoomState.AfterLetter:
                FadeTo(MAIN,  0f,                  instant);
                FadeTo(BIRD,  0f,                  instant);
                FadeTo(NIGHT, _maxVolumes[NIGHT],  instant);
                break;
            case RoomStateManager.RoomState.Morning:
                FadeTo(MAIN,  _maxVolumes[MAIN],  instant);
                FadeTo(BIRD,  _maxVolumes[BIRD],  instant);
                FadeTo(NIGHT, 0f,                 instant);
                break;
        }
    }

    void FadeTo(int idx, float target, bool instant)
    {
        var src = _sources[idx];
        if (src == null) return;

        if (_fades[idx] != null) StopCoroutine(_fades[idx]);

        if (instant)
        {
            src.volume = target;
            if (target > 0f && !src.isPlaying) src.Play();
            else if (target <= 0f) src.Stop();
            _fades[idx] = null;
            return;
        }

        _fades[idx] = StartCoroutine(FadeRoutine(idx, src, target));
    }

    IEnumerator FadeRoutine(int idx, AudioSource src, float target)
    {
        if (target > 0f && !src.isPlaying)
        {
            src.volume = 0f;
            src.Play();
        }
        else if (target <= 0f && src.volume <= 0f)
        {
            src.Stop();
            _fades[idx] = null;
            yield break;
        }

        float start = src.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            src.volume = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        src.volume = target;
        if (target <= 0f) src.Stop();
        _fades[idx] = null;
    }
}
