using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryAssetDatabaseSO", menuName = "Inventory/Inventory Asset Database")]
public class InventoryAssetDatabaseSO : ScriptableObject
{
    public List<InventoryAsset> items = new List<InventoryAsset>();

    private Dictionary<string, InventoryAsset> assetDict = new Dictionary<string, InventoryAsset>();
    private bool isInitialized = false;

    public void Initialize()
    {
        if (isInitialized) return;

        assetDict.Clear();

        foreach (var asset in items)
        {
            if (asset == null) continue;

            if (!assetDict.ContainsKey(asset.itemID))
                assetDict.Add(asset.itemID, asset);
            else
                Debug.LogWarning($"[InventoryAssetDatabaseSO] 중복된 itemID: {asset.itemID}");
        }

        isInitialized = true;
    }

    public InventoryAsset GetAsset(string assetID)
    {
        if (!isInitialized)
        {
            Debug.LogWarning("[InventoryAssetDatabaseSO] Initialize()를 먼저 호출해야 합니다.");
            Initialize();
        }

        return assetDict.TryGetValue(assetID, out var data) ? data : null;
    }

    private void OnEnable()
    {
        isInitialized = false;
    }
}
