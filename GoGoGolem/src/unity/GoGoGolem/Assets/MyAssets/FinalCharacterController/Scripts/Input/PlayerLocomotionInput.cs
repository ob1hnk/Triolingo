using UnityEngine;
using UnityEngine.InputSystem;

namespace MyAssets.FinalCharacterController
{
    [DefaultExecutionOrder(-2)]
    public class PlayerLocomotionInput 
        : MonoBehaviour, GameInputActions.IPlayerMovementActions
    {
        [SerializeField] private bool holdToSprint = true;

        public Vector2 MovementInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool SprintToggledOn { get; private set; }

        private GameInputActions input;

        private void OnEnable()
        {
            input = new GameInputActions();

            input.PlayerMovement.SetCallbacks(this);
            input.PlayerMovement.Enable();
        }

        private void OnDisable()
        {
            if (input == null) return;

            input.PlayerMovement.RemoveCallbacks(this);
            input.PlayerMovement.Disable();
        }

        // ===== PlayerMovement Actions =====

        public void OnMovement(InputAction.CallbackContext context)
        {
            MovementInput = context.ReadValue<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        public void OnToggleSprint(InputAction.CallbackContext context)
        {
            if (!context.performed && !context.canceled) return;

            if (context.performed)
            {
                SprintToggledOn = holdToSprint || !SprintToggledOn;
            }
            else if (context.canceled)
            {
                SprintToggledOn = !holdToSprint && SprintToggledOn;
            }
        }
    }
}
