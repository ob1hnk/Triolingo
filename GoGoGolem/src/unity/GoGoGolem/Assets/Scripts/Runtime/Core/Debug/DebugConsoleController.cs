using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using IngameDebugConsole;
using UnityEngine.InputSystem;
#endif

public class DebugConsoleController : MonoBehaviour
{
    public static DebugConsoleController Instance { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private GameState previousState;
#endif

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        var manager = DebugLogManager.Instance;
        if (manager == null) return;

        if (manager.IsLogWindowVisible)
        {
            manager.HideLogWindow();
            if (GameStateManager.Instance != null
                && GameStateManager.Instance.CurrentState == GameState.DebugConsole)
            {
                GameStateManager.Instance.ChangeState(previousState);
            }
        }
        else
        {
            if (GameStateManager.Instance != null)
            {
                previousState = GameStateManager.Instance.CurrentState;
                GameStateManager.Instance.ChangeState(GameState.DebugConsole);
            }
            manager.ShowLogWindow();
        }
    }

    [ConsoleMethod("scene.load", "씬 이름으로 씬 전환. 예: scene.load Room  / scene.load Forest")]
    public static void Cmd_LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[scene.load] 씬 이름을 입력하세요. 예: scene.load Room");
            return;
        }

        var console = DebugLogManager.Instance;
        if (console != null && console.IsLogWindowVisible)
            console.HideLogWindow();

        if (GameStateManager.Instance?.CurrentState == GameState.DebugConsole)
            GameStateManager.Instance.ChangeState(GameState.Gameplay);

        Debug.Log($"[scene.load] {sceneName} 로드 중...");
        SceneManager.LoadScene(sceneName);
    }
#endif
}
