using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(AudioSource))]
public class GolemFootstepController : MonoBehaviour
{
    [SerializeField] FootstepData _footstepData;

    AudioSource _audioSource;
    NavMeshAgent _agent;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _agent = GetComponent<NavMeshAgent>();
    }

    // AnimationEvent에서 호출
    void PlayFootstepSound()
    {
        if (_footstepData == null || _footstepData.clips.Length == 0) return;

        var clip = _footstepData.clips[Random.Range(0, _footstepData.clips.Length)];
        _audioSource.pitch = 1f + Random.Range(-_footstepData.pitchVariance, _footstepData.pitchVariance);
        _audioSource.PlayOneShot(clip, _footstepData.volume);
    }
}
