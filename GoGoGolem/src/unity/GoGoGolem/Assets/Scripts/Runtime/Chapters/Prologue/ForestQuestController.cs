using System;
using UnityEngine;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬의 퀘스트 흐름을 한 곳에서 문서화하고 라우팅하는 컨트롤러.
    ///
    /// 역할:
    ///   - Forest 씬에서 진행되어야 할 퀘스트 phase의 순서 전체를 Inspector에 선언한다.
    ///   - 외부 소스가 CompleteByPhaseID() / CompleteAt()을 호출하면
    ///     enforceOrder gate 체크 후 QuestManager에 CompletePhase 이벤트를 발행한다.
    ///
    /// 설계 원칙:
    ///   - 소스 컴포넌트(ForestEventController, LeafEventController, TriggerZone 등)에 의존하지 않는다.
    ///     소스 쪽에서 이 컨트롤러의 public 메소드를 직접 호출한다.
    ///   - 각 PhaseEntry에 questID를 함께 명시하여 MQ-01 / MQ-02 혼용 가능.
    ///
    /// ──────────────────────────────────────────────────────
    /// MQ-01 Phase 완료 경로 (Forest 씬 담당)
    /// ──────────────────────────────────────────────────────
    ///
    /// 퀘스트 시작: 씬 로드 시 자동 시작 (또는 Yarn <<start_quest MQ-01>>)
    ///
    /// ★ = 이 컨트롤러(ForestQuestController)를 경유
    ///
    ///   P01  시작 (혼잣말)         TriggerZone 0 진입 → DLG_001.yarn → <<start_quest MQ-01>>, <<complete_phase>> → QuestYarnCommands
    ///                              [조건] MQ-01-P01 미완료
    ///
    ///   P02  장애물 조우           TriggerZone 1 진입 → DLG_002.yarn → <<complete_phase>> → QuestYarnCommands
    ///
    ///   P03  플레이어 선택         TriggerZone 1 진입 → DLG_002.yarn → <<complete_phase>> → QuestYarnCommands
    ///
    ///   *P04  상호작용 연출         Timeline 완료 (EnterCompleteState)
    ///                              → CompleteByPhaseID("MQ-01-P04")
    ///
    ///   *P05  돌발 이벤트           LeafEventController Leaf 타임라인 완료
    ///                              → CompleteByPhaseID("MQ-01-P05")
    ///
    ///   *P06  완료 (나뭇잎 획득)    LeafEventController OnItemReceived
    ///                              → CompleteByPhaseID("MQ-01-P06")
    ///
    /// ──────────────────────────────────────────────────────
    /// MQ-02 Phase 완료 경로 (Forest 씬 담당)
    /// ──────────────────────────────────────────────────────
    ///
    /// 퀘스트 시작: DLG_006.yarn → <<start_quest MQ-02>> → QuestYarnCommands → QuestManager
    ///
    ///   *P01  강가 도착             TriggerZone.UnityEvent → CompleteByPhaseID("MQ-02-P01")
    ///
    ///   P02  상황 파악              DLG_007.yarn → <<complete_phase>> → QuestYarnCommands
    ///
    ///   *P03  아이템 사용 (ITEM-001) ItemUsableZone step[0].onPlaced → CompleteByPhaseID("MQ-02-P03")
    ///
    ///   P04  제스처 인식            GestureSceneController.NotifyEntryPhaseComplete()
    ///   P05  전달 실패              GestureSceneController.NotifyQuestPhaseComplete()
    ///   P06  골렘 대화              GolemDialogueSceneController.NotifyQuestPhaseComplete()
    ///
    ///   *P07  아이템 사용 (ITEM-002) ItemUsableZone step[1].onPlaced → CompleteByPhaseID("MQ-02-P07")
    ///
    ///   P08  제스처 인식            GestureSceneController.NotifyEntryPhaseComplete()
    ///   P09  전달 성공              GestureSceneController.NotifyQuestPhaseComplete()
    ///   P10  완료                   DLG_012.yarn → <<complete_phase>> → QuestYarnCommands
    /// </summary>
    public class ForestQuestController : MonoBehaviour
    {
        [Serializable]
        public struct PhaseEntry
        {
            [Tooltip("사람이 읽기 위한 라벨 (예: 'MQ-01 P01 시작')")]
            public string label;

            [Tooltip("Quest ID (예: MQ-01)")]
            public string questID;

            [Tooltip("Objective ID (예: MQ-01-OBJ-01)")]
            public string objectiveID;

            [Tooltip("Phase ID (예: MQ-01-P01)")]
            public string phaseID;

            [Tooltip("이 씬이 이 phase를 책임지는가? false면 선언만 하고 호출은 무시 (문서 용도)")]
            public bool handledByThisScene;
        }

        [Header("Phase Sequence (MQ-01 → MQ-02 순서대로 입력)")]
        [Tooltip("Forest 씬 전체 퀘스트 phase를 순서대로 나열한다.\n" +
                 "handledByThisScene이 true인 phase만 이 컨트롤러가 발행한다.")]
        [SerializeField] private PhaseEntry[] phases;

        [Header("Event Channel")]
        [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

        [Header("Options")]
        [Tooltip("true면 이전 phase들이 완료되지 않은 상태에서 호출하면 경고 후 무시한다.")]
        [SerializeField] private bool enforceOrder = true;

        [SerializeField] private bool verboseLogging = true;

        private void Start()
        {
            if (requestCompletePhaseEvent == null)
                Debug.LogError("[ForestQuestController] requestCompletePhaseEvent가 연결되지 않았습니다.");
            if (phases == null || phases.Length == 0)
                Debug.LogWarning("[ForestQuestController] phases가 비어있습니다.");

            if (verboseLogging) LogSequence();
        }

        // =============================================
        // Public API
        // =============================================

        /// <summary>phaseID로 해당 phase를 완료 요청.</summary>
        public void CompleteByPhaseID(string phaseID)
        {
            int index = FindIndexByPhaseID(phaseID);
            if (index < 0)
            {
                Debug.LogWarning($"[ForestQuestController] phaseID '{phaseID}'가 시퀀스에 없습니다.");
                return;
            }
            CompleteAt(index);
        }

        /// <summary>시퀀스 index로 완료 요청.</summary>
        public void CompleteAt(int index)
        {
            if (phases == null || index < 0 || index >= phases.Length)
            {
                Debug.LogWarning($"[ForestQuestController] 잘못된 phase index: {index}");
                return;
            }

            var entry = phases[index];

            if (!entry.handledByThisScene)
            {
                Debug.LogWarning($"[ForestQuestController] {Format(entry)}는 이 씬 담당이 아닙니다. 무시.");
                return;
            }

            if (Managers.Quest == null)
            {
                Debug.LogError("[ForestQuestController] Managers.Quest가 없습니다.");
                return;
            }

            if (!Managers.Quest.IsQuestActive(entry.questID))
            {
                Debug.Log($"[ForestQuestController] {entry.questID}가 비활성. {Format(entry)} 무시.");
                return;
            }

            if (IsAlreadyCompleted(entry))
            {
                if (verboseLogging)
                    Debug.Log($"[ForestQuestController] {Format(entry)}는 이미 완료됨. 스킵.");
                return;
            }

            if (enforceOrder && !ArePrereqsSatisfied(index))
            {
                Debug.LogWarning($"[ForestQuestController] {Format(entry)}의 선행 phase가 아직 미완. 호출 무시.");
                return;
            }

            if (verboseLogging)
                Debug.Log($"[ForestQuestController] → CompletePhase: {Format(entry)}");

            requestCompletePhaseEvent?.Raise(
                new CompletePhaseRequest(entry.questID, entry.objectiveID, entry.phaseID));
        }

        // =============================================
        // 퀘스트 상태 조회 (외부에서 사용)
        // =============================================

        /// <summary>특정 phase가 완료됐는지 확인.</summary>
        public bool IsPhaseCompleted(string phaseID)
        {
            int index = FindIndexByPhaseID(phaseID);
            if (index < 0) return false;
            return IsAlreadyCompleted(phases[index]);
        }

        /// <summary>퀘스트가 완료됐는지 확인.</summary>
        public bool IsQuestCompleted(string questID)
        {
            return Managers.Quest?.IsQuestCompleted(questID) ?? false;
        }

        /// <summary>퀘스트가 활성 상태인지 확인.</summary>
        public bool IsQuestActive(string questID)
        {
            return Managers.Quest?.IsQuestActive(questID) ?? false;
        }

        // =============================================
        // Helpers
        // =============================================

        private int FindIndexByPhaseID(string phaseID)
        {
            if (phases == null) return -1;
            for (int i = 0; i < phases.Length; i++)
                if (phases[i].phaseID == phaseID) return i;
            return -1;
        }

        private bool ArePrereqsSatisfied(int index)
        {
            for (int i = 0; i < index; i++)
            {
                var prev = phases[i];
                if (string.IsNullOrEmpty(prev.questID) ||
                    string.IsNullOrEmpty(prev.objectiveID) ||
                    string.IsNullOrEmpty(prev.phaseID))
                    continue;

                // 완료된 퀘스트도 조회 (MQ-01 완료 후 MQ-02 진행 시)
                var quest = Managers.Quest?.GetActiveQuest(prev.questID)
                         ?? Managers.Quest?.GetCompletedQuest(prev.questID);
                if (quest == null) return false;

                var phase = quest.GetPhase(prev.objectiveID, prev.phaseID);
                if (phase == null || !phase.IsCompleted)
                    return false;
            }
            return true;
        }

        private bool IsAlreadyCompleted(PhaseEntry entry)
        {
            var quest = Managers.Quest?.GetActiveQuest(entry.questID)
                     ?? Managers.Quest?.GetCompletedQuest(entry.questID);
            var phase = quest?.GetPhase(entry.objectiveID, entry.phaseID);
            return phase != null && phase.IsCompleted;
        }

        private string Format(PhaseEntry e)
        {
            return string.IsNullOrEmpty(e.label)
                ? $"[{e.questID}/{e.phaseID}]"
                : $"[{e.questID}/{e.phaseID}] {e.label}";
        }

        private void LogSequence()
        {
            if (phases == null || phases.Length == 0) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[ForestQuestController] Forest 씬 퀘스트 시퀀스:");
            for (int i = 0; i < phases.Length; i++)
            {
                var e = phases[i];
                string tag = e.handledByThisScene ? "★ this scene" : "  (other)  ";
                sb.AppendLine($"  [{i:D2}] {tag}  {Format(e)}");
            }
            Debug.Log(sb.ToString());
        }

#if UNITY_EDITOR
        [ContextMenu("Log Sequence")]
        private void ContextLogSequence() => LogSequence();
#endif
    }
}