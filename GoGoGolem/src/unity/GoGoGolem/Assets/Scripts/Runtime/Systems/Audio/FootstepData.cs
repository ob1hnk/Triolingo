using UnityEngine;

[CreateAssetMenu(fileName = "FootstepData", menuName = "Audio/Footstep Data")]
public class FootstepData : ScriptableObject
{
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Range(0f, 0.3f)] public float pitchVariance = 0.1f;
}
