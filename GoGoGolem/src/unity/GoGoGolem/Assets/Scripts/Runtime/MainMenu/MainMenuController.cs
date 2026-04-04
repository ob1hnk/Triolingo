using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string introSceneName = "Intro";
    [SerializeField] private SettingsPresenter settingsPresenter;
    [SerializeField] private PrivacyConsentPresenter privacyConsentPresenter;

    private bool _pendingStart;

    private void OnEnable()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.OnConsented += OnPrivacyConsented;
    }

    private void OnDisable()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.OnConsented -= OnPrivacyConsented;
    }

    public void OnStartGame()
    {
        if (!PrivacyConsentPresenter.HasConsented)
        {
            _pendingStart = true;
            if (privacyConsentPresenter != null)
                privacyConsentPresenter.Show();
            return;
        }

        SceneManager.LoadScene(introSceneName);
    }

    public void OnSettings()
    {
        if (settingsPresenter != null)
            settingsPresenter.Toggle();
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnPrivacyPolicy()
    {
        if (privacyConsentPresenter != null)
            privacyConsentPresenter.Show();
    }

    private void OnPrivacyConsented()
    {
        if (_pendingStart)
        {
            _pendingStart = false;
            SceneManager.LoadScene(introSceneName);
        }
    }
}
