using UnityEngine;
using System.Collections;
using MyAssets.Runtime.Data.Quest;

namespace MyAssets.Runtime.Systems.Dialogue
{
    /// <summary>
    /// 대화 시스템 관리자
    /// DialogueData를 받아서 대화를 재생하고 UI에 표시
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private DialogueDatabase dialogueDatabase;
        // [SerializeField] private DialogueUI dialogueUI; // TODO: 나중에 추가
        
        private DialogueData currentDialogue;
        private int currentLineIndex = 0;
        private bool isPlaying = false;
        
        private void Awake()
        {
            if (dialogueDatabase != null)
            {
                dialogueDatabase.Initialize();
            }
            else
            {
                Debug.LogError("[DialogueManager] DialogueDatabase is not assigned!");
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
            
            if (dialogueDatabase == null)
            {
                Debug.LogError("[DialogueManager] DialogueDatabase is not assigned!");
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
            
            Debug.Log($"[DialogueManager] Starting dialogue: {dialogueID}");
            
            StartCoroutine(PlayDialogue());
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
            else
            {
                Debug.LogWarning($"[DialogueManager] No dialogue found for phase {phaseID}");
            }
        }
        
        /// <summary>
        /// 대화 재생 코루틴
        /// </summary>
        private IEnumerator PlayDialogue()
        {
            while (currentLineIndex < currentDialogue.dialogueLines.Count)
            {
                DialogueLine line = currentDialogue.dialogueLines[currentLineIndex];
                
                // 콘솔에 대화 출력 (임시 - UI 구현 전)
                Debug.Log($"[{line.speaker}] {line.content}");
                
                // TODO: UI에 대화 표시
                // dialogueUI?.ShowDialogueLine(line.speaker, line.content);
                
                // 선택지가 있는 경우
                if (line.isChoice && line.choiceOptions.Count > 0)
                {
                    Debug.Log($"[Choice] Options: {string.Join(", ", line.choiceOptions)}");
                    // TODO: dialogueUI?.ShowChoices(line.choiceOptions);
                    // TODO: yield return new WaitUntil(() => dialogueUI.HasSelectedChoice());
                    // TODO: int choice = dialogueUI.GetSelectedChoice();
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
            
            // TODO: dialogueUI?.HideDialogueUI();
            
            Debug.Log("[DialogueManager] Dialogue ended.");
        }
        
        /// <summary>
        /// 대화 중인지 확인
        /// </summary>
        public bool IsPlaying()
        {
            return isPlaying;
        }
        
        /// <summary>
        /// 대화 스킵
        /// </summary>
        public void SkipDialogue()
        {
            if (!isPlaying) return;
            
            StopAllCoroutines();
            EndDialogue();
        }
    }
}
