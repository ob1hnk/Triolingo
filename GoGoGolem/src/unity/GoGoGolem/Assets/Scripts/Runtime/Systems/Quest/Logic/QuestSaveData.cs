using System.Collections.Generic;

namespace MyAssets.Runtime.Systems.Quest
{
    /// <summary>
    /// 퀘스트 진행 상태를 저장하기 위한 데이터 구조
    /// JSON으로 직렬화되어 파일에 저장됩니다.
    /// </summary>
    [System.Serializable]
    public class QuestSaveData
    {
        /// <summary>
        /// 진행 중인 퀘스트 목록
        /// </summary>
        public List<QuestProgressData> activeQuests = new List<QuestProgressData>();

        /// <summary>
        /// 완료된 퀘스트 ID 목록
        /// </summary>
        public List<string> completedQuestIDs = new List<string>();

        /// <summary>
        /// 마지막 저장 시간
        /// </summary>
        public string lastSaveTime;

        /// <summary>
        /// 저장 파일 버전 (나중에 데이터 마이그레이션용)
        /// </summary>
        public int saveVersion = 1;
    }

    /// <summary>
    /// 개별 퀘스트의 진행 상태
    /// </summary>
    [System.Serializable]
    public class QuestProgressData
    {
        /// <summary>
        /// 퀘스트 ID
        /// </summary>
        public string questID;

        /// <summary>
        /// 퀘스트 상태 (InProgress, Completed 등)
        /// </summary>
        public string status;

        /// <summary>
        /// 완료된 Objective ID 목록
        /// </summary>
        public List<string> completedObjectiveIDs = new List<string>();

        /// <summary>
        /// 완료된 Phase ID 목록
        /// </summary>
        public List<string> completedPhaseIDs = new List<string>();

        /// <summary>
        /// 퀘스트 시작 시간
        /// </summary>
        public string startTime;
    }
}