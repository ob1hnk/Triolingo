using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; 

public class InventoryUIView : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform slotContainer;
    [SerializeField] private InventorySlot slotPrefab;

    private List<InventorySlot> slots = new();
    private int columns = 4;

        public void Render(Dictionary<string, int> items)
        {
                int index = 0;
                
                foreach (var slot in slots)
                {
                        slot.Clear();
                }

                foreach (var item in items)
                {
                        if (index >= slots.Count) break;

                        slots[index].SetItem(item.Key, item.Value);
                        index++;
                }
        }

        public void SelectItem(int index)
        {
                ClearSelection();

                if (IsValidIndex(index))
                {
                        slots[index].SetSelected(true);
                }
        }

        /// <summary>
        /// 방향 입력에 따른 다음 인덱스 계산 + 선택 처리
        /// 결과 인덱스를 Presenter에게 반환
        /// </summary>
        public int MoveSelection(int currentIndex, Vector2 dir)
        {
                int nextIndex = CalculateNextIndex(currentIndex, dir);
                SelectItem(nextIndex);
                return nextIndex;
        }



        public int GetItemIndexUnderMouse()
        {
                // Raycast or EventSystem 기반
                // 아직 구현 안 해도 OK
                return -1;
        }

        public void Scroll(float value)
        {
                if (scrollRect == null) return;

                scrollRect.verticalNormalizedPosition += value * 0.1f;
        }

        public void UseSelectedItem(int index)
        {
                Debug.Log($"아이템 사용 / 장착: {index}");
        }

        public void UpdatePointer(Vector2 mousePos)
        {
                // 필요하면 hover 처리
        }

        private int GetItemCount()
        {
                return 20; // 예시
        }

    /* =========================
     * 내부 구현
     * ========================= */
        private void ClearSelection()
        {
                foreach (var slot in slots)
                {
                        slot.SetSelected(false);
                }
        }

        private bool IsValidIndex(int index)
        {
                return index >= 0 && index < slots.Count;
        }

        private int CalculateNextIndex(int currentIndex, Vector2 dir)
        {
                int row = currentIndex / columns;
                int col = currentIndex % columns;

                if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                {
                col += dir.x > 0 ? 1 : -1;
                }
                else
                {
                row += dir.y > 0 ? -1 : 1;
                }

                int nextIndex = row * columns + col;

                if (!IsValidIndex(nextIndex))
                return currentIndex;

                return nextIndex;
        }


}


