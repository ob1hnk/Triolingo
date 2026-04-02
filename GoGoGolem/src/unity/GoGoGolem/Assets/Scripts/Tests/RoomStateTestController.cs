using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Room 상태 테스트용 런타임 버튼 패널.
/// Canvas에 붙여두고 Play 모드에서 상태를 수동으로 전환한다.
/// </summary>
public class RoomStateTestController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomStateManager roomStateManager;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset buttonFont;

    [Header("Layout")]
    [SerializeField] private float buttonWidth = 200f;
    [SerializeField] private float buttonHeight = 40f;
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Vector2 origin = new Vector2(-16f, -16f);

    private void Start()
    {
        if (roomStateManager == null)
        {
            roomStateManager = Object.FindFirstObjectByType<RoomStateManager>();
            if (roomStateManager == null)
            {
                Debug.LogError("[RoomStateTest] RoomStateManager를 찾을 수 없습니다.");
                return;
            }
        }

        BuildButtons();
    }

    private void BuildButtons()
    {
        float y = origin.y;

        SpawnButton("BeforeLetter", ref y, () =>
        {
            Debug.Log("[RoomStateTest] → BeforeLetter");
            roomStateManager.SetState(RoomStateManager.RoomState.BeforeLetter);
        });

        SpawnButton("AfterLetter", ref y, () =>
        {
            Debug.Log("[RoomStateTest] → AfterLetter");
            roomStateManager.SetState(RoomStateManager.RoomState.AfterLetter);
        });

        SpawnButton("Morning", ref y, () =>
        {
            Debug.Log("[RoomStateTest] → Morning");
            roomStateManager.SetState(RoomStateManager.RoomState.Morning);
        });
    }

    private void SpawnButton(string label, ref float y, System.Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(origin.x, y);
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

        go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.88f);
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