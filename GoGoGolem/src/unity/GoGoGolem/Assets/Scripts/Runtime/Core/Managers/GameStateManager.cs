using UnityEngine;

public enum GameState
{
    Gameplay,
    InventoryUI,
    Paused,
    Dialogue
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Event Channels")]
    [SerializeField] private GameStateChangeEvent onGameStateChangedEvent;

    public GameState CurrentState { get; private set; } = GameState.Gameplay;

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

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        GameState oldState = CurrentState;
        CurrentState = newState;

        onGameStateChangedEvent?.Raise(new GameStateChange(oldState, newState));
    }

    public bool IsInState(GameState state)
    {
        return CurrentState == state;
    }
}
