using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MyAssets.Runtime.Data.Quest;

namespace MyAssets.Runtime.Systems.Quest
{
    /// <summary>
    /// 런타임에서 실행되는 Objective 인스턴스
    /// </summary>
    [System.Serializable]
    public class QuestObjective
    {
        public string ObjectiveID { get; private set; }
        public string Description { get; private set; }
        public bool IsCompleted { get; private set; }

        private List<QuestPhase> phases;

        /// <summary>
        /// ObjectiveData로부터 Objective 생성
        /// </summary>
        public QuestObjective(ObjectiveData objectiveData)
        {
            ObjectiveID = objectiveData.objectiveID;
            Description = objectiveData.description;
            IsCompleted = false;

            phases = new List<QuestPhase>();
            foreach (var phaseData in objectiveData.phases)
            {
                phases.Add(new QuestPhase(phaseData));
            }
        }

        /// <summary>
        /// 특정 Phase 완료
        /// </summary>
        public void CompletePhase(string phaseID)
        {
            var phase = phases.FirstOrDefault(p => p.PhaseID == phaseID);
            if (phase == null)
            {
                Debug.LogWarning($"Phase {phaseID} not found in Objective {ObjectiveID}");
                return;
            }

            phase.Complete();

            // 모든 Phase가 완료되면 Objective 완료
            CheckCompletion();
        }

        /// <summary>
        /// 완료 상태 체크
        /// </summary>
        private void CheckCompletion()
        {
            if (phases.All(p => p.IsCompleted))
            {
                IsCompleted = true;
                Debug.Log($"[Quest Objective] Completed: {ObjectiveID} - {Description}");
            }
        }

        /// <summary>
        /// Phase 가져오기
        /// </summary>
        public QuestPhase GetPhase(string phaseID)
        {
            return phases.FirstOrDefault(p => p.PhaseID == phaseID);
        }

        /// <summary>
        /// 현재 진행중인 Phase 가져오기
        /// </summary>
        public QuestPhase GetCurrentPhase()
        {
            return phases.FirstOrDefault(p => !p.IsCompleted);
        }

        /// <summary>
        /// 모든 Phase 가져오기
        /// </summary>
        public List<QuestPhase> GetAllPhases()
        {
            return phases;
        }

        /// <summary>
        /// 진행도 계산 (0.0 ~ 1.0)
        /// </summary>
        public float GetProgress()
        {
            if (phases.Count == 0) return 0f;
            int completedCount = phases.Count(p => p.IsCompleted);
            return (float)completedCount / phases.Count;
        }

        /// <summary>
        /// Objective 정보를 문자열로 반환
        /// </summary>
        public override string ToString()
        {
            int completedPhases = phases.Count(p => p.IsCompleted);
            return $"[Objective {ObjectiveID}] {Description} ({completedPhases}/{phases.Count}) - Completed: {IsCompleted}";
        }
    }
}