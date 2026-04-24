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

    // 현재 텍스처를 빌려준 GestureDetector (구독 해제용)
    private Demo.GestureDetection.GestureDetector _borrowedDetector;

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

        // 이전 구독이 남아있으면 먼저 해제 (중복 호출 방어)
        if (_borrowedDetector != null)
        {
            _borrowedDetector.OnCameraReady -= OnDetectorCameraReady;
            _borrowedDetector = null;
        }

        var detector = FindObjectOfType<Demo.GestureDetection.GestureDetector>();

        if (detector != null)
        {
            // 제스처 씬: GestureDetector 텍스처 빌려쓰기
            var tex = detector.GetWebcamTexture();
            if (tex != null)
            {
                // 이미 준비됨 → 즉시 표시
                ApplyTexture(tex);
            }
            else if (detector.IsCameraReady)
            {
                // OnCameraReady 이벤트가 이미 발동됨 (설정창을 늦게 열었을 때)
                // 이벤트를 기다리면 영영 오지 않으므로 핸들러를 직접 호출
                _borrowedDetector = detector;
                OnDetectorCameraReady();
            }
            else
            {
                // 아직 준비 안 됨 → OnCameraReady 이벤트로 대기 (폴링/timeScale 문제 없음)
                webcamPreview.gameObject.SetActive(false);
                _borrowedDetector = detector;
                detector.OnCameraReady += OnDetectorCameraReady;
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
        // GestureDetector 이벤트 구독 해제
        if (_borrowedDetector != null)
        {
            _borrowedDetector.OnCameraReady -= OnDetectorCameraReady;
            _borrowedDetector = null;
        }

        if (cameraDropdown != null)
            cameraDropdown.onValueChanged.RemoveListener(OnCameraDropdownChanged);

        ReleaseOwnedTexture();

        if (webcamPreview == null) return;
        webcamPreview.texture = null;
        webcamPreview.gameObject.SetActive(false);
    }

    // ───────────────────────────────────────────
    // GestureDetector 카메라 준비 완료 콜백 (제스처 씬)
    // ───────────────────────────────────────────

    private void OnDetectorCameraReady()
    {
        var detector = _borrowedDetector;

        // 이벤트는 한 번만 사용하므로 즉시 구독 해제
        if (detector != null)
        {
            detector.OnCameraReady -= OnDetectorCameraReady;
            _borrowedDetector = null;
        }

        // 설정창이 닫혔으면 표시 불필요
        if (settingsPresenter == null || !settingsPresenter.IsVisible) return;

        if (detector != null)
        {
            var tex = detector.GetWebcamTexture();
            if (tex != null) ApplyTexture(tex);
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