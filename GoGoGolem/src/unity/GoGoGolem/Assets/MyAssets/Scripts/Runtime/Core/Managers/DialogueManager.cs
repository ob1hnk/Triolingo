using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MyAssets.Runtime.Data.Quest;

namespace MyAssets.Runtime.Systems.Dialogue
{
    /// <summary>
    /// 대화 시스템 관리자
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private DialogueDatabase dialogueDatabase;
        // [SerializeField] private DialogueUI dialogueUI; // 대화 UI (별도 생성 필요)
        
        private DialogueData currentDialogue;
        private int currentLineIndex = 0;
        private bool isPlaying = false;
        
        private void Awake()
        {
            if (dialogueDatabase != null)
            {
                dialogueDatabase.Initialize();
            }
        }
        
        /// <summary>
        /// 대화 시작 (Dialogue ID로)
        /// </summary>
        public void StartDialogue(string dialogueID)
        {
            if (isPlaying)
            {
                Debug.LogWarning("[DialogueManager] Dialogue is already playing.");
                return;
            }
            
            currentDialogue = dialogueDatabase.GetDialogueData(dialogueID);
            if (currentDialogue == null)
            {
                Debug.LogError($"[DialogueManager] Dialogue {dialogueID} not found.");
                return;
            }
            
            currentLineIndex = 0;
            isPlaying = true;
            
            // StartCoroutine(PlayDialogue());
        }
        
        /// <summary>
        /// Phase ID로 대화 시작 (첫 번째 Dialogue 재생)
        /// </summary>
        public void StartDialogueByPhase(string phaseID)
        {
            var dialogues = dialogueDatabase.GetDialoguesByPhase(phaseID);
            if (dialogues.Count > 0)
            {
                StartDialogue(dialogues[0].dialogueID);
            }
        }
        

        /*
        /// <summary>
        /// 대화 재생 코루틴
        /// </summary>
        private IEnumerator PlayDialogue()
        {
            while (currentLineIndex < currentDialogue.dialogueLines.Count)
            {
                DialogueLine line = currentDialogue.dialogueLines[currentLineIndex];
                
                // UI에 대화 표시
                dialogueUI?.ShowDialogueLine(line.speaker, line.content);
                
                // 선택지가 있는 경우
                if (line.isChoice && line.choiceOptions.Count > 0)
                {
                    dialogueUI?.ShowChoices(line.choiceOptions);
                    yield return new WaitUntil(() => dialogueUI.HasSelectedChoice());
                    int choice = dialogueUI.GetSelectedChoice();
                    Debug.Log($"[DialogueManager] Player chose option {choice}");
                }
                
                // 다음 라인으로 (스페이스바 또는 마우스 클릭 대기)
                yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0));
                
                currentLineIndex++;
            }
            
            // 대화 종료
            EndDialogue();
        }
        
        /// <summary>
        /// 대화 종료
        /// </summary>
        private void EndDialogue()
        {
            isPlaying = false;
            currentDialogue = null;
            currentLineIndex = 0;
            
            dialogueUI?.HideDialogueUI();
            
            Debug.Log("[DialogueManager] Dialogue ended.");
        }
        
        /// <summary>
        /// 대화 중인지 확인
        /// </summary>
        public bool IsPlaying()
        {
            return isPlaying;
        }
        */
        }
}
