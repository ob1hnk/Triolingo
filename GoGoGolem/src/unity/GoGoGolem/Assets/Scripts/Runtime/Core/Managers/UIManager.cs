using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private InventoryUIPresenter inventoryPresenter;
    [SerializeField] private SettingsPresenter settingsPresenter;

    [Header("Event Channels")]
    [SerializeField] private GameStateChangeEvent onGameStateChangedEvent;

    public InventoryUIPresenter Inventory => inventoryPresenter;

    private void Start()
    {
        if (inventoryPresenter == null)
            Debug.LogError("UIManager: InventoryUIPresenter가 할당되지 않았습니다.");

        if (settingsPresenter == null)
            Debug.LogError("UIManager: SettingsPresenter가 할당되지 않았습니다.");

        inventoryPresenter?.Hide();
        settingsPresenter?.Hide();
    }

    private void OnEnable()
    {
        if (onGameStateChangedEvent != null)
            onGameStateChangedEvent.Register(OnGameStateChanged);
    }

    private void OnDisable()
    {
        if (onGameStateChangedEvent != null)
            onGameStateChangedEvent.Unregister(OnGameStateChanged);
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

            case GameState.Gameplay:
                if (change.OldState == GameState.InventoryUI)
                    HandleInventoryClose();
                else if (change.OldState == GameState.Paused)
                    HandleSettingsClose();
                break;
        }
    }

    private void HandleInventoryOpen()
    {
        Time.timeScale = 0f;
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
}
