using UnityEngine;

public class InputModeController : MonoBehaviour
{
    public static InputModeController Instance { get; private set; }

    private GameInputActions input;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeInput();
        SubscribeToStateChanges();
    }
        
        private void InitializeInput()
    {
        input = new GameInputActions();
        input.Global.Enable();
        
        // Q 키는 인벤토리 토글만 담당
        input.Global.ToggleInventory.performed += _ => HandleInventoryToggle();
        
        // 초기 상태에 맞는 입력 활성화
        EnableGameplayInput();
    }

    private void SubscribeToStateChanges()
    {
        GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
    }

    private void HandleInventoryToggle()
    {
        var currentState = GameStateManager.Instance.CurrentState;
        
        if (currentState == GameState.Gameplay)
        {
            GameStateManager.Instance.ChangeState(GameState.InventoryUI);
        }
        else if (currentState == GameState.InventoryUI)
        {
            GameStateManager.Instance.ChangeState(GameState.Gameplay);
        }
    }

    private void OnGameStateChanged(GameState oldState, GameState newState)
    {
        // 이전 상태의 입력 비활성화
        DisableInputForState(oldState);
        
        // 새 상태의 입력 활성화
        EnableInputForState(newState);
    }

    private void EnableInputForState(GameState state)
    {
        switch (state)
        {
            case GameState.Gameplay:
                EnableGameplayInput();
                break;
                
            case GameState.InventoryUI:
                EnableUIInput();
                break;
                
            case GameState.Paused:
                // 일시정지 시에는 특정 입력만 활성화
                break;
        }
    }

    private void DisableInputForState(GameState state)
    {
        switch (state)
        {
            case GameState.Gameplay:
                DisableGameplayInput();
                break;
                
            case GameState.InventoryUI:
                DisableUIInput();
                break;
        }
    }

    private void EnableGameplayInput()
    {
        input.PlayerMovement.Enable();
        input.PlayerActions.Enable();
        input.CameraControl.Enable();
    }

    private void DisableGameplayInput()
    {
        input.PlayerMovement.Disable();
        input.PlayerActions.Disable();
        input.CameraControl.Disable();
    }

    private void EnableUIInput()
    {
        input.UI.Enable();
    }

    private void DisableUIInput()
    {
        input.UI.Disable();
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }
        
        if (input == null) return;

        input.Global.ToggleInventory.performed -= _ => HandleInventoryToggle();
        
        input.Global.Disable();
        input.PlayerMovement.Disable();
        input.PlayerActions.Disable();
        input.CameraControl.Disable();
        input.UI.Disable();

        input.Dispose();
    }

    // Presenter가 접근할 수 있도록 제공
    public GameInputActions.UIActions GetUIActions() => input.UI;
}
