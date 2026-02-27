using UnityEngine;

public class InputModeController : MonoBehaviour
{
    public static InputModeController Instance { get; private set; }

    [Header("Event Channels")]
    [SerializeField] private GameStateChangeEvent onGameStateChangedEvent;

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

    private void InitializeInput()
    {
        input = new GameInputActions();
        input.Global.Enable();

        // Q 키는 인벤토리 토글만 담당
        input.Global.ToggleInventory.performed += _ => HandleInventoryToggle();

        // 초기 상태에 맞는 입력 활성화
        EnableGameplayInput();
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

    private void OnGameStateChanged(GameStateChange change)
    {
        // 이전 상태의 입력 비활성화
        DisableInputForState(change.OldState);

        // 새 상태의 입력 활성화
        EnableInputForState(change.NewState);
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

            case GameState.Dialogue:
                // 대화 중에는 추가 입력 없음 (Space/Esc는 GolemDialogueSceneController가 직접 처리)
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

            case GameState.Dialogue:
                DisableGameplayInput();
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
        if (input == null) return;

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
