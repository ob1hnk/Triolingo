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
        Debug.Log("[IntroSceneController] Start() 호출됨");

        if (dialogueRunner == null)
        {
            Debug.LogError("[IntroSceneController] DialogueRunner가 연결되지 않았습니다!");
            return;
        }

        var presenters = dialogueRunner.GetComponents<Yarn.Unity.DialoguePresenterBase>();
        Debug.Log($"[IntroSceneController] DialogueRunner 연결 확인. Presenters: {presenters.Length}개");
        foreach (var p in presenters)
            Debug.Log($"  - {p.GetType().Name} (enabled={p.enabled})");

        Debug.Log($"[IntroSceneController] IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        dialogueRunner.StartDialogue("DLG_INTRO");
        Debug.Log($"[IntroSceneController] StartDialogue 호출 후 IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
    }

    private void OnDialogueComplete()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
