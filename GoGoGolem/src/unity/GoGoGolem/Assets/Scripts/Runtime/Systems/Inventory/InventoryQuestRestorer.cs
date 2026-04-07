using System;
using UnityEngine;

/// <summary>
/// [개발/디버그 보조용] 씬 시작 시 JSON 인벤토리와 퀘스트 진행 상태를 대조해 아이템을 보정한다.
///
/// 필요성:
///   인벤토리는 기본적으로 JSON으로 저장/복원되므로 정상 플레이 흐름에서는 이 컴포넌트가 없어도 된다.
///   다음 상황에서 보조 수단으로 사용한다:
///     - 개발 중 특정 퀘스트 단계부터 테스트할 때 (JSON 없이 아이템 자동 세팅)
///     - JSON 삭제 또는 오염 시 퀘스트 기반으로 자동 복구
///     - "퀘스트는 완료됐는데 아이템이 없는" 엣지 케이스 수정
///
/// 동작:
///   requiredPhaseID 완료 + consumedPhaseID 미완료 → 아이템 있어야 함 → 없으면 추가
///   그 외 → 아이템 없어야 함 → 있으면 제거
///
/// Forest 씬 세팅 예시:
///   Rule 0: itemID=ITEM-001  required=MQ-01-P06  consumed=MQ-02-P07
///   Rule 1: itemID=ITEM-002  required=MQ-02-P02  consumed=MQ-02-P03
/// </summary>
public class InventoryQuestRestorer : MonoBehaviour
{
    [Serializable]
    public class ReconcileRule
    {
        [Tooltip("보정할 아이템 ID")]
        public string itemID;
        [Tooltip("이 phase가 완료돼야 아이템을 보유")]
        public string requiredPhaseID;
        [Tooltip("이 phase가 완료되면 아이템 소비됨 (없어야 함). 비워두면 소비 조건 없음.")]
        public string consumedPhaseID;
    }

    [SerializeField] private ReconcileRule[] rules;

    private void Start()
    {
        if (Managers.Inventory == null)
        {
            Debug.LogWarning("[InventoryQuestRestorer] Managers.Inventory null — 보정 스킵");
            return;
        }
        if (Managers.Quest == null)
        {
            Debug.LogWarning("[InventoryQuestRestorer] Managers.Quest null — 보정 스킵");
            return;
        }
        if (rules == null || rules.Length == 0)
        {
            Debug.LogWarning("[InventoryQuestRestorer] rules가 비어있음 — 보정 스킵");
            return;
        }

        Debug.Log($"[InventoryQuestRestorer] 보정 시작 ({rules.Length}개 규칙) / " +
                  $"activeQuests={Managers.Quest.GetAllActiveQuests().Count} " +
                  $"completedQuests={Managers.Quest.GetAllCompletedQuests().Count}");

        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule.itemID) || string.IsNullOrEmpty(rule.requiredPhaseID))
                continue;

            bool acquired = IsPhaseCompleted(rule.requiredPhaseID);
            bool consumed = !string.IsNullOrEmpty(rule.consumedPhaseID)
                            && IsPhaseCompleted(rule.consumedPhaseID);
            bool shouldHave = acquired && !consumed;

            Debug.Log($"[InventoryQuestRestorer] {rule.itemID}: " +
                      $"required({rule.requiredPhaseID})={acquired} " +
                      $"consumed({rule.consumedPhaseID})={consumed} " +
                      $"→ shouldHave={shouldHave}");

            Managers.Inventory.ReconcileItem(rule.itemID, shouldHave);
        }
    }

    private bool IsPhaseCompleted(string phaseID)
    {
        var qm = Managers.Quest;
        foreach (var quest in qm.GetAllActiveQuests())
            if (SearchPhase(quest, phaseID)) return true;
        foreach (var quest in qm.GetAllCompletedQuests())
            if (SearchPhase(quest, phaseID)) return true;
        return false;
    }

    private bool SearchPhase(Quest quest, string phaseID)
    {
        foreach (var obj in quest.GetAllObjectives())
        {
            var phase = obj.GetPhase(phaseID);
            if (phase == null) continue;
            // 퀘스트 Status가 Completed면 모든 phase를 완료로 간주.
            // quest.IsCompleted()는 objectives 기반이라 RestoreCompletedQuest 후 false를 반환하므로 사용 불가.
            // (RestoreCompletedQuest는 quest.Complete()만 호출해 Status만 세팅, 개별 phase는 복원하지 않음)
            return quest.Status == QuestStatus.Completed || phase.IsCompleted;
        }
        return false;
    }

#if UNITY_EDITOR
    [ContextMenu("Run Reconcile Now (Play Mode Only)")]
    private void DebugRunNow()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Play Mode에서만 동작합니다."); return; }
        Start();
    }
#endif
}
