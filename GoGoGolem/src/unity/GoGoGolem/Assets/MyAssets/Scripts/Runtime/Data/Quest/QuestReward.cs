using System.Collections.Generic;
using UnityEngine;

namespace MyAssets.Runtime.Data.Quest
{
    /// <summary>
    /// 퀘스트 보상 데이터
    /// </summary>
    [System.Serializable]
    public class QuestReward
    {
        [Header("보상 정보")]
        [Tooltip("보상 ID (예: RWD-001)")]
        public string rewardID;

        [Header("아이템 보상")]
        [Tooltip("보상 아이템 ID 목록")]
        public List<string> itemIDs = new List<string>();

        [Header("기타 보상")]
        [Tooltip("골드")]
        public int gold = 0;

        [Tooltip("경험치")]
        public int experience = 0;

        /// <summary>
        /// 보상이 비어있는지 확인
        /// </summary>
        public bool IsEmpty()
        {
            return (itemIDs == null || itemIDs.Count == 0) && gold == 0 && experience == 0;
        }

        /// <summary>
        /// 보상 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            string result = $"[Reward {rewardID}]";
            
            if (itemIDs != null && itemIDs.Count > 0)
            {
                result += $"\n  Items: {string.Join(", ", itemIDs)}";
            }
            
            if (gold > 0)
            {
                result += $"\n  Gold: {gold}";
            }
            
            if (experience > 0)
            {
                result += $"\n  Exp: {experience}";
            }

            return result;
        }
    }
}