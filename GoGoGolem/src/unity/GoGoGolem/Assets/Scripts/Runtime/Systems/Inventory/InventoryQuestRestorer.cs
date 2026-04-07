using System;
using UnityEngine;

/// <summary>
/// 씬 시작 시 JSON 인벤토리와 퀘스트 진행 상태를 대조해 아이템을 보정한다.
///
/// 동작:
///   requiredPhaseID 완료 + consumedPhaseID 미완료 → 아이템 있어야 함
///   그 외 → 아이템 없어야 함
///   → InventoryManager.ReconcileItem으로 추가/제거
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
        if (Managers.Inventory == null || Managers.Quest == null)
        {
            Debug.LogWarning("[InventoryQuestRestorer] Managers 준비 안 됨 — 보정 스킵");
            return;
        }

        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule.itemID) || string.IsNullOrEmpty(rule.requiredPhaseID))
                continue;

            bool acquired = IsPhaseCompleted(rule.requiredPhaseID);
            bool consumed = !string.IsNullOrEmpty(rule.consumedPhaseID)
                            && IsPhaseCompleted(rule.consumedPhaseID);

            Managers.Inventory.ReconcileItem(rule.itemID, acquired && !consumed);
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
            if (phase != null) return phase.IsCompleted;
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
