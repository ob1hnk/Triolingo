using System;
using UnityEngine;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬의 퀘스트(MQ-02) 흐름을 한 곳에서 문서화하고 라우팅하는 컨트롤러.
    ///
    /// 역할:
    ///   - Forest 씬에서 진행되어야 할 MQ-02 phase의 순서 전체를 Inspector에 선언한다.
    ///   - 외부 소스가 CompleteByPhaseID() / CompleteAt()을 호출하면
    ///     enforceOrder gate 체크 후 QuestManager에 CompletePhase 이벤트를 발행한다.
    ///
    /// 설계 원칙:
    ///   - 소스 컴포넌트(ItemUsableZone, TriggerZone 등)에 의존하지 않는다.
    ///     소스 쪽에서 UnityEvent 등으로 이 컨트롤러의 public 메소드를 호출한다.
    ///   - questID를 Inspector로 받아 재사용 가능.
    ///
    /// ──────────────────────────────────────────────────────
    /// MQ-02 Phase 완료 경로 (전체)
    /// ──────────────────────────────────────────────────────
    ///
    /// 퀘스트 시작:
    ///   DLG_006.yarn  →  <<start_quest MQ-02>>  →  QuestYarnCommands  →  QuestManager
    ///
    /// ★ = 이 컨트롤러(ForestQuestController)를 경유
    ///
    ///   *P01  강가 도착               TriggerZone.UnityEvent → CompleteByPhaseID("MQ-02-P01")
    ///                              (TriggerZone은 팀원이 구현 중)
    ///
    ///   P02  상황 파악              DLG_007.yarn → <<complete_phase>> → QuestYarnCommands
    ///                              (같은 노드에서 <<give_item ITEM-002>>로 식량 꾸러미 지급)
    ///
    ///   *P03  아이템 사용 (ITEM-001)  ItemUsableZone step[0].onPlaced → CompleteByPhaseID("MQ-02-P03")
    ///                              (gate: P02 완료 필요)
    ///
    ///   P04  제스처 인식              GestureSceneController.NotifyEntryPhaseComplete() — 씬 진입 시
    ///   P05  전달 실패               GestureSceneController.NotifyQuestPhaseComplete() — 제스처 성공 시
    ///
    ///   P06  골렘 대화               GolemDialogueSceneController.NotifyQuestPhaseComplete() — 대화 종료 시
    ///
    ///   *P07  아이템 사용 (ITEM-002)  ItemUsableZone step[1].onPlaced → CompleteByPhaseID("MQ-02-P07")
    ///                              (gate: P06 완료 필요)
    ///
    ///   P08  제스처 인식              GestureSceneController.NotifyEntryPhaseComplete() — 씬 진입 시
    ///   P09  전달 성공               GestureSceneController.NotifyQuestPhaseComplete() — 제스처 성공 시
    ///
    ///   P10  완료                    DLG_012.yarn → <<complete_phase>> → QuestYarnCommands
    ///                              (QuestManager가 자동으로 퀘스트 완료 처리)
    /// </summary>
    public class ForestQuestController : MonoBehaviour
    {
        [Serializable]
        public struct PhaseEntry
        {
            [Tooltip("사람이 읽기 위한 라벨 (예: 'P01 강가 도착')")]
            public string label;

            [Tooltip("Objective ID (예: MQ-02-OBJ-01)")]
            public string objectiveID;

            [Tooltip("Phase ID (예: MQ-02-P01)")]
            public string phaseID;

            [Tooltip("이 씬이 이 phase를 책임지는가? false면 선언만 하고 호출은 무시 (문서 용도)")]
            public bool handledByThisScene;
        }

        [Header("Quest")]
        [SerializeField] private string questID = "MQ-02";

        [Header("Phase Sequence (순서대로 입력)")]
        [Tooltip("이 씬에서 진행되어야 할 퀘스트의 phase들을 순서대로 나열한다.\n" +
                 "handledByThisScene이 true인 phase만 이 컨트롤러가 발행한다.")]
        [SerializeField] private PhaseEntry[] phases;

        [Header("Event Channel")]
        [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

        [Header("Options")]
        [Tooltip("true면 이전 phase들이 완료되지 않은 상태로 호출되면 경고하고 무시한다.")]
        [SerializeField] private bool enforceOrder = true;

        [SerializeField] private bool verboseLogging = true;

        private void Start()
        {
            if (requestCompletePhaseEvent == null)
                Debug.LogError($"[ForestQuestController] requestCompletePhaseEvent가 연결되지 않았습니다.");
            if (phases == null || phases.Length == 0)
                Debug.LogWarning($"[ForestQuestController] phases가 비어있습니다.");

            if (verboseLogging) LogSequence();
        }

        // =============================================
        // Public API — 외부 소스가 호출
        // =============================================

        /// <summary>
        /// phaseID로 해당 phase를 완료 요청. UnityEvent에서 string 인자로 바로 바인딩 가능.
        /// </summary>
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

        /// <summary>
        /// 시퀀스 index로 완료 요청. UnityEvent에서 int 인자로 바인딩 가능.
        /// </summary>
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

            if (!Managers.Quest.IsQuestActive(questID))
            {
                Debug.Log($"[ForestQuestController] {questID}가 비활성. {Format(entry)} 무시.");
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
                new CompletePhaseRequest(questID, entry.objectiveID, entry.phaseID));
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

        /// <summary>index 앞의 모든 phase(이 씬 담당 여부와 무관)가 완료됐는지 확인.</summary>
        private bool ArePrereqsSatisfied(int index)
        {
            var quest = Managers.Quest?.GetActiveQuest(questID);
            if (quest == null) return false;

            for (int i = 0; i < index; i++)
            {
                var prev = phases[i];
                if (string.IsNullOrEmpty(prev.objectiveID) || string.IsNullOrEmpty(prev.phaseID))
                    continue;

                var phase = quest.GetPhase(prev.objectiveID, prev.phaseID);
                if (phase == null || !phase.IsCompleted)
                    return false;
            }
            return true;
        }

        private bool IsAlreadyCompleted(PhaseEntry entry)
        {
            var quest = Managers.Quest?.GetActiveQuest(questID);
            var phase = quest?.GetPhase(entry.objectiveID, entry.phaseID);
            return phase != null && phase.IsCompleted;
        }

        private string Format(PhaseEntry e)
        {
            return string.IsNullOrEmpty(e.label)
                ? $"[{e.phaseID}]"
                : $"[{e.phaseID}] {e.label}";
        }

        private void LogSequence()
        {
            if (phases == null || phases.Length == 0) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ForestQuestController] {questID} sequence:");
            for (int i = 0; i < phases.Length; i++)
            {
                var e = phases[i];
                string tag = e.handledByThisScene ? "★ this scene" : "  (other)";
                sb.AppendLine($"  [{i}] {tag}  {Format(e)}");
            }
            Debug.Log(sb.ToString());
        }

#if UNITY_EDITOR
        [ContextMenu("Log Sequence")]
        private void ContextLogSequence() => LogSequence();
#endif
    }
}