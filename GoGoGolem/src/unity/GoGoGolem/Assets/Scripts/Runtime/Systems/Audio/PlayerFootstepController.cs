using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootstepController : MonoBehaviour
{
    [System.Serializable]
    struct SceneFootstepEntry
    {
        public string sceneName;
        public FootstepData footstepData;
    }

    [SerializeField] SceneFootstepEntry[] _sceneFootsteps;
    [SerializeField] FootstepData _defaultFootstepData;

    AudioSource _audioSource;
    PlayerState _playerState;
    FootstepData _currentData;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _playerState = GetComponent<PlayerState>();
    }

    void Start() => UpdateCurrentData(SceneManager.GetActiveScene().name);

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => UpdateCurrentData(scene.name);

    void UpdateCurrentData(string sceneName)
    {
        foreach (var entry in _sceneFootsteps)
        {
            if (entry.sceneName == sceneName)
            {
                _currentData = entry.footstepData;
                return;
            }
        }
        _currentData = _defaultFootstepData;
    }

    // AnimationEvent에서 호출
    void PlayFootstepSound()
    {
        if (_currentData == null || _currentData.clips.Length == 0) return;
        if (!_playerState.InGroundedState()) return;

        var clip = _currentData.clips[Random.Range(0, _currentData.clips.Length)];
        _audioSource.pitch = 1f + Random.Range(-_currentData.pitchVariance, _currentData.pitchVariance);
        _audioSource.PlayOneShot(clip, _currentData.volume);
    }
}
