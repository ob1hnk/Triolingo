using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// <summary>
/// Bridges the Dialogue InputAction map with Yarn Spinner's LineAdvancer.
/// InputModeController enables/disables the Dialogue map based on GameState.
/// LineAdvancer should be set to InputMode.None so this handler drives it.
/// </summary>
public class DialogueInputHandler : MonoBehaviour
{
    [SerializeField] private LineAdvancer lineAdvancer;

    private GameInputActions.DialogueActions _dialogueActions;

    private void OnEnable()
    {
        if (InputModeController.Instance == null)
        {
            Debug.LogError("[DialogueInputHandler] InputModeController를 찾을 수 없습니다.");
            return;
        }

        _dialogueActions = InputModeController.Instance.GetDialogueActions();
        _dialogueActions.Continue.performed += OnContinue;
        _dialogueActions.Skip.performed += OnSkip;
    }

    private void OnDisable()
    {
        _dialogueActions.Continue.performed -= OnContinue;
        _dialogueActions.Skip.performed -= OnSkip;
    }

    private void OnContinue(InputAction.CallbackContext ctx)
    {
        if (lineAdvancer == null) return;
        lineAdvancer.RequestLineHurryUp();
    }

    private void OnSkip(InputAction.CallbackContext ctx)
    {
        if (lineAdvancer == null) return;
        lineAdvancer.RequestDialogueCancellation();
    }
}
