using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 다이얼로그 시스템 테스트용 런타임 버튼 패널.
/// 별도 Canvas에 붙여두고 Play 모드에서 대화를 수동으로 트리거한다.
/// </summary>
public class DialogueTestController : MonoBehaviour
{
    [Header("Dialogue IDs")]
    [SerializeField] private List<string> dialogueIDs = new List<string>
    {
        "DLG-001", "DLG-002",
        "DLG-005", "DLG-006", "DLG-007", "DLG-008", "DLG-009", "DLG-010", "DLG-011"
    };

    [Header("Font")]
    [SerializeField] private TMP_FontAsset buttonFont;

    [Header("Layout")]
    [SerializeField] private float buttonWidth = 200f;
    [SerializeField] private float buttonHeight = 40f;
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Vector2 origin = new Vector2(-16f, -16f); // top-right 기준

    private DialogueManager _dialogueManager;

    private void Start()
    {
        _dialogueManager = Object.FindFirstObjectByType<DialogueManager>();
        if (_dialogueManager == null)
        {
            Debug.LogError("[DialogueTest] DialogueManager를 찾을 수 없습니다.");
            return;
        }

        BuildButtons();
    }

    private void BuildButtons()
    {
        float y = origin.y;

        foreach (var id in dialogueIDs)
        {
            var capturedID = id;
            SpawnButton(capturedID, ref y, () =>
            {
                if (_dialogueManager.IsPlaying())
                {
                    Debug.LogWarning($"[DialogueTest] 이미 대화 진행 중. {capturedID} 무시됨.");
                    return;
                }
                Debug.Log($"[DialogueTest] 대화 시작: {capturedID}");
                _dialogueManager.StartDialogue(capturedID);
            });
        }

        y -= spacing * 2;

        SpawnButton("[중단] Stop Dialogue", ref y, () =>
        {
            _dialogueManager.SkipDialogue();
            Debug.Log("[DialogueTest] 대화 강제 중단.");
        }, new Color(0.6f, 0.15f, 0.15f, 0.9f));
    }

    private void SpawnButton(string label, ref float y, System.Action onClick, Color? bgColor = null)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(origin.x, y);
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

        go.GetComponent<Image>().color = bgColor ?? new Color(0.15f, 0.15f, 0.15f, 0.88f);
        go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 0f);
        textRt.offsetMax = new Vector2(-8f, 0f);

        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 13f;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.color = Color.white;
        if (buttonFont != null) tmp.font = buttonFont;

        y -= buttonHeight + spacing;
    }
}
