using System.Collections.Generic;
using UnityEngine;
using MyAssets.Runtime.Data.Quest;

namespace MyAssets.Runtime.Systems.Quest
{
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Quest System/Quest Database")]
    public class QuestDatabase : ScriptableObject  // ← 중요!
    {
        [Header("Quest 데이터 목록")]
        [SerializeField] private List<QuestData> questDataList = new List<QuestData>();

        private Dictionary<string, QuestData> questDataDictionary;

        public void Initialize()
        {
            questDataDictionary = new Dictionary<string, QuestData>();

            foreach (var questData in questDataList)
            {
                if (questData == null)
                {
                    Debug.LogWarning("QuestDatabase: null QuestData found in list.");
                    continue;
                }

                if (!questData.Validate())
                {
                    Debug.LogWarning($"QuestDatabase: Invalid QuestData {questData.name}");
                    continue;
                }

                if (questDataDictionary.ContainsKey(questData.questID))
                {
                    Debug.LogWarning($"QuestDatabase: Duplicate questID {questData.questID}");
                    continue;
                }

                questDataDictionary.Add(questData.questID, questData);
            }

            Debug.Log($"[QuestDatabase] Initialized with {questDataDictionary.Count} quests.");
        }

        public QuestData GetQuestData(string questID)
        {
            if (questDataDictionary == null)
            {
                Debug.LogError("QuestDatabase not initialized. Call Initialize() first.");
                return null;
            }

            if (questDataDictionary.TryGetValue(questID, out QuestData questData))
            {
                return questData;
            }

            Debug.LogWarning($"QuestDatabase: QuestData {questID} not found.");
            return null;
        }

        public List<string> GetAllQuestIDs()
        {
            if (questDataDictionary == null)
            {
                Debug.LogError("QuestDatabase not initialized.");
                return new List<string>();
            }

            return new List<string>(questDataDictionary.Keys);
        }

        public bool HasQuest(string questID)
        {
            if (questDataDictionary == null)
            {
                Debug.LogError("QuestDatabase not initialized.");
                return false;
            }

            return questDataDictionary.ContainsKey(questID);
        }

#if UNITY_EDITOR
        [ContextMenu("Auto Load Quest Data from Resources")]
        private void AutoLoadFromResources()
        {
            questDataList.Clear();
            QuestData[] foundQuests = Resources.LoadAll<QuestData>("QuestData/MainQuests");
            
            foreach (var quest in foundQuests)
            {
                questDataList.Add(quest);
            }

            Debug.Log($"Loaded {questDataList.Count} quests from Resources.");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}