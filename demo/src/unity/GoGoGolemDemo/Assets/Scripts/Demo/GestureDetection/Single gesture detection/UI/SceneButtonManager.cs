using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Demo.GestureDetection.UI
{
  /// <summary>
  /// 씬 전환 버튼 관리자 - 현재 씬에 따라 버튼 활성화/비활성화
  /// </summary>
  public class SceneButtonManager : MonoBehaviour
  {
    [Header("Scene Names")]
    [SerializeField] private string _jangpoongSceneName = "JangpoongScene";
    [SerializeField] private string _liftUpSceneName = "LiftUpScene";

    [Header("Scene Buttons")]
    [SerializeField] private Button _jangpoongButton;
    [SerializeField] private Button _liftUpButton;

    [Header("Button Colors")]
    [SerializeField] private Color _activeButtonColor = Color.white;
    [SerializeField] private Color _inactiveButtonColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Transition Settings")]
    [SerializeField] private float _transitionDelay = 0.3f;

    private string _currentSceneName;

    private void Start()
    {
      _currentSceneName = SceneManager.GetActiveScene().name;
      
      SetupButtons();
      UpdateButtonStates();
      
      Debug.Log($"[SceneButtonManager] Current scene: {_currentSceneName}");
    }

    /// <summary>
    /// 버튼 리스너 등록
    /// </summary>
    private void SetupButtons()
    {
      if (_jangpoongButton != null)
      {
        _jangpoongButton.onClick.RemoveAllListeners();
        _jangpoongButton.onClick.AddListener(() => LoadScene(_jangpoongSceneName));
      }

      if (_liftUpButton != null)
      {
        _liftUpButton.onClick.RemoveAllListeners();
        _liftUpButton.onClick.AddListener(() => LoadScene(_liftUpSceneName));
      }
    }

    /// <summary>
    /// 현재 씬에 따라 버튼 상태 업데이트
    /// </summary>
    private void UpdateButtonStates()
    {
      // 장풍 버튼
      if (_jangpoongButton != null)
      {
        bool isCurrentScene = _currentSceneName == _jangpoongSceneName;
        _jangpoongButton.interactable = !isCurrentScene;
        SetButtonColor(_jangpoongButton, isCurrentScene ? _inactiveButtonColor : _activeButtonColor);
      }

      // 들어올리기 버튼
      if (_liftUpButton != null)
      {
        bool isCurrentScene = _currentSceneName == _liftUpSceneName;
        _liftUpButton.interactable = !isCurrentScene;
        SetButtonColor(_liftUpButton, isCurrentScene ? _inactiveButtonColor : _activeButtonColor);
      }
    }

    /// <summary>
    /// 버튼 색상 설정
    /// </summary>
    private void SetButtonColor(Button button, Color color)
    {
      if (button == null) return;

      var colors = button.colors;
      colors.normalColor = color;
      colors.highlightedColor = color * 1.2f;
      colors.pressedColor = color * 0.8f;
      colors.disabledColor = _inactiveButtonColor;
      button.colors = colors;

      // 버튼 내부 Text 색상도 변경 (선택사항)
      var buttonText = button.GetComponentInChildren<Text>();
      if (buttonText != null)
      {
        buttonText.color = button.interactable ? Color.black : Color.gray;
      }
    }

    /// <summary>
    /// 씬 로드
    /// </summary>
    private void LoadScene(string sceneName)
    {
      if (string.IsNullOrEmpty(sceneName))
      {
        Debug.LogError($"[SceneButtonManager] Scene name is empty!");
        return;
      }

      // 현재 씬과 같으면 무시
      if (sceneName == _currentSceneName)
      {
        Debug.Log($"[SceneButtonManager] Already in scene: {sceneName}");
        return;
      }

      Debug.Log($"[SceneButtonManager] Loading scene: {sceneName}");

      if (_transitionDelay > 0)
      {
        StartCoroutine(LoadSceneWithDelay(sceneName, _transitionDelay));
      }
      else
      {
        SceneManager.LoadScene(sceneName);
      }
    }

    /// <summary>
    /// 현재 씬 다시 로드 (리셋용)
    /// </summary>
    public void ReloadCurrentScene()
    {
      Debug.Log($"[SceneButtonManager] Reloading current scene: {_currentSceneName}");
      SceneManager.LoadScene(_currentSceneName);
    }

    /// <summary>
    /// 키보드 단축키 (테스트용)
    /// </summary>
    private void Update()
    {
      // 1번: 장풍 씬
      if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        LoadScene(_jangpoongSceneName);

      // 2번: 들어올리기 씬
      if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        LoadScene(_liftUpSceneName);

      // R: 현재 씬 리로드
      if (Input.GetKeyDown(KeyCode.R))
        ReloadCurrentScene();
    }

    /// <summary>
    /// 버튼 상태 강제 업데이트 (외부에서 호출 가능)
    /// </summary>
    public void RefreshButtonStates()
    {
      _currentSceneName = SceneManager.GetActiveScene().name;
      UpdateButtonStates();
    }

    private System.Collections.IEnumerator LoadSceneWithDelay(string sceneName, float delay)
    {
      yield return new WaitForSeconds(delay);
      SceneManager.LoadScene(sceneName);
    }
  }
}
