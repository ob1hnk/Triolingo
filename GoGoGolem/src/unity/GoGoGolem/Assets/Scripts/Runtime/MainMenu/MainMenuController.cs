using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string introSceneName = "Intro";

    public void OnStartGame()
    {
        SceneManager.LoadScene(introSceneName);
    }

    public void OnSettings()
    {
        // TODO: 설정 창 구현 예정
        Debug.Log("설정 버튼 클릭");
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
        // TODO: 개인정보처리방침 구현 예정
        Debug.Log("개인정보처리방침 클릭");
    }
}
