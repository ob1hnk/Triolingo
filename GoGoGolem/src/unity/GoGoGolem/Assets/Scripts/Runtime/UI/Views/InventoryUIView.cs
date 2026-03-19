using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIView : MonoBehaviour
{
    [Header("Slot Settings")]
    [SerializeField] private Transform slotContainer;
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

    private List<InventorySlot> slots = new();
    private List<InventorySlot> skillSlots = new();
    private Dictionary<int, string> indexToItemID = new();

    private int TotalSlotCount => slots.Count + skillSlots.Count;

    private InventorySlot GetSlotAt(int index)
    {
        if (index < slots.Count) return slots[index];
        int skillIndex = index - slots.Count;
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
        if (!Application.isPlaying && slotContainer != null)
        {
            CleanupEditorSlots();
        }
    }

    private void CleanupEditorSlots()
    {
        int childCount = slotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);
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

        if (slotContainer == null)
        {
            Debug.LogError("[InventoryUIView] slotContainer가 할당되지 않았습니다!");
            return;
        }

        ClearAllSlotObjects();

        for (int i = 0; i < initialSlotCount; i++)
        {
            CreateSlot(i);
        }

        RegisterSkillSlots();
    }

    private void CreateSlot(int index)
    {
        InventorySlot newSlot = Instantiate(slotPrefab, slotContainer);
        newSlot.gameObject.name = $"Slot_{index}";
        newSlot.gameObject.hideFlags = HideFlags.DontSaveInEditor;

        newSlot.SetIndex(index);
        newSlot.OnSlotClicked += HandleSlotClick;
        newSlot.ShowAsEmpty();
        slots.Add(newSlot);
    }

    private void RegisterSkillSlots()
    {
        foreach (var slot in skillSlots)
            if (slot != null) slot.OnSlotClicked -= HandleSlotClick;
        skillSlots.Clear();

        if (skillSlotContainer == null) return;

        int startIndex = slots.Count;
        for (int i = 0; i < skillSlotContainer.childCount; i++)
        {
            var slot = skillSlotContainer.GetChild(i).GetComponent<InventorySlot>();
            if (slot == null) continue;
            slot.SetIndex(startIndex + i);
            slot.OnSlotClicked += HandleSlotClick;
            skillSlots.Add(slot);
        }
    }

    private void ClearAllSlotObjects()
    {
        foreach (var slot in slots)
            if (slot != null) slot.OnSlotClicked -= HandleSlotClick;

        foreach (var slot in skillSlots)
            if (slot != null) slot.OnSlotClicked -= HandleSlotClick;
        skillSlots.Clear();

        int childCount = slotContainer.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = slotContainer.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
        slots.Clear();
    }

    public void Render(Dictionary<string, int> items)
    {
        if (slots.Count == 0)
        {
            InitializeSlots();
        }

        indexToItemID.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].ShowAsEmpty();
        }

        bool isEmpty = items == null || items.Count == 0;
        foreach (var slot in slots)
            slot.gameObject.SetActive(!isEmpty);
        if (noItemText != null) noItemText.SetActive(isEmpty);

        if (isEmpty) return;

        int requiredSlots = items.Count;
        if (requiredSlots > slots.Count)
        {
            int slotsToAdd = requiredSlots - slots.Count;
            for (int i = 0; i < slotsToAdd; i++)
            {
                CreateSlot(slots.Count);
            }
        }

        var itemDB = Managers.Inventory?.ItemDB;
        int index = 0;
        foreach (var item in items)
        {
            if (index >= slots.Count) break;

            var data = itemDB?.GetItem(item.Key);
            slots[index].SetItem(data?.icon);
            indexToItemID[index] = item.Key;
            index++;
        }
    }

    public void RenderSkills(Dictionary<string, int> skills)
    {
        bool isEmpty = skills == null || skills.Count == 0;
        foreach (var slot in skillSlots)
            slot.gameObject.SetActive(!isEmpty);
        if (noSkillText != null) noSkillText.SetActive(isEmpty);
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
        foreach (var slot in slots)
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
