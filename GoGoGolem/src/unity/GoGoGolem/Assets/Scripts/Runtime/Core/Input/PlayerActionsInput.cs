using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-2)]
public class PlayerActionsInput : MonoBehaviour, GameInputActions.IPlayerActionsActions
{
    #region Class Variables
    public bool GatherPressed { get; private set; }

    private PlayerLocomotionInput _playerLocomotionInput;
    #endregion

    #region Startup
    private void Awake()
    {
        _playerLocomotionInput = GetComponent<PlayerLocomotionInput>();
    }
    
    #endregion

    private void Update()
    {
        if (_playerLocomotionInput.MovementInput != Vector2.zero)
        {
            GatherPressed = false;
        }
    }

    public void SetGatherPressedFalse()
    {
        GatherPressed = false;
    }


    public void OnGather(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        GatherPressed = true;
    }

    public void OnInteract(InputAction.CallbackContext context) { }
}
