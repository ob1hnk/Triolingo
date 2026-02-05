using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MyAssets.Runtime.Data.Quest
{
    [CreateAssetMenu(fileName = "DialogueDatabase", menuName = "Quest System/Dialogue Database")]
    public class DialogueDatabase : ScriptableObject
    {
        [Header("All Dialogues")]
        [SerializeField] private List<DialogueData> allDialogues = new List<DialogueData>();
        
        private Dictionary<string, DialogueData> dialogueDictionary;
        private Dictionary<string, List<DialogueData>> phaseDialogueDictionary;
        private bool isInitialized = false;
        
        /// <summary>
        /// 데이터베이스 초기화
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;
            
            dialogueDictionary = new Dictionary<string, DialogueData>();
            phaseDialogueDictionary = new Dictionary<string, List<DialogueData>>();
            
            foreach (var dialogue in allDialogues)
            {
                if (dialogue != null && dialogue.Validate())
                {
                    // Dialogue ID로 검색
                    if (!dialogueDictionary.ContainsKey(dialogue.dialogueID))
                    {
                        dialogueDictionary.Add(dialogue.dialogueID, dialogue);
                    }
                    
                    // Phase ID로 검색 (한 Phase에 여러 Dialogue가 있을 수 있음)
                    if (!string.IsNullOrEmpty(dialogue.phaseID))
                    {
                        if (!phaseDialogueDictionary.ContainsKey(dialogue.phaseID))
                        {
                            phaseDialogueDictionary[dialogue.phaseID] = new List<DialogueData>();
                        }
                        phaseDialogueDictionary[dialogue.phaseID].Add(dialogue);
                    }
                }
            }
            
            isInitialized = true;
            Debug.Log($"[DialogueDatabase] Initialized with {dialogueDictionary.Count} dialogues.");
        }
        
        /// <summary>
        /// DialogueData 가져오기 (Dialogue ID로)
        /// </summary>
        public DialogueData GetDialogueData(string dialogueID)
        {
            if (!isInitialized)
            {
                Initialize();
            }
            
            if (dialogueDictionary.TryGetValue(dialogueID, out DialogueData data))
            {
                return data;
            }
            
            Debug.LogWarning($"[DialogueDatabase] Dialogue {dialogueID} not found.");
            return null;
        }
        
        /// <summary>
        /// Phase에 속한 모든 Dialogue 가져오기
        /// </summary>
        public List<DialogueData> GetDialoguesByPhase(string phaseID)
        {
            if (!isInitialized)
            {
                Initialize();
            }
            
            if (phaseDialogueDictionary.TryGetValue(phaseID, out List<DialogueData> dialogues))
            {
                return dialogues;
            }
            
            return new List<DialogueData>();
        }
    }
}