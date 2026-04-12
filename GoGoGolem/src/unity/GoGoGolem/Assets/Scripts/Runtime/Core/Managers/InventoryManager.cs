using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private InventoryAssetDatabaseSO itemCatalogue;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestAcquireItemEvent;

    public InventoryLogic Logic { get; private set; }
    public InventoryAssetDatabaseSO AssetDB { get; private set; }

    private InventorySaveSystem _saveSystem;

    public void Init()
    {
        if (itemCatalogue == null)
            Debug.LogError("[InventoryManager] itemCatalogue이 Inspector에 연결되지 않았습니다.");
        else
        {
            AssetDB = itemCatalogue;
            AssetDB.Initialize();
        }

        _saveSystem = new InventorySaveSystem();
        Logic = new InventoryLogic();

        // 저장 파일에서 인벤토리 복원
        var data = _saveSystem.Load();
        foreach (var entry in data.items)
            Logic.AddItem(entry.itemID, entry.count);

        // 저장은 OnApplicationQuit에서만 수행 (씬 전환 중 파일 I/O 크래시 방지)
    }

    private void OnEnable()
    {
        if (requestAcquireItemEvent != null)
            requestAcquireItemEvent.Register(AcquireItem);
    }

    private void OnDisable()
    {
        if (requestAcquireItemEvent != null)
            requestAcquireItemEvent.Unregister(AcquireItem);
    }

    private void OnApplicationQuit()
    {
        SaveInventory();
    }

    public void AcquireItem(string itemID)
    {
        var asset = AssetDB.GetAsset(itemID);
        if (asset == null)
        {
            Debug.LogWarning($"DB에 존재하지 않는 아이템 ID: {itemID}");
            return;
        }
        Logic.AddItem(itemID);
    }

    /// <summary>
    /// 아이템 보유 여부를 퀘스트 상태와 대조해 보정.
    /// shouldHave=true이고 없으면 추가, false이고 있으면 제거.
    /// </summary>
    public void ReconcileItem(string itemID, bool shouldHave)
    {
        bool has = Logic.HasItem(itemID);
        if (shouldHave && !has)
        {
            if (AssetDB.GetAsset(itemID) == null)
            {
                Debug.LogWarning($"[InventoryManager] Reconcile: DB에 없는 ID={itemID}, 스킵");
                return;
            }
            Logic.AddItem(itemID);
            Debug.Log($"[InventoryManager] Reconcile: {itemID} 추가 (퀘스트 상태 기준 보유해야 함)");
        }
        else if (!shouldHave && has)
        {
            Logic.RemoveItem(itemID, Logic.GetAllItems()[itemID]);
            Debug.Log($"[InventoryManager] Reconcile: {itemID} 제거 (퀘스트 상태 기준 없어야 함)");
        }
    }

    private void SaveInventory()
    {
        _saveSystem?.Save(Logic.GetAllItems());
    }

    // ── Debug ────────────────────────────────────────

    [ContextMenu("Clear All Items (Debug)")]
    public void ClearAll()
    {
        Logic?.Clear();
        Debug.Log("[InventoryManager] 인벤토리 초기화 (Debug)");
    }

    [ContextMenu("Delete Save File (Debug)")]
    public void DeleteSaveFile()
    {
        _saveSystem?.Delete();
        Logic?.Clear();
        Debug.Log("[InventoryManager] 저장 파일 삭제 + 인벤토리 초기화 (Debug)");
    }
}
