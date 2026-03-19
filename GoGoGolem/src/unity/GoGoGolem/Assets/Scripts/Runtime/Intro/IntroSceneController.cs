using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

/// <summary>
/// 인트로 씬 진행을 제어한다.
/// 씬 시작 시 INTRO 대화를 자동 실행하고, 완료 후 다음 씬으로 전환한다.
/// </summary>
public class IntroSceneController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string nextSceneName = "World";

    private void Start()
    {
        if (dialogueRunner == null)
        {
            Debug.LogError("[IntroSceneController] DialogueRunner가 연결되지 않았습니다!");
            return;
        }

        Debug.Log("[IntroSceneController] Start. DialogueRunner 연결됨. DLG_INTRO 실행 시도.");
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        dialogueRunner.onDialogueStart.AddListener(() => Debug.Log("[IntroSceneController] onDialogueStart 발생. 대화 실제 시작됨."));
        dialogueRunner.StartDialogue("DLG_INTRO");
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    private void OnDialogueComplete()
    {
        Debug.Log("[IntroSceneController] 대화 완료. 씬 전환: " + nextSceneName);
        SceneManager.LoadScene(nextSceneName);
    }
}
