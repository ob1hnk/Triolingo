using System.Collections.Generic;
using UnityEngine;

namespace MyAssets.Runtime.Data.Quest
{
    /// <summary>
    /// 퀘스트 데이터 ScriptableObject
    /// Inspector에서 퀘스트를 생성하고 편집할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "New Quest", menuName = "Quest System/Quest Data")]
    public class QuestData : ScriptableObject
    {
        [Header("Quest 기본 정보")]
        [Tooltip("퀘스트 ID (예: MQ-01)")]
        public string questID;

        [Tooltip("퀘스트 이름")]
        public string questName;

        [Tooltip("퀘스트 요약")]
        [TextArea(3, 5)]
        public string summary;

        [Tooltip("퀘스트 타입")]
        public QuestType questType = QuestType.MainQuest;

        [Header("Quest 목표들")]
        [Tooltip("퀘스트의 Objective 목록")]
        public List<ObjectiveData> objectives = new List<ObjectiveData>();

        [Header("Quest 보상")]
        [Tooltip("퀘스트 완료 시 보상")]
        public QuestReward reward;

        /// <summary>
        /// 데이터 유효성 검사
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrEmpty(questID))
            {
                Debug.LogError($"Quest {name}: questID가 비어있습니다.");
                return false;
            }

            if (objectives == null || objectives.Count == 0)
            {
                Debug.LogError($"Quest {questID}: objectives가 비어있습니다.");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Objective 데이터
    /// </summary>
    [System.Serializable]
    public class ObjectiveData
    {
        [Tooltip("Objective ID (예: MQ-01-OBJ-01)")]
        public string objectiveID;

        [Tooltip("목표 설명 (플레이어가 보는 텍스트)")]
        [TextArea(2, 3)]
        public string description;

        [Tooltip("이 Objective를 구성하는 Phase 목록")]
        public List<PhaseData> phases = new List<PhaseData>();
    }

    /// <summary>
    /// Phase 데이터
    /// </summary>
    [System.Serializable]
    public class PhaseData
    {
        [Tooltip("Phase ID (예: MQ-01-P01)")]
        public string phaseID;

        [Tooltip("Phase 타입")]
        public PhaseType phaseType;

        [Tooltip("Content ID (예: DLG-001, INT-001)")]
        public string contentID;

        [Tooltip("이 Phase의 설명 (개발용)")]
        [TextArea(2, 3)]
        public string description;

        [Header("조건 및 액션 (선택사항)")]
        [Tooltip("완료 조건 ID 목록 (비어있으면 무조건 완료)")]
        public List<string> conditionIDs = new List<string>();

        [Tooltip("실행할 액션 ID 목록")]
        public List<string> actionIDs = new List<string>();
    }
}