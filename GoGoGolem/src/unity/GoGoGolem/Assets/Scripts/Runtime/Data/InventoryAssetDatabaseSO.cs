using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InventoryAssetDatabaseSO", menuName = "Inventory/Inventory Asset Database")]
public class InventoryAssetDatabaseSO : ScriptableObject
{
    [Header("아이템")]
    public List<InventoryAsset> itemAssets = new();

    [Header("스킬")]
    public List<InventoryAsset> skillAssets = new();

    [Header("보상")]
    public List<InventoryAsset> rewardAssets = new();

    private Dictionary<string, InventoryAsset> assetDict = new Dictionary<string, InventoryAsset>();
    private bool isInitialized = false;

    public void Initialize()
    {
        if (isInitialized) return;

        assetDict.Clear();

        RegisterList(itemAssets,   InventoryAssetType.Item);
        RegisterList(skillAssets,  InventoryAssetType.Skill);
        RegisterList(rewardAssets, InventoryAssetType.Reward);

        isInitialized = true;
    }

    private void RegisterList(List<InventoryAsset> list, InventoryAssetType expectedType)
    {
        foreach (var asset in list)
        {
            if (asset == null) continue;

            if (asset.type != expectedType)
                Debug.LogWarning($"[InventoryAssetDatabaseSO] {asset.itemID}의 type이 {expectedType}이 아닙니다 (실제: {asset.type}).");

            if (!assetDict.ContainsKey(asset.itemID))
                assetDict.Add(asset.itemID, asset);
            else
                Debug.LogWarning($"[InventoryAssetDatabaseSO] 중복된 assetID: {asset.itemID}");
        }
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
