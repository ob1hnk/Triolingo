using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; 

public class InventoryUIView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI inventoryText;
    [SerializeField] private ScrollRect scrollRect;

        public void Render(Dictionary<string, int> items)
        {
                if (inventoryText == null)
                {
                        Debug.LogWarning("InventoryUIView: inventoryText가 할당되지 않았습니다.");
                        return;
                }

                if (items.Count == 0)
                {
                        inventoryText.text = "Inventory is empty!";
                        return;
                }

                inventoryText.text = "=== Inventory ===\n";

                foreach (var item in items)
                {
                        inventoryText.text += $"{item.Key} x {item.Value}\n";
                }
        }

        public void SelectItem(int index)
        {
                Debug.Log($"아이템 선택: {index}");
        }

        public int MoveSelection(int currentIndex, Vector2 dir)
        {
                // dir.y > 0 : 위
                // dir.y < 0 : 아래
                // dir.x > 0 : 오른쪽
                // dir.x < 0 : 왼쪽

                int nextIndex = currentIndex;

                if (dir.y > 0) nextIndex--;
                if (dir.y < 0) nextIndex++;

                nextIndex = Mathf.Clamp(nextIndex, 0, GetItemCount() - 1);
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

}


