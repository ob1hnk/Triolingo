using UnityEngine;

[CreateAssetMenu(fileName = "DoorSoundData", menuName = "Audio/Door Sound Data")]
public class DoorSoundData : ScriptableObject
{
    public AudioClip enterClip;
    public AudioClip exitClip;
    [Range(0f, 1f)] public float volume = 1f;
}
