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

        #region Quest 추가/제거/이동

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

        public void RemoveActiveQuest(string questID)
        {
            activeQuests.Remove(questID);
        }

        public void MoveToCompleted(Quest quest)
        {
            if (quest == null) return;

            activeQuests.Remove(quest.QuestID);
            
            if (!completedQuests.ContainsKey(quest.QuestID))
            {
                completedQuests.Add(quest.QuestID, quest);
            }
        }

        #endregion

        #region Quest 조회

        public Quest GetActiveQuest(string questID)
        {
            activeQuests.TryGetValue(questID, out Quest quest);
            return quest;
        }

        public Quest GetCompletedQuest(string questID)
        {
            completedQuests.TryGetValue(questID, out Quest quest);
            return quest;
        }

        public List<Quest> GetAllActiveQuests()
        {
            return new List<Quest>(activeQuests.Values);
        }

        public List<Quest> GetAllCompletedQuests()
        {
            return new List<Quest>(completedQuests.Values);
        }

        public Dictionary<string, Quest> GetActiveQuestsDictionary()
        {
            return activeQuests;
        }

        public Dictionary<string, Quest> GetCompletedQuestsDictionary()
        {
            return completedQuests;
        }

        #endregion

        #region 상태 확인

        public bool IsQuestActive(string questID)
        {
            return activeQuests.ContainsKey(questID);
        }

        public bool IsQuestCompleted(string questID)
        {
            return completedQuests.ContainsKey(questID);
        }

        public int GetActiveQuestCount()
        {
            return activeQuests.Count;
        }

        public int GetCompletedQuestCount()
        {
            return completedQuests.Count;
        }

        public float GetQuestProgress(string questID)
        {
            Quest quest = GetActiveQuest(questID);
            if (quest != null)
            {
                return quest.GetProgress();
            }

            return IsQuestCompleted(questID) ? 1.0f : 0f;
        }

        #endregion

        #region 유틸리티

        public void ClearAll()
        {
            activeQuests.Clear();
            completedQuests.Clear();
        }

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