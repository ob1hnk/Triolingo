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
    
    public GameState CurrentState { get; private set; } = GameState.Gameplay;
    
    public event System.Action<GameState, GameState> OnStateChanged;
    
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
        
        OnStateChanged?.Invoke(oldState, newState);
    }
    public bool IsInState(GameState state)
    {
        return CurrentState == state;
    }
}