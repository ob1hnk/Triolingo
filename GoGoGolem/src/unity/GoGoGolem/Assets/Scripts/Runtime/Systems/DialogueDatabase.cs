using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 대화 데이터베이스
/// 모든 DialogueData를 관리하고 검색 기능 제공
/// </summary>
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
        if (isInitialized)
        {
            Debug.Log("[DialogueDatabase] Already initialized.");
            return;
        }
        
        dialogueDictionary = new Dictionary<string, DialogueData>();
        phaseDialogueDictionary = new Dictionary<string, List<DialogueData>>();
        
        foreach (var dialogue in allDialogues)
        {
            if (dialogue != null && dialogue.Validate())
            {
                // Dialogue ID로 검색용 Dictionary
                if (!dialogueDictionary.ContainsKey(dialogue.dialogueID))
                {
                    dialogueDictionary.Add(dialogue.dialogueID, dialogue);
                }
                else
                {
                    Debug.LogWarning($"[DialogueDatabase] Duplicate dialogue ID: {dialogue.dialogueID}");
                }
                
                // Phase ID로 검색용 Dictionary (한 Phase에 여러 Dialogue가 있을 수 있음)
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
            Debug.LogWarning("[DialogueDatabase] Not initialized! Initializing now...");
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
            Debug.LogWarning("[DialogueDatabase] Not initialized! Initializing now...");
            Initialize();
        }
        
        if (phaseDialogueDictionary.TryGetValue(phaseID, out List<DialogueData> dialogues))
        {
            return dialogues;
        }
        
        return new List<DialogueData>();
    }
    
    /// <summary>
    /// 모든 Dialogue 개수 반환
    /// </summary>
    public int GetDialogueCount()
    {
        return allDialogues.Count;
    }
    
    /// <summary>
    /// 데이터베이스 유효성 검사
    /// </summary>
    [ContextMenu("Validate All Dialogues")]
    public void ValidateAll()
    {
        int validCount = 0;
        int invalidCount = 0;
        
        foreach (var dialogue in allDialogues)
        {
            if (dialogue != null && dialogue.Validate())
            {
                validCount++;
            }
            else
            {
                invalidCount++;
            }
        }
        
        Debug.Log($"[DialogueDatabase] Validation complete: {validCount} valid, {invalidCount} invalid");
    }
    
    /// <summary>
    /// 데이터베이스 내용 출력 (디버그용)
    /// </summary>
    [ContextMenu("Print Database Content")]
    public void PrintDatabaseContent()
    {
        if (!isInitialized)
        {
            Initialize();
        }
        
        Debug.Log($"=== Dialogue Database ===");
        Debug.Log($"Total Dialogues: {allDialogues.Count}");
        
        foreach (var dialogue in allDialogues)
        {
            if (dialogue != null)
            {
                Debug.Log($"- {dialogue.dialogueID} (Phase: {dialogue.phaseID}) - {dialogue.dialogueLines.Count} lines");
            }
        }
    }
}
