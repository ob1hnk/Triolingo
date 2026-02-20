using UnityEngine;

public class DialogueTest : MonoBehaviour
{
    private DialogueManager dialogueManager;
    
    private void Start()
    {
        dialogueManager = Object.FindFirstObjectByType<DialogueManager>();
    }
    
    private void Update()
    {
        // D키: DLG-001 대화 시작 (테스트용)
        if (Input.GetKeyDown(KeyCode.D))
        {
            dialogueManager.StartDialogue("DLG-001");
        }
    }
}