using MyAssets.FinalCharacterController;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputModeController : MonoBehaviour
{
    public static InputModeController Instance { get; private set; }

    private GameInputActions input;

    private bool inventoryOpen = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        input = new GameInputActions();

        // 항상 활성
        input.Global.Enable();

        // 기본 상태
        EnableGameplayInput();

        // Q 키 바인딩
        input.Global.ToggleInventory.performed += _ => ToggleInventory();
    }

    private void ToggleInventory()
    {
        if (inventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        inventoryOpen = true;

        DisableGameplayInput();
        EnableUIInput();

        Time.timeScale = 0f;

        UIManager.Instance.Inventory.Show();
    }

    public void CloseInventory()
    {
        inventoryOpen = false;

        DisableUIInput();
        EnableGameplayInput();

        Time.timeScale = 1f;

        UIManager.Instance.Inventory.Hide();
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

    public GameInputActions UIInput => input;
}
