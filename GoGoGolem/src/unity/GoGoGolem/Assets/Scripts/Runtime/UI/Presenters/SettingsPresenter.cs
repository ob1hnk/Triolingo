using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 설정 패널 Presenter.
/// GameState.Paused 상태에서 UIManager에 의해 Show/Hide된다.
/// 카메라 선택은 WebCamTexture.devices를 통해 열거하고, PlayerPrefs로 저장한다.
/// Gesture Detection 씬의 Bootstrap이 저장된 이름을 읽어 적용한다.
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    private const string PrefKeyCamera = "Settings_CameraName";

    [SerializeField] private GameObject settingsPanel;

    [Header("Settings — Camera Input")]
    [SerializeField] private TMP_Dropdown cameraDropdown;

    [Header("Settings — Mic Input")]
    // TODO: 마이크 입력 선택 UI 연결

    private bool _isVisible = false;
    public bool IsVisible => _isVisible;
    public event System.Action<bool> OnVisibilityChanged;

    private void Awake()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void Show()
    {
        _isVisible = true;
        if (settingsPanel != null) settingsPanel.SetActive(true);
        InitializeCameraDropdown();
        OnVisibilityChanged?.Invoke(true);
    }

    public void Hide()
    {
        _isVisible = false;
        if (settingsPanel != null) settingsPanel.SetActive(false);
        OnVisibilityChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (_isVisible) Hide();
        else Show();
    }

    private void InitializeCameraDropdown()
    {
        if (cameraDropdown == null) return;

        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            cameraDropdown.interactable = false;
            return;
        }

        var names = System.Array.ConvertAll(devices, d => d.name);

        cameraDropdown.onValueChanged.RemoveAllListeners();
        cameraDropdown.ClearOptions();
        cameraDropdown.AddOptions(new List<string>(names));
        cameraDropdown.interactable = true;

        // 저장된 카메라 이름으로 초기 선택값 복원
        string savedName = PlayerPrefs.GetString(PrefKeyCamera, names[0]);
        int savedIndex = System.Array.IndexOf(names, savedName);
        cameraDropdown.SetValueWithoutNotify(savedIndex >= 0 ? savedIndex : 0);
        cameraDropdown.RefreshShownValue();

        cameraDropdown.onValueChanged.AddListener(index =>
        {
            PlayerPrefs.SetString(PrefKeyCamera, names[index]);
            PlayerPrefs.Save();
        });
    }
}
