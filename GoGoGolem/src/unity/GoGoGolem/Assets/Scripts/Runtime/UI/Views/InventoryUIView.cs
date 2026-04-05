using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : MonoBehaviour
{
    [Header("Slot Settings")]
    [SerializeField] private Transform itemSlotContainer;
    [SerializeField] private Transform skillSlotContainer;
    [SerializeField] private InventorySlot slotPrefab;

    [Header("Grid Settings")]
    [SerializeField] private int columns = 4;
    [SerializeField] private int initialSlotCount = 4;

    [Header("Empty State")]
    [SerializeField] private GameObject noItemText;
    [SerializeField] private GameObject noSkillText;

    [Header("Item Info Panel")]
    [SerializeField] private ItemInfoPanel itemInfoPanel;

    private List<InventorySlot> itemSlots = new();
    private List<InventorySlot> skillSlots = new();
    private Dictionary<int, string> indexToItemID = new();

    private int TotalSlotCount => itemSlots.Count + skillSlots.Count;

    private InventorySlot GetSlotAt(int index)
    {
        if (index < itemSlots.Count) return itemSlots[index];
        int skillIndex = index - itemSlots.Count;
        return skillIndex < skillSlots.Count ? skillSlots[skillIndex] : null;
    }

    public System.Action<int> OnSlotClicked;

    private void Start()
    {
        if (Application.isPlaying)
        {
            if (itemInfoPanel == null)
                itemInfoPanel = GetComponentInChildren<ItemInfoPanel>(true);
            if (itemInfoPanel == null)
                itemInfoPanel = FindAnyObjectByType<ItemInfoPanel>(FindObjectsInactive.Include);
            if (itemInfoPanel == null)
                Debug.LogError("[InventoryUIView] ItemInfoPanel을 찾을 수 없습니다. Inspector에서 연결해주세요.");

            InitializeSlots();
        }
    }

#if UNITY_EDITOR
    private void OnDestroy()
    {
        if (!Application.isPlaying && itemSlotContainer != null)
        {
            CleanupEditorSlots();
        }
    }

    private void CleanupEditorSlots()
    {
        int childCount = itemSlotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = itemSlotContainer.GetChild(i);
            if (child.gameObject.hideFlags == HideFlags.DontSave)
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
#endif

    private void InitializeSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUIView] slotPrefab이 할당되지 않았습니다!");
            return;
        }

        if (itemSlotContainer == null)
        {
            Debug.LogError("[InventoryUIView] itemSlotContainer가 할당되지 않았습니다!");
            return;
        }

        ClearAllSlotObjects();
    }

    private InventorySlot CreateSlot(Transform container, List<InventorySlot> slotList, string namePrefix, int globalIndex)
    {
        InventorySlot newSlot = Instantiate(slotPrefab, container);
        newSlot.gameObject.name = $"{namePrefix}_{slotList.Count}";
        newSlot.gameObject.hideFlags = HideFlags.DontSaveInEditor;

        newSlot.SetIndex(globalIndex);
        newSlot.OnSlotClicked += HandleSlotClick;
        newSlot.ShowAsEmpty();
        slotList.Add(newSlot);
        return newSlot;
    }

    private void ClearAllSlotObjects()
    {
        ClearSlotList(itemSlots);
        ClearSlotList(skillSlots);
        ClearContainer(itemSlotContainer);
        ClearContainer(skillSlotContainer);
    }

    private void ClearSlotList(List<InventorySlot> slotList)
    {
        foreach (var slot in slotList)
            if (slot != null) slot.OnSlotClicked -= HandleSlotClick;
        slotList.Clear();
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        int childCount = container.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private void RenderSection(
        Dictionary<string, int> data,
        Transform container,
        List<InventorySlot> slotList,
        string namePrefix,
        GameObject emptyText,
        int indexOffset)
    {
        ClearSlotList(slotList);
        ClearContainer(container);

        bool isEmpty = data == null || data.Count == 0;
        if (emptyText != null) emptyText.SetActive(isEmpty);

        if (isEmpty) return;

        int requiredSlots = Mathf.Max(initialSlotCount, data.Count);
        for (int i = 0; i < requiredSlots; i++)
        {
            CreateSlot(container, slotList, namePrefix, indexOffset + i);
        }

        var itemDB = Managers.Inventory?.ItemDB;
        int index = 0;
        foreach (var kv in data)
        {
            if (index >= slotList.Count) break;

            var itemData = itemDB?.GetItem(kv.Key);
            slotList[index].SetItem(itemData?.icon);
            indexToItemID[indexOffset + index] = kv.Key;
            index++;
        }
    }

    public void RenderItems(Dictionary<string, int> items)
    {
        indexToItemID.Clear();
        RenderSection(items, itemSlotContainer, itemSlots, "Slot", noItemText, 0);
    }

    public void RenderSkills(Dictionary<string, int> skills)
    {
        RenderSection(skills, skillSlotContainer, skillSlots, "SkillSlot", noSkillText, itemSlots.Count);
    }

    public void SelectItem(int index)
    {
        ClearSelection();

        if (IsValidIndex(index))
        {
            var slot = GetSlotAt(index);
            if (slot != null) slot.SetSelected(true);
        }
    }

    public int MoveSelection(int currentIndex, Vector2 direction)
    {
        int nextIndex = CalculateNextIndex(currentIndex, direction);
        SelectItem(nextIndex);
        return nextIndex;
    }

    public void ClearSelection()
    {
        foreach (var slot in itemSlots)
            slot.SetSelected(false);
        foreach (var slot in skillSlots)
            slot.SetSelected(false);

        if (itemInfoPanel != null)
            itemInfoPanel.ShowEmpty();
    }

    public void ShowItemInfo(int index)
    {
        if (!IsValidIndex(index))
        {
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }

        if (!indexToItemID.TryGetValue(index, out string itemID))
        {
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }

        ItemData itemData = Managers.Inventory?.ItemDB?.GetItem(itemID);
        if (itemData == null)
        {
            if (itemInfoPanel != null)
                itemInfoPanel.ShowEmpty();
            return;
        }

        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowItemInfo(itemData);
        }
    }

    public bool HasItemAtIndex(int index)
    {
        return indexToItemID.ContainsKey(index);
    }


    public void UpdatePointer(Vector2 mousePos)
    {
    }

    private void HandleSlotClick(int slotIndex)
    {
        OnSlotClicked?.Invoke(slotIndex);
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < TotalSlotCount;
    }

    private int CalculateNextIndex(int currentIndex, Vector2 direction)
    {
        int row = currentIndex / columns;
        int col = currentIndex % columns;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            col += direction.x > 0 ? 1 : -1;
        else
            row += direction.y > 0 ? -1 : 1;

        col = Mathf.Clamp(col, 0, columns - 1);

        int maxRow = (TotalSlotCount - 1) / columns;
        row = Mathf.Clamp(row, 0, maxRow);

        int nextIndex = row * columns + col;
        return IsValidIndex(nextIndex) ? nextIndex : currentIndex;
    }

}
