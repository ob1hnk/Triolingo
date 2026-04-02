using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("Settings — Webcam Preview")]
    [Tooltip("설정창 내 웹캠 미리보기 RawImage (제스처 씬에서만 활성)")]
    [SerializeField] private RawImage webcamPreview;

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
        Time.timeScale = 0f; // 게임 일시정지
        if (settingsPanel != null) settingsPanel.SetActive(true);
        InitializeCameraDropdown();
        BindWebcamPreview();
        OnVisibilityChanged?.Invoke(true);
    }

    public void Hide()
    {
        _isVisible = false;
        Time.timeScale = 1f; // 게임 재개
        UnbindWebcamPreview();
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

    private Coroutine _webcamBindCoroutine;

    /// <summary>
    /// 제스처 씬에서만: GestureDetector의 웹캠 텍스처를 미리보기 RawImage에 연결
    /// 텍스처가 아직 준비 안 됐으면 준비될 때까지 대기
    /// </summary>
    private void BindWebcamPreview()
    {
        if (webcamPreview == null) return;

        var detector = FindObjectOfType<Demo.GestureDetection.GestureDetector>();
        if (detector == null)
        {
            webcamPreview.gameObject.SetActive(false);
            return;
        }

        var tex = detector.GetWebcamTexture();
        if (tex != null)
        {
            ApplyWebcamTexture(tex);
            return;
        }

        // 텍스처가 아직 없으면 (로딩 중) 준비될 때까지 대기
        webcamPreview.gameObject.SetActive(false);
        _webcamBindCoroutine = StartCoroutine(WaitForWebcamTexture(detector));
    }

    private System.Collections.IEnumerator WaitForWebcamTexture(Demo.GestureDetection.GestureDetector detector)
    {
        while (_isVisible)
        {
            var tex = detector.GetWebcamTexture();
            if (tex != null)
            {
                ApplyWebcamTexture(tex);
                yield break;
            }
            yield return null;
        }
    }

    private void ApplyWebcamTexture(Texture tex)
    {
        webcamPreview.texture = tex;
        // 전면 카메라 반전 보정: 좌우 반전된 uvRect 적용
        webcamPreview.uvRect = new Rect(1, 0, -1, 1);
        webcamPreview.gameObject.SetActive(true);
    }

    private void UnbindWebcamPreview()
    {
        if (_webcamBindCoroutine != null)
        {
            StopCoroutine(_webcamBindCoroutine);
            _webcamBindCoroutine = null;
        }

        if (webcamPreview == null) return;
        webcamPreview.texture = null;
        webcamPreview.gameObject.SetActive(false);
    }
}