using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 패널 Presenter.
/// GameState.Paused 상태에서 UIManager에 의해 Show/Hide된다.
/// 카메라 선택은 WebCamTexture.devices를 통해 열거하고, PlayerPrefs로 저장한다.
/// Gesture Detection 씬의 Bootstrap이 저장된 이름을 읽어 적용한다.
///
/// 웹캠 미리보기는 WebcamPreviewBinder가 담당한다.
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    private const string PrefKeyCamera = "Settings_CameraName";
    private const string PrefKeyMic = "Settings_MicName";

    [SerializeField] private GameObject settingsPanel;

    [Header("Settings — Camera Input")]
    [SerializeField] private TMP_Dropdown cameraDropdown;

    [Header("Settings — Mic Input")]
    [SerializeField] private TMP_Dropdown micDropdown;

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
        Time.timeScale = 0f; // 게임 일시정지
        if (settingsPanel != null) settingsPanel.SetActive(true);
        InitializeCameraDropdown();
        StartCoroutine(RequestMicPermissionAndInit());
        OnVisibilityChanged?.Invoke(true);
    }

    public void Hide()
    {
        _isVisible = false;
        Time.timeScale = 1f; // 게임 재개
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

    private IEnumerator RequestMicPermissionAndInit()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }
        InitializeMicDropdown();
    }

    private void InitializeMicDropdown()
    {
        if (micDropdown == null) return;

        var devices = Microphone.devices;
        if (devices == null || devices.Length == 0)
        {
            micDropdown.interactable = false;
            return;
        }

        micDropdown.onValueChanged.RemoveAllListeners();
        micDropdown.ClearOptions();
        micDropdown.AddOptions(new List<string>(devices));
        micDropdown.interactable = true;

        string savedName = PlayerPrefs.GetString(PrefKeyMic, devices[0]);
        int savedIndex = System.Array.IndexOf(devices, savedName);
        micDropdown.SetValueWithoutNotify(savedIndex >= 0 ? savedIndex : 0);
        micDropdown.RefreshShownValue();

        micDropdown.onValueChanged.AddListener(index =>
        {
            PlayerPrefs.SetString(PrefKeyMic, devices[index]);
            PlayerPrefs.Save();
        });
    }

    /// <summary>
    /// 저장된 마이크 디바이스 이름을 반환한다. 저장값이 없으면 null.
    /// </summary>
    public static string GetSavedMicName()
    {
        return PlayerPrefs.HasKey(PrefKeyMic)
            ? PlayerPrefs.GetString(PrefKeyMic)
            : null;
    }

    /// <summary>
    /// 저장된 카메라 디바이스 이름을 반환한다. 저장값이 없으면 null.
    /// </summary>
    public static string GetSavedCameraName()
    {
        return PlayerPrefs.HasKey(PrefKeyCamera)
            ? PlayerPrefs.GetString(PrefKeyCamera)
            : null;
    }
}