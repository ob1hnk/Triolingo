using System;
using UnityEngine;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Forest 씬의 퀘스트(MQ-02) 흐름을 한 곳에서 문서화하고 라우팅하는 컨트롤러.
    ///
    /// 역할:
    ///   - Forest 씬에서 진행되어야 할 MQ-02 phase의 **순서 전체**를 Inspector에 선언한다.
    ///   - 외부 이벤트 소스(트리거 존, ItemUsableZone, UI 등)가 public 메소드를 호출하면
    ///     QuestManager에 CompletePhase 이벤트를 대신 발행해준다.
    ///   - Forest 씬이 책임지지 않는 phase(제스처, 골렘 대화, 할아버지 대화 등)는
    ///     여전히 각자의 시스템에서 직접 발행한다. 여기 선언만 해두면 "순서를 눈으로" 볼 수 있다.
    ///
    /// 설계 원칙:
    ///   - ItemUsableZone, TriggerZone 등 소스 컴포넌트에 의존하지 않는다.
    ///     그쪽에서 UnityEvent/코드/이벤트 구독 등 어떤 방식으로든 이 컨트롤러의
    ///     public 메소드를 호출하기만 하면 된다.
    ///   - 단일 퀘스트(MQ-02) 전용이 아니라 questID를 Inspector로 받아 재사용 가능.
    ///     예: 같은 패턴으로 MQ-01을 담당하는 또 다른 인스턴스를 씬에 둘 수 있음.
    ///
    /// MQ-02 Forest 씬 phase 참고 (씬이 책임지는 것만 ★표시):
    ///   P01 강가 도착                  ★ Forest (트리거 존 진입)
    ///   P02 상황 파악                     Yarn <<complete_phase>>
    ///   P03 아이템 사용 (ITEM-001)      ★ Forest (ItemUsableZone step 0)
    ///   P04 제스처 인식                   Gesture 씬
    ///   P05 전달 실패                     Gesture 씬
    ///   P06 음성 인식                     골렘 대화 시스템
    ///   P07 아이템 사용 (ITEM-002)      ★ Forest (ItemUsableZone step 1)
    ///   P08 제스처 인식                   Gesture 씬
    ///   P09 전달 성공                     Gesture 씬
    ///   P10 완료                          할아버지 NPCQuestHandler
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