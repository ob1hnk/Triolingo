using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;


/// <summary>
/// 인트로 씬에서 골렘 이름을 입력받는 UI 프레젠터.
/// Yarn Command <<request_golem_name_input>> 에 의해 호출된다.
/// </summary>
public class GolemNameInputPresenter : MonoBehaviour
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
        dialogueRunner.AddCommandHandler("request_golem_name_input", RequestGolemNameInput);
    }

    private IEnumerator RequestGolemNameInput()
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

        GameManager.Instance.SetGolemName(input);
        dialogueRunner.VariableStorage.SetValue("$golemName", input);
        _confirmed = true;
    }
}
