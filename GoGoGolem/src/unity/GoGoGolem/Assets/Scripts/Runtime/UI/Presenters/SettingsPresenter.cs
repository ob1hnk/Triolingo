using UnityEngine;

/// <summary>
/// 설정 패널 Presenter.
/// GameState.Paused 상태에서 UIManager에 의해 Show/Hide된다.
/// 카메라 입력, 마이크 입력 등 설정 항목은 추후 구현.
/// </summary>
public class SettingsPresenter : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings — Camera Input")]
    // TODO: 카메라 입력 선택 UI 연결

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
}
