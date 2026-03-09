using UnityEngine;

/// <summary>
/// 게임 전체를 총괄하는 싱글톤 매니저.
/// 플레이어 데이터(편지 ID, 제스처 튜토리얼 완료 여부 등)를 관리하고 PlayerPrefs로 영속화한다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private const string SaveKey = "PlayerData";

    private PlayerData _data;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    // ── Letter ────────────────────────────────────────────────

    public string CurrentLetterId => _data.currentLetterId;

    public void SetLetterId(string taskId)
    {
        _data.currentLetterId = taskId;
        Save();
    }

    public void ClearLetterId()
    {
        _data.currentLetterId = null;
        Save();
    }

    // ── Gesture Tutorial ──────────────────────────────────────

    public bool IsGestureLearned(string gestureTypeName)
    {
        return _data.learnedGestures.Contains(gestureTypeName);
    }

    public void MarkGestureLearned(string gestureTypeName)
    {
        if (_data.learnedGestures.Contains(gestureTypeName)) return;
        _data.learnedGestures.Add(gestureTypeName);
        Save();
        Debug.Log($"[GameManager] Gesture learned: {gestureTypeName}");
    }

    [ContextMenu("Reset All Player Data")]
    public void ResetAllData()
    {
        _data = new PlayerData();
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Debug.Log("[GameManager] All player data reset.");
    }

    // ── Save / Load ───────────────────────────────────────────

    private void Save()
    {
        var json = JsonUtility.ToJson(_data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (PlayerPrefs.HasKey(SaveKey))
        {
            var json = PlayerPrefs.GetString(SaveKey);
            _data = JsonUtility.FromJson<PlayerData>(json);
        }
        else
        {
            _data = new PlayerData();
        }
    }
}