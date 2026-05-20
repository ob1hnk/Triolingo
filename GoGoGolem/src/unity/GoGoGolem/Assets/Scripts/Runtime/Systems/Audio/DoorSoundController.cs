using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DoorSoundController : MonoBehaviour
{
    [SerializeField] DoorSoundData _data;

    AudioSource _audioSource;

    void Awake() => _audioSource = GetComponent<AudioSource>();

    void Start() => PlayEnter();

    void PlayEnter()
    {
        if (_data == null || _data.enterClip == null) return;
        _audioSource.PlayOneShot(_data.enterClip, _data.volume);
    }

    public void PlayExit(Action onComplete)
    {
        if (_data == null || _data.exitClip == null)
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(PlayExitCoroutine(onComplete));
    }

    IEnumerator PlayExitCoroutine(Action onComplete)
    {
        _audioSource.PlayOneShot(_data.exitClip, _data.volume);
        yield return new WaitForSeconds(_data.exitClip.length);
        onComplete?.Invoke();
    }
}
