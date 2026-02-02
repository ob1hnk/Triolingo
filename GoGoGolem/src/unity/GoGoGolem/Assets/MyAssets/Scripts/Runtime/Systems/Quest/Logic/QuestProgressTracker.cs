using System.Collections.Generic;
using UnityEngine;

namespace MyAssets.Runtime.Systems.Quest
{
    /// <summary>
    /// 퀘스트 진행 상태를 추적하고 관리합니다.
    /// 책임: activeQuests와 completedQuests Dictionary 관리 및 조회
    /// </summary>
    public class QuestProgressTracker
    {
        private Dictionary<string, Quest> activeQuests = new Dictionary<string, Quest>();
        private Dictionary<string, Quest> completedQuests = new Dictionary<string, Quest>();

        #region Quest 추가/제거

        /// <summary>
        /// 활성 퀘스트 추가
        /// </summary>
        public void AddActiveQuest(Quest quest)
        {
            if (quest == null)
            {
                Debug.LogError("[QuestProgressTracker] Cannot add null quest.");
                return;
            }

            if (activeQuests.ContainsKey(quest.QuestID))
            {
                Debug.LogWarning($"[QuestProgressTracker] Quest {quest.QuestID} is already active.");
                return;
            }

            activeQuests.Add(quest.QuestID, quest);
        }

        /// <summary>
        /// 활성 퀘스트 제거
        /// </summary>
        public void RemoveActiveQuest(string questID)
        {
            if (activeQuests.ContainsKey(questID))
            {
                activeQuests.Remove(questID);
            }
        }

        /// <summary>
        /// 퀘스트를 완료 목록으로 이동
        /// </summary>
        public void MoveToCompleted(Quest quest)
        {
            if (quest == null) return;

            if (activeQuests.ContainsKey(quest.QuestID))
            {
                activeQuests.Remove(quest.QuestID);
            }

            if (!completedQuests.ContainsKey(quest.QuestID))
            {
                completedQuests.Add(quest.QuestID, quest);
            }
        }

        #endregion

        #region Quest 조회

        /// <summary>
        /// 활성 퀘스트 가져오기
        /// </summary>
        public Quest GetActiveQuest(string questID)
        {
            activeQuests.TryGetValue(questID, out Quest quest);
            return quest;
        }

        /// <summary>
        /// 완료된 퀘스트 가져오기
        /// </summary>
        public Quest GetCompletedQuest(string questID)
        {
            completedQuests.TryGetValue(questID, out Quest quest);
            return quest;
        }

        /// <summary>
        /// 모든 활성 퀘스트 가져오기
        /// </summary>
        public List<Quest> GetAllActiveQuests()
        {
            return new List<Quest>(activeQuests.Values);
        }

        /// <summary>
        /// 모든 완료된 퀘스트 가져오기
        /// </summary>
        public List<Quest> GetAllCompletedQuests()
        {
            return new List<Quest>(completedQuests.Values);
        }

        /// <summary>
        /// 활성 퀘스트 Dictionary 가져오기 (세이브용)
        /// </summary>
        public Dictionary<string, Quest> GetActiveQuestsDictionary()
        {
            return activeQuests;
        }

        /// <summary>
        /// 완료된 퀘스트 Dictionary 가져오기 (세이브용)
        /// </summary>
        public Dictionary<string, Quest> GetCompletedQuestsDictionary()
        {
            return completedQuests;
        }

        #endregion

        #region 상태 확인

        /// <summary>
        /// 퀘스트가 활성화되어 있는지 확인
        /// </summary>
        public bool IsQuestActive(string questID)
        {
            return activeQuests.ContainsKey(questID);
        }

        /// <summary>
        /// 퀘스트가 완료되었는지 확인
        /// </summary>
        public bool IsQuestCompleted(string questID)
        {
            return completedQuests.ContainsKey(questID);
        }

        /// <summary>
        /// 활성 퀘스트 개수
        /// </summary>
        public int GetActiveQuestCount()
        {
            return activeQuests.Count;
        }

        /// <summary>
        /// 완료된 퀘스트 개수
        /// </summary>
        public int GetCompletedQuestCount()
        {
            return completedQuests.Count;
        }

        /// <summary>
        /// 특정 퀘스트의 진행도 (0.0 ~ 1.0)
        /// </summary>
        public float GetQuestProgress(string questID)
        {
            Quest quest = GetActiveQuest(questID);
            if (quest == null)
            {
                // 완료된 퀘스트면 1.0 반환
                if (IsQuestCompleted(questID))
                {
                    return 1.0f;
                }
                return 0f;
            }

            return quest.GetProgress();
        }

        #endregion

        #region 유틸리티

        /// <summary>
        /// 모든 진행 상태 초기화
        /// </summary>
        public void ClearAll()
        {
            activeQuests.Clear();
            completedQuests.Clear();
        }

        /// <summary>
        /// 디버그용: 현재 상태 출력
        /// </summary>
        public void PrintStatus()
        {
            Debug.Log("=== Quest Progress Status ===");
            Debug.Log($"Active Quests: {activeQuests.Count}");
            foreach (var quest in activeQuests.Values)
            {
                Debug.Log($"  [{quest.Status}] {quest.QuestID}: {quest.QuestName} ({quest.GetProgress() * 100:F1}%)");
            }

            Debug.Log($"Completed Quests: {completedQuests.Count}");
            foreach (var quest in completedQuests.Values)
            {
                Debug.Log($"  [✓] {quest.QuestID}: {quest.QuestName}");
            }
        }

        #endregion
    }
}