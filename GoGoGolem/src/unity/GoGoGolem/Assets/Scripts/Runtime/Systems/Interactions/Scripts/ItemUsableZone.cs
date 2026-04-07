using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 이 존 안에 있을 때 인벤토리에서 특정 아이템을 순서대로 배치할 수 있다.
/// Trigger Collider가 필요하며, 플레이어 오브젝트에 "Player" 태그가 있어야 한다.
///
/// 사용 흐름:
///   1. 플레이어가 존에 진입 → Current = this, glow 활성화
///   2. 인벤토리 열고 아이템 선택 + E 키 (InventoryUIPresenter가 처리)
///   3. 현재 스텝의 itemID와 일치하면 TryPlace로 spawnPoint에 프리팹 스폰, 다음 스텝으로 진행
///   4. 모든 스텝 완료 후에는 Accepts가 false를 반환
/// </summary>
[RequireComponent(typeof(Collider))]
public class ItemUsableZone : MonoBehaviour, IInteractable
{
    /// <summary>현재 플레이어가 위치한 존. 없으면 null.</summary>
    public static ItemUsableZone Current { get; private set; }

    [Serializable]
    public class PlacementStep
    {
        [Tooltip("이 스텝에서 허용되는 아이템 ID")]
        public string itemID;
        [Tooltip("이 스텝에서 스폰할 프리팹")]
        public GameObject prefab;

        [Header("Phase Gate (선택)")]
        [Tooltip("이 phase가 완료되어야 배치 가능. 비워두면 gate 없음.")]
        public string requiredPhaseID;
        [Tooltip("requiredPhaseID가 속한 Quest ID (completedPhaseID 조회에도 사용)")]
        public string gateQuestID;
        [Tooltip("requiredPhaseID가 속한 Objective ID")]
        public string gateObjectiveID;

        [Header("Completion Phase (씬 재진입 시 복원용)")]
        [Tooltip("이 스텝이 완료됐음을 나타내는 phase ID. 씬 재로드 시 _currentStep 복원에 사용.\n" +
                 "gateQuestID와 동일한 퀘스트에서 탐색. 비워두면 복원 안 함.")]
        public string completedPhaseID;

        [Header("Callback")]
        [Tooltip("배치 성공 시 호출. ForestQuestController.CompleteByPhaseID 등을 바인딩.")]
        public UnityEvent onPlaced;
    }

    [Header("Sequential Placement")]
    [Tooltip("순서대로 배치해야 할 아이템 목록. 한 번 배치되면 다음 스텝으로 넘어간다.")]
    [SerializeField] private PlacementStep[] sequence;

    [Header("Spawn")]
    [Tooltip("비워두면 이 오브젝트 위치에 스폰")]
    [SerializeField] private Transform spawnPoint;

    [Header("Visual")]
    [Tooltip("플레이어 진입 시 활성화할 발광 오브젝트 (자식으로 배치)")]
    [SerializeField] private GameObject glowIndicator;

    [Header("Interaction Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    private int _currentStep;
    private GameObject _spawnedInstance;

    public bool IsComplete => sequence == null || _currentStep >= sequence.Length;
    public string NextExpectedItemID => IsComplete ? null : sequence[_currentStep].itemID;

    // ── IInteractable ──────────────────────────────────
    public InteractionType InteractionType => InteractionType.UseItem;
    public bool CanInteract => !IsComplete && IsGateSatisfied(_currentStep);
    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "아이템 사용";
    public Sprite GetKeyHintSprite() => promptData?.KeyHintSprite;
    public Vector3 GetPromptOffset() => promptData != null ? promptData.WorldOffset : new Vector3(0f, 1.5f, 0f);
    public void Interact() => GameStateManager.Instance?.ChangeState(GameState.InventoryUI);
    // ──────────────────────────────────────────────────

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (glowIndicator != null) glowIndicator.SetActive(false);
    }

    private void Start()
    {
        if (sequence == null || sequence.Length == 0)
        {
            Debug.LogWarning($"[ItemUsableZone] '{name}': sequence가 비어있습니다. 아무 아이템도 배치할 수 없습니다.");
            return;
        }

        for (int i = 0; i < sequence.Length; i++)
        {
            var s = sequence[i];
            if (string.IsNullOrEmpty(s.itemID))
                Debug.LogError($"[ItemUsableZone] '{name}': sequence[{i}]의 itemID가 비어있습니다.");
            if (s.prefab == null)
                Debug.LogError($"[ItemUsableZone] '{name}': sequence[{i}] ({s.itemID})의 prefab이 비어있습니다.");
        }

        RestoreCurrentStep();

        var sb = new System.Text.StringBuilder();
        sb.Append($"[ItemUsableZone] '{name}' sequence: ");
        for (int i = 0; i < sequence.Length; i++)
        {
            if (i > 0) sb.Append(" → ");
            sb.Append($"[{i}] {sequence[i].itemID}");
        }
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 씬 재진입 시 퀘스트 phase 완료 상태 기반으로 _currentStep을 복원한다.
    /// 각 PlacementStep의 completedPhaseID가 완료됐으면 해당 스텝은 이미 배치된 것으로 간주.
    /// </summary>
    private void RestoreCurrentStep()
    {
        if (Managers.Quest == null) return;

        for (int i = 0; i < sequence.Length; i++)
        {
            if (!IsStepAlreadyPlaced(sequence[i])) break;

            // 프리팹 재스폰 (씬 리로드로 사라진 것 복원)
            var step = sequence[i];
            if (step.prefab != null)
            {
                if (_spawnedInstance != null) Destroy(_spawnedInstance);
                Transform sp = spawnPoint != null ? spawnPoint : transform;
                _spawnedInstance = Instantiate(step.prefab, sp.position, sp.rotation);
            }

            _currentStep = i + 1;
        }

        if (_currentStep > 0)
            Debug.Log($"[ItemUsableZone] '{name}' 씬 재진입: step {_currentStep}까지 배치 완료 상태로 복원");
    }

    /// <summary>
    /// 이 스텝의 completedPhaseID가 완료됐으면 true. gateQuestID 퀘스트 전체에서 탐색.
    /// </summary>
    private bool IsStepAlreadyPlaced(PlacementStep step)
    {
        if (string.IsNullOrEmpty(step.completedPhaseID) || string.IsNullOrEmpty(step.gateQuestID))
            return false;

        var quest = Managers.Quest.GetActiveQuest(step.gateQuestID)
                 ?? Managers.Quest.GetCompletedQuest(step.gateQuestID);
        if (quest == null) return false;

        // 퀘스트 자체가 완료됐으면 모든 phase 완료
        if (quest.Status == QuestStatus.Completed) return true;

        // objective 전체 탐색 (phaseID만으로 조회)
        foreach (var obj in quest.GetAllObjectives())
        {
            var phase = obj.GetPhase(step.completedPhaseID);
            if (phase != null) return phase.IsCompleted;
        }
        return false;
    }

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
            if (glowIndicator != null) glowIndicator.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Current = this;
        if (glowIndicator != null) glowIndicator.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || Current != this) return;
        Current = null;
        if (glowIndicator != null) glowIndicator.SetActive(false);
    }

    /// <summary>현재 스텝에서 이 itemID를 배치할 수 있는지 검사.</summary>
    public bool Accepts(string itemID)
    {
        if (IsComplete) return false;
        if (!IsGateSatisfied(_currentStep)) return false;
        return sequence[_currentStep].itemID == itemID;
    }

    /// <summary>스텝의 선행 phase가 완료됐는지 확인. gate가 설정되지 않았으면 항상 true.</summary>
    private bool IsGateSatisfied(int stepIndex)
    {
        var step = sequence[stepIndex];
        if (string.IsNullOrEmpty(step.requiredPhaseID)) return true;
        if (Managers.Quest == null) return true;

        var quest = Managers.Quest.GetActiveQuest(step.gateQuestID)
                 ?? Managers.Quest.GetCompletedQuest(step.gateQuestID);
        if (quest == null) return false;

        var phase = quest.GetPhase(step.gateObjectiveID, step.requiredPhaseID);
        return phase != null && phase.IsCompleted;
    }

    /// <summary>
    /// 현재 스텝의 아이템이면 spawnPoint에 배치하고 다음 스텝으로 진행.
    /// 성공 시 true 반환. InventoryUIPresenter는 true가 돌아올 때만 아이템을 차감해야 한다.
    /// </summary>
    public bool TryPlace(string itemID)
    {
        if (!Accepts(itemID)) return false;

        var step = sequence[_currentStep];
        if (step.prefab == null)
        {
            Debug.LogWarning($"[ItemUsableZone] step {_currentStep} ({step.itemID})의 prefab이 비어있습니다.");
            return false;
        }

        if (_spawnedInstance != null) Destroy(_spawnedInstance);

        Transform sp = spawnPoint != null ? spawnPoint : transform;
        _spawnedInstance = Instantiate(step.prefab, sp.position, sp.rotation);

        step.onPlaced?.Invoke();

        _currentStep++;

        if (IsComplete && glowIndicator != null)
            glowIndicator.SetActive(false);

        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = Current == this ? Color.green : new Color(0f, 1f, 0f, 0.25f);
        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
            Gizmos.DrawWireCube(transform.position + box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
    }
#endif
}
