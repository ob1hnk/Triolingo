using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;


/// <summary>
/// 인트로 씬에서 플레이어 이름을 입력받는 UI 프레젠터.
/// Yarn Command <<request_name_input>> 에 의해 호출된다.
/// </summary>
public class NameInputPresenter : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton;
    [SerializeField] private DialogueRunner dialogueRunner;

    private bool _confirmed;

    private void Awake()
    {
        panel.SetActive(false);
        confirmButton.onClick.AddListener(OnConfirm);
        dialogueRunner.AddCommandHandler("request_name_input", RequestNameInput);
    }

    private IEnumerator RequestNameInput()
    {
        _confirmed = false;
        nameInputField.text = string.Empty;
        panel.SetActive(true);
        nameInputField.ActivateInputField();

        yield return new WaitUntil(() => _confirmed);

        panel.SetActive(false);
    }

    private void OnConfirm()
    {
        string input = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        GameManager.Instance.SetPlayerName(input);
        dialogueRunner.VariableStorage.SetValue("$playerName", input);
        _confirmed = true;
    }
}
