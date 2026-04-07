using System;
using UnityEngine;
using UI.Presenters;

namespace Demo.Chapters.Prologue
{
    /// <summary>
    /// Room(숙소) 씬의 퀘스트(MQ-03, MQ-04) 흐름을 한 곳에서 문서화하고 라우팅하는 컨트롤러.
    ///
    /// 역할:
    ///   - Room 씬에서 진행되어야 할 MQ-03 / MQ-04 phase의 순서 전체를 Inspector에 선언한다.
    ///   - 외부 소스가 CompleteByPhaseID() / CompleteAt()을 호출하면
    ///     enforceOrder gate 체크 후 QuestManager에 CompletePhase 이벤트를 발행한다.
    ///   - demoMode가 true이면 씬 진입 시 MQ-03 자동 시작 및 정착지 phase(P01~P03) 자동 완료.
    ///
    /// 설계 원칙:
    ///   - 소스 컴포넌트에 의존하지 않는다.
    ///     소스 쪽에서 UnityEvent 등으로 이 컨트롤러의 public 메소드를 호출한다.
    ///   - questID를 Inspector로 받아 재사용 가능.
    ///
    /// ──────────────────────────────────────────────────────
    /// MQ-03 Phase 완료 경로 (전체)
    /// ──────────────────────────────────────────────────────
    ///
    /// 퀘스트 시작:
    ///   MQ-02 완료 후 정착지 씬 진입 시 (데모에서는 Room 씬 Start에서 자동)
    ///
    /// ★ = 이 컨트롤러(RoomQuestController)를 경유
    ///
    ///   P01  정착지 입성             정착지 씬 → 아주머니 NPC 조우 → 자동 트리거
    ///                              (데모: ★ Start()에서 자동 완료)
    ///
    ///   P02  자유 탐색              정착지 씬 → 수레, 우물, 텃밭 등 상호작용 루프
    ///                              (데모: ★ Start()에서 자동 완료)
    ///
    ///   P03  숙소 진입              정착지 씬 → 숙소 장소 이동
    ///                              (데모: ★ Start()에서 자동 완료)
    ///
    ///   ★P04  편지 작성             편지 시스템 → CompleteByPhaseID("MQ-03-P04")
    ///
    ///   ★P05  취침                  타임라인 연출 → CompleteByPhaseID("MQ-03-P05")
    ///                              (MQ-03 완료 → MQ-04 자동 시작)
    ///
    /// ──────────────────────────────────────────────────────
    /// MQ-04 Phase 완료 경로 (전체)
    /// ──────────────────────────────────────────────────────
    ///
    /// 퀘스트 시작:
    ///   MQ-03 완료 직후 (이 컨트롤러가 requestStartQuestEvent로 발행)
    ///
    ///   ★P01  편지 확인             답장 읽기 UI → CompleteByPhaseID("MQ-04-P01")
    ///
    ///   ★P02  데모 종료             E로 문 상호작용 → CompleteByPhaseID("MQ-04-P02")
    ///                              시네마틱 종료
    /// </summary>
    public class RoomQuestController : MonoBehaviour
    {

        [Serializable]
        public struct PhaseEntry
        {
            [Tooltip("사람이 읽기 위한 라벨 (예: 'P01 정착지 입성')")]
            public string label;

            [Tooltip("Quest ID (예: MQ-03)")]
            public string questID;

            [Tooltip("Objective ID (예: MQ-03-OBJ-01)")]
            public string objectiveID;

            [Tooltip("Phase ID (예: MQ-03-P01)")]
            public string phaseID;

            [Tooltip("이 씬이 이 phase를 책임지는가? false면 선언만 하고 호출은 무시 (문서 용도)")]
            public bool handledByThisScene;

            [Tooltip("데모 모드에서 Start 시 자동 완료할 phase인가?")]
            public bool autoCompleteInDemo;
        }

        [Header("Quest IDs")]
        [SerializeField] private string primaryQuestID = "MQ-03";
        [SerializeField] private string secondaryQuestID = "MQ-04";

        [Header("Phase Sequence (순서대로 입력)")]
        [Tooltip("이 씬에서 진행되어야 할 퀘스트의 phase들을 순서대로 나열한다.\n" +
                 "handledByThisScene이 true인 phase만 이 컨트롤러가 발행한다.")]
        [SerializeField] private PhaseEntry[] phases;

        [Header("Event Channels")]
        [SerializeField] private StringGameEvent requestStartQuestEvent;
        [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

        [Header("Scene Sources (Room 씬 전용)")]
        [SerializeField] private LetterWritePresenter letterWritePresenter;
        [SerializeField] private BedInteraction bedInteraction;
        [SerializeField] private LetterReadPresenter letterReadPresenter;
        [Tooltip("Sleep 타임라인 종료 후 재생할 Bark. 완료 시 MQ-04 시작.")]
        [SerializeField] private BarkTrigger morningBark;
        [Tooltip("Room 씬의 출구 문. Interact 시 MQ-04-P02 완료 발행.")]
        [SerializeField] private ChangeSceneInteraction exitDoor;

        [Header("Options")]
        [Tooltip("true면 정착지 씬 없이 바로 Room 씬에서 시작. MQ-03을 자동 시작하고 P01~P03을 자동 완료한다.")]
        [SerializeField] private bool demoMode = true;

        [Tooltip("true면 이전 phase들이 완료되지 않은 상태로 호출되면 경고하고 무시한다.")]
        [SerializeField] private bool enforceOrder = true;

        [SerializeField] private bool verboseLogging = true;

        private void OnEnable()
        {
            if (letterWritePresenter != null)
                letterWritePresenter.OnLetterSubmitted += HandleLetterSubmitted;
            else if (verboseLogging)
                Debug.LogWarning("[RoomQuestController] 구독 실패: letterWritePresenter 미연결 (P04 편지 작성)");

            if (bedInteraction != null)
                bedInteraction.OnSlept += HandleSlept;
            else if (verboseLogging)
                Debug.LogWarning("[RoomQuestController] 구독 실패: bedInteraction 미연결 (P05 취침)");

            if (letterReadPresenter != null)
                letterReadPresenter.OnPanelToggled += HandleLetterReadToggled;
            else if (verboseLogging)
                Debug.LogWarning("[RoomQuestController] 구독 실패: letterReadPresenter 미연결 (MQ-04-P01 편지 읽기)");

            if (exitDoor != null)
                exitDoor.OnInteracted += HandleExitDoor;
            else if (verboseLogging)
                Debug.LogWarning("[RoomQuestController] 구독 실패: exitDoor 미연결 (MQ-04-P02 데모 종료)");

            if (verboseLogging)
                Debug.Log($"[RoomQuestController] 구독 완료 — " +
                    $"letterWrite:{letterWritePresenter != null} " +
                    $"bed:{bedInteraction != null} " +
                    $"letterRead:{letterReadPresenter != null} " +
                    $"exitDoor:{exitDoor != null}");
        }

        private void OnDisable()
        {
            if (letterWritePresenter != null)
                letterWritePresenter.OnLetterSubmitted -= HandleLetterSubmitted;
            if (bedInteraction != null)
                bedInteraction.OnSlept -= HandleSlept;
            if (letterReadPresenter != null)
                letterReadPresenter.OnPanelToggled -= HandleLetterReadToggled;
            if (exitDoor != null)
                exitDoor.OnInteracted -= HandleExitDoor;

            if (verboseLogging)
                Debug.Log("[RoomQuestController] 구독 해제 완료");
        }

        private void HandleLetterSubmitted()
        {
            if (verboseLogging)
                Debug.Log("[RoomQuestController] 이벤트 수신: OnLetterSubmitted → MQ-03-P04");
            CompleteByPhaseID("MQ-03-P04");
        }

        private void HandleSlept()
        {
            if (verboseLogging)
                Debug.Log("[RoomQuestController] 이벤트 수신: OnSlept → MQ-03-P05");
            CompleteByPhaseID("MQ-03-P05");
        }

        private void HandleLetterReadToggled(bool isOpen)
        {
            if (verboseLogging)
                Debug.Log($"[RoomQuestController] 이벤트 수신: OnPanelToggled(isOpen={isOpen})");
            if (!isOpen) CompleteByPhaseID("MQ-04-P01");
        }

        private void HandleExitDoor()
        {
            if (verboseLogging)
                Debug.Log("[RoomQuestController] 이벤트 수신: OnInteracted (exitDoor) → MQ-04-P02");
            CompleteByPhaseID("MQ-04-P02");
        }

        private void Start()
        {
            if (requestCompletePhaseEvent == null)
                Debug.LogError("[RoomQuestController] requestCompletePhaseEvent가 연결되지 않았습니다.");
            if (requestStartQuestEvent == null)
                Debug.LogError("[RoomQuestController] requestStartQuestEvent가 연결되지 않았습니다.");
            if (phases == null || phases.Length == 0)
                Debug.LogWarning("[RoomQuestController] phases가 비어있습니다.");
            if (Managers.Quest == null)
                Debug.LogError("[RoomQuestController] Managers.Quest가 없습니다. 퀘스트 시스템 미초기화.");

            if (verboseLogging)
            {
                LogSequence();
                Debug.Log($"[RoomQuestController] 설정 — demoMode:{demoMode} enforceOrder:{enforceOrder}");
            }

            if (demoMode) RunDemoSetup();
        }

        // =============================================
        // Demo Setup
        // =============================================

        /// <summary>
        /// 데모 빌드용: MQ-03 시작 후 정착지 phase(P01~P03)를 자동 완료한다.
        /// 정착지 씬이 구현되면 demoMode를 false로 전환하면 된다.
        /// </summary>
        private void RunDemoSetup()
        {
            if (verboseLogging)
                Debug.Log("[RoomQuestController] 데모 모드: MQ-03 자동 시작 및 정착지 phase 자동 완료");

            // MQ-03이 아직 시작되지 않았으면 시작
            if (Managers.Quest != null && !Managers.Quest.IsQuestActive(primaryQuestID))
            {
                requestStartQuestEvent?.Raise(primaryQuestID);
            }

            // autoCompleteInDemo로 표시된 phase들을 순차 완료
            if (phases == null) return;
            for (int i = 0; i < phases.Length; i++)
            {
                var entry = phases[i];
                if (!entry.autoCompleteInDemo) continue;
                if (IsAlreadyCompleted(entry)) continue;

                if (verboseLogging)
                    Debug.Log($"[RoomQuestController] 데모 자동완료: {Format(entry)}");

                requestCompletePhaseEvent?.Raise(
                    new CompletePhaseRequest(entry.questID, entry.objectiveID, entry.phaseID));
            }
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
                Debug.LogWarning($"[RoomQuestController] phaseID '{phaseID}'가 시퀀스에 없습니다.");
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
                Debug.LogWarning($"[RoomQuestController] 잘못된 phase index: {index}");
                return;
            }

            var entry = phases[index];

            if (!entry.handledByThisScene)
            {
                Debug.LogWarning($"[RoomQuestController] {Format(entry)}는 이 씬 담당이 아닙니다. 무시.");
                return;
            }

            if (Managers.Quest == null)
            {
                Debug.LogError("[RoomQuestController] Managers.Quest가 없습니다.");
                return;
            }

            if (!Managers.Quest.IsQuestActive(entry.questID))
            {
                Debug.Log($"[RoomQuestController] {entry.questID}가 비활성. {Format(entry)} 무시.");
                return;
            }

            if (IsAlreadyCompleted(entry))
            {
                if (verboseLogging)
                    Debug.Log($"[RoomQuestController] {Format(entry)}는 이미 완료됨. 스킵.");
                return;
            }

            if (enforceOrder && !ArePrereqsSatisfied(index))
            {
                Debug.LogWarning($"[RoomQuestController] {Format(entry)}의 선행 phase가 아직 미완. 호출 무시.");
                return;
            }

            if (verboseLogging)
                Debug.Log($"[RoomQuestController] → CompletePhase: {Format(entry)}");

            requestCompletePhaseEvent?.Raise(
                new CompletePhaseRequest(entry.questID, entry.objectiveID, entry.phaseID));
        }

        /// <summary>
        /// 타임라인 Signal에서 호출. morningBark를 재생하고, 완료 후 MQ-04를 시작한다.
        /// </summary>
        public void FireMorningBarkThenStartQuest()
        {
            if (morningBark == null)
            {
                Debug.LogWarning("[RoomQuestController] morningBark가 미연결. MQ-04 바로 시작.");
                StartSecondaryQuest();
                return;
            }

            if (verboseLogging)
                Debug.Log("[RoomQuestController] morningBark 발화 → 완료 후 MQ-04 시작 예정");

            morningBark.Fire(() =>
            {
                if (verboseLogging)
                    Debug.Log("[RoomQuestController] morningBark 완료 → MQ-04 시작");
                StartSecondaryQuest();
            });
        }

        /// <summary>
        /// MQ-04를 시작한다.
        /// </summary>
        public void StartSecondaryQuest()
        {
            if (Managers.Quest != null && Managers.Quest.IsQuestActive(secondaryQuestID))
            {
                if (verboseLogging)
                    Debug.Log($"[RoomQuestController] {secondaryQuestID}는 이미 활성 상태.");
                return;
            }

            if (verboseLogging)
                Debug.Log($"[RoomQuestController] → StartQuest: {secondaryQuestID}");

            requestStartQuestEvent?.Raise(secondaryQuestID);
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

        /// <summary>index 앞의 모든 phase(같은 questID에 한정)가 완료됐는지 확인.</summary>
        private bool ArePrereqsSatisfied(int index)
        {
            var entry = phases[index];

            for (int i = 0; i < index; i++)
            {
                var prev = phases[i];
                // 다른 퀘스트의 phase는 선행 조건에서 제외
                if (prev.questID != entry.questID) continue;
                if (string.IsNullOrEmpty(prev.objectiveID) || string.IsNullOrEmpty(prev.phaseID))
                    continue;

                var quest = Managers.Quest?.GetActiveQuest(prev.questID);
                if (quest == null) return false;

                var phase = quest.GetPhase(prev.objectiveID, prev.phaseID);
                if (phase == null || !phase.IsCompleted)
                    return false;
            }
            return true;
        }

        private bool IsAlreadyCompleted(PhaseEntry entry)
        {
            var quest = Managers.Quest?.GetActiveQuest(entry.questID);
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
            sb.AppendLine($"[RoomQuestController] quest sequence:");
            for (int i = 0; i < phases.Length; i++)
            {
                var e = phases[i];
                string tag = e.handledByThisScene ? "★ this scene" : "  (other)";
                string demo = e.autoCompleteInDemo ? " [DEMO-AUTO]" : "";
                sb.AppendLine($"  [{i}] {tag}  {e.questID} {Format(e)}{demo}");
            }
            Debug.Log(sb.ToString());
        }

#if UNITY_EDITOR
        [ContextMenu("Log Sequence")]
        private void ContextLogSequence() => LogSequence();
#endif
    }
}
