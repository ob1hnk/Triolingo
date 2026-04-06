using UnityEngine;

/// <summary>
/// 인벤토리 테스트용 화면 버튼.
///
/// 사용법:
///   1. 씬의 아무 GameObject에 이 컴포넌트 추가
///   2. Inspector에서 acquireItemEvent에 RequestAcquireItem StringGameEvent SO 연결
///      (InventoryManager가 구독하는 것과 동일한 에셋)
///   3. Play 시 좌상단에 3개 버튼 표시
/// </summary>
public class InventoryTester : MonoBehaviour
{
    [Header("Event Channel")]
    [Tooltip("InventoryManager가 구독하는 RequestAcquireItem 이벤트")]
    [SerializeField] private StringGameEvent acquireItemEvent;

    [Header("Test Item IDs")]
    [SerializeField] private string itemId1 = "ITEM-001";
    [SerializeField] private string itemId2 = "ITEM-002";

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 220, 160));
        GUI.skin.button.fontSize = 14;

        if (GUILayout.Button($"획득: {itemId1}", GUILayout.Height(36)))
            RaiseAcquire(itemId1);

        if (GUILayout.Button($"획득: {itemId2}", GUILayout.Height(36)))
            RaiseAcquire(itemId2);

        if (GUILayout.Button("인벤토리 모두 비우기", GUILayout.Height(36)))
            ClearInventory();

        GUILayout.EndArea();
    }

    private void RaiseAcquire(string itemId)
    {
        if (acquireItemEvent == null)
        {
            Debug.LogError("[InventoryTester] acquireItemEvent가 연결되지 않았습니다.");
            return;
        }
        acquireItemEvent.Raise(itemId);
        Debug.Log($"[InventoryTester] AcquireItem 이벤트 발생: {itemId}");
    }

    private void ClearInventory()
    {
        if (Managers.Inventory == null || Managers.Inventory.Logic == null)
        {
            Debug.LogError("[InventoryTester] InventoryManager가 초기화되지 않았습니다.");
            return;
        }
        Managers.Inventory.Logic.Clear();
        Debug.Log("[InventoryTester] 인벤토리 비움");
    }
}