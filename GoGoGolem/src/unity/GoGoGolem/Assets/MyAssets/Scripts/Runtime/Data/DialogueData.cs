using System.Collections.Generic;
using UnityEngine;

namespace MyAssets.Runtime.Data.Quest
{
    /// <summary>
    /// 대화 데이터 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "New Dialogue", menuName = "Quest System/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [Header("Dialogue 기본 정보")]
        [Tooltip("대화 ID (예: DLG-001)")]
        public string dialogueID;
        
        [Tooltip("연결된 Phase ID (예: MQ-01-P01)")]
        public string phaseID;
        
        [Header("대화 라인들")]
        [Tooltip("대화 라인 목록 (순서대로 재생)")]
        public List<DialogueLine> dialogueLines = new List<DialogueLine>();
        
        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrEmpty(dialogueID))
            {
                Debug.LogError($"Dialogue {name}: dialogueID가 비어있습니다.");
                return false;
            }
            
            if (dialogueLines == null || dialogueLines.Count == 0)
            {
                Debug.LogError($"Dialogue {dialogueID}: dialogueLines가 비어있습니다.");
                return false;
            }
            
            return true;
        }
    }
    
    /// <summary>
    /// 대화 라인 (개별 발화)
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("순서 (1, 2, 3...)")]
        public int order;
        
        [Tooltip("발화자 (주인공, 골렘, 할아버지 등)")]
        public string speaker;
        
        [Tooltip("대화 내용")]
        [TextArea(2, 5)]
        public string content;
        
        [Header("조건 및 비고")]
        [Tooltip("조건 (예: 숲 초입 영역 도달)")]
        public string condition;
        
        [Tooltip("비고 (내적독백, 행동, 선택A 등)")]
        public string note;
        
        [Header("선택지 (선택형 대화인 경우)")]
        [Tooltip("선택지 여부")]
        public bool isChoice = false;
        
        [Tooltip("선택지 옵션들")]
        public List<string> choiceOptions = new List<string>();
    }
}
