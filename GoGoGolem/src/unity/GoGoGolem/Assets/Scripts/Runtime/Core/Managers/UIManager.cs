using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] private InventoryUIPresenter inventoryPresenter;
    [SerializeField] private QuestUIPresenter questPresenter;
    [SerializeField] private SettingsPresenter settingsPresenter;

    [Header("Event Channels")]
    [SerializeField] private GameStateChangeEvent onGameStateChangedEvent;

    public InventoryUIPresenter Inventory => inventoryPresenter;

    private void Start()
    {
        FindPresenters();
    }

    private void OnEnable()
    {
        if (onGameStateChangedEvent != null)
            onGameStateChangedEvent.Register(OnGameStateChanged);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (onGameStateChangedEvent != null)
            onGameStateChangedEvent.Unregister(OnGameStateChanged);
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 전환 후 Presenter 참조가 끊어졌을 수 있으므로 다시 찾는다
        FindPresenters();
    }

    private void FindPresenters()
    {
        if (inventoryPresenter == null)
            inventoryPresenter = FindObjectOfType<InventoryUIPresenter>(true);
        if (questPresenter == null)
            questPresenter = FindObjectOfType<QuestUIPresenter>(true);
        if (settingsPresenter == null)
            settingsPresenter = FindObjectOfType<SettingsPresenter>(true);

        inventoryPresenter?.Hide();
        settingsPresenter?.Hide();
    }

    private void OnGameStateChanged(GameStateChange change)
    {
        switch (change.NewState)
        {
            case GameState.InventoryUI:
                HandleInventoryOpen();
                break;

            case GameState.Paused:
                HandleSettingsOpen();
                break;

            case GameState.LetterUI:
                HandleLetterOpen();
                break;

            case GameState.Gameplay:
                if (change.OldState == GameState.InventoryUI)
                    HandleInventoryClose();
                else if (change.OldState == GameState.Paused)
                    HandleSettingsClose();
                else if (change.OldState == GameState.LetterUI)
                    HandleLetterClose();
                break;
        }
    }

    private void HandleInventoryOpen()
    {
        Time.timeScale = 0f;
        questPresenter?.Hide();
        inventoryPresenter?.Show();
    }

    private void HandleInventoryClose()
    {
        Time.timeScale = 1f;
        inventoryPresenter?.Hide();
    }

    private void HandleSettingsOpen()
    {
        Time.timeScale = 0f;
        settingsPresenter?.Show();
    }

    private void HandleSettingsClose()
    {
        Time.timeScale = 1f;
        settingsPresenter?.Hide();
    }

    private void HandleLetterOpen()
    {
        Time.timeScale = 0f;
    }

    private void HandleLetterClose()
    {
        Time.timeScale = 1f;
    }
}
