using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창 웹캠 미리보기 전용 Presenter.
///
/// - GestureDetector가 씬에 있으면 (제스처 씬): 카메라를 새로 열지 않고
///   GestureDetector.GetWebcamTexture()로 텍스처를 빌려서 표시한다.
/// - GestureDetector가 없으면 (다른 씬): 자체 WebCamTexture를 열어서 표시한다.
///
/// 세팅:
///   1. 웹캠 미리보기가 필요한 씬의 SettingsPanel 오브젝트에 이 컴포넌트 추가
///   2. Webcam Preview  → 미리보기용 RawImage 연결
///   3. Camera Dropdown → 카메라 선택 TMP_Dropdown 연결 (SettingsPresenter와 공유)
///   4. Settings Presenter → SettingsPresenter 연결 (같은 오브젝트면 자동 탐색)
/// </summary>
public class WebcamPreviewPresenter : MonoBehaviour
{
    [SerializeField] private RawImage webcamPreview;
    [SerializeField] private TMP_Dropdown cameraDropdown;
    [SerializeField] private SettingsPresenter settingsPresenter;

    // 자체 WebCamTexture (GestureDetector 없는 씬에서만 사용)
    private WebCamTexture _ownedTexture;

    // 텍스처 대기 코루틴 (GestureDetector 텍스처가 아직 준비 안 됐을 때)
    private Coroutine _waitCoroutine;

    // ───────────────────────────────────────────
    // 생명주기
    // ───────────────────────────────────────────

    private void Awake()
    {
        if (settingsPresenter == null)
            settingsPresenter = GetComponentInParent<SettingsPresenter>();

        if (webcamPreview != null)
            webcamPreview.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (settingsPresenter != null)
            settingsPresenter.OnVisibilityChanged += OnVisibilityChanged;
    }

    private void OnDisable()
    {
        if (settingsPresenter != null)
            settingsPresenter.OnVisibilityChanged -= OnVisibilityChanged;

        StopPreview();
    }

    private void OnDestroy()
    {
        ReleaseOwnedTexture();
    }

    // ───────────────────────────────────────────
    // 설정창 표시 콜백
    // ───────────────────────────────────────────

    private void OnVisibilityChanged(bool isVisible)
    {
        if (isVisible) StartPreview();
        else StopPreview();
    }

    // ───────────────────────────────────────────
    // 미리보기 시작 / 정지
    // ───────────────────────────────────────────

    private void StartPreview()
    {
        if (webcamPreview == null) return;

        var detector = FindObjectOfType<Demo.GestureDetection.GestureDetector>();

        if (detector != null)
        {
            // 제스처 씬: GestureDetector 텍스처 빌려쓰기
            var tex = detector.GetWebcamTexture();
            if (tex != null)
            {
                ApplyTexture(tex);
            }
            else
            {
                // 텍스처가 아직 준비 안 됐으면 대기
                webcamPreview.gameObject.SetActive(false);
                _waitCoroutine = StartCoroutine(WaitForDetectorTexture(detector));
            }
        }
        else
        {
            // 다른 씬: 자체 WebCamTexture 열기
            StartOwnedPreview(GetSelectedCameraName());

            if (cameraDropdown != null)
            {
                cameraDropdown.onValueChanged.RemoveListener(OnCameraDropdownChanged);
                cameraDropdown.onValueChanged.AddListener(OnCameraDropdownChanged);
            }
        }
    }

    private void StopPreview()
    {
        if (_waitCoroutine != null)
        {
            StopCoroutine(_waitCoroutine);
            _waitCoroutine = null;
        }

        if (cameraDropdown != null)
            cameraDropdown.onValueChanged.RemoveListener(OnCameraDropdownChanged);

        ReleaseOwnedTexture();

        if (webcamPreview == null) return;
        webcamPreview.texture = null;
        webcamPreview.gameObject.SetActive(false);
    }

    // ───────────────────────────────────────────
    // GestureDetector 텍스처 대기 (제스처 씬)
    // ───────────────────────────────────────────

    private IEnumerator WaitForDetectorTexture(Demo.GestureDetection.GestureDetector detector)
    {
        while (settingsPresenter != null && settingsPresenter.IsVisible)
        {
            var tex = detector.GetWebcamTexture();
            if (tex != null)
            {
                ApplyTexture(tex);
                yield break;
            }
            yield return null;
        }
    }

    // ───────────────────────────────────────────
    // 자체 WebCamTexture (다른 씬)
    // ───────────────────────────────────────────

    private void StartOwnedPreview(string deviceName)
    {
        ReleaseOwnedTexture();

        _ownedTexture = string.IsNullOrEmpty(deviceName)
            ? new WebCamTexture()
            : new WebCamTexture(deviceName);

        _ownedTexture.Play();
        ApplyTexture(_ownedTexture);
    }

    private void ReleaseOwnedTexture()
    {
        if (_ownedTexture == null) return;
        if (_ownedTexture.isPlaying) _ownedTexture.Stop();
        Destroy(_ownedTexture);
        _ownedTexture = null;
    }

    // ───────────────────────────────────────────
    // RawImage 적용
    // ───────────────────────────────────────────

    private void ApplyTexture(Texture tex)
    {
        webcamPreview.texture = tex;
        webcamPreview.uvRect = new Rect(1, 0, -1, 1); // 전면 카메라 좌우 반전 보정
        webcamPreview.gameObject.SetActive(true);
    }

    // ───────────────────────────────────────────
    // 카메라 전환 (다른 씬에서만 동작)
    // ───────────────────────────────────────────

    private void OnCameraDropdownChanged(int _)
    {
        StartOwnedPreview(GetSelectedCameraName());
    }

    private string GetSelectedCameraName()
    {
        if (cameraDropdown == null) return string.Empty;
        var options = cameraDropdown.options;
        int index = cameraDropdown.value;
        if (options == null || options.Count == 0 || index < 0 || index >= options.Count)
            return string.Empty;
        return options[index].text;
    }
}