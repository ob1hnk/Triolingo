using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 퀘스트 시스템 테스트용 런타임 버튼 패널.
/// 별도 Canvas에 붙여두고 Play 모드에서 퀘스트 흐름을 수동으로 제어한다.
/// </summary>
public class QuestTestController : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] private string questID = "MQ-02";
    [SerializeField] private QuestDatabase questDatabase;

    [Header("Event Channels")]
    [SerializeField] private StringGameEvent requestStartQuestEvent;
    [SerializeField] private CompletePhaseGameEvent requestCompletePhaseEvent;

    [Header("Font")]
    [SerializeField] private TMP_FontAsset buttonFont;

    [Header("Layout")]
    [SerializeField] private float buttonWidth = 300f;
    [SerializeField] private float buttonHeight = 44f;
    [SerializeField] private float spacing = 6f;
    [SerializeField] private Vector2 origin = new Vector2(-16f, -16f); // top-right 기준

    private void Start()
    {
        questDatabase.Initialize();
        BuildButtons();
    }

    private void BuildButtons()
    {
        var questData = questDatabase.GetQuestData(questID);
        if (questData == null)
        {
            Debug.LogError($"[QuestTest] QuestData not found: {questID}");
            return;
        }

        float y = origin.y;

        // 퀘스트 시작
        SpawnButton($"[시작] {questID}: {questData.questName}", ref y, () =>
        {
            requestStartQuestEvent?.Raise(questID);
            Debug.Log($"[QuestTest] 퀘스트 시작 요청: {questID}");
        });

        // Objective별 완료 버튼
        foreach (var objective in questData.objectives)
        {
            var captured = objective;
            SpawnButton($"[완료] {objective.objectiveID}: {objective.description}", ref y, () =>
            {
                foreach (var phase in captured.phases)
                {
                    requestCompletePhaseEvent?.Raise(
                        new CompletePhaseRequest(questID, captured.objectiveID, phase.phaseID));
                    Debug.Log($"[QuestTest] Phase 완료: {captured.objectiveID} / {phase.phaseID}");
                }
            });
        }

        // 구분선 역할의 빈 간격
        y -= spacing * 2;

        // 퀘스트 리셋
        SpawnButton("[리셋] 세이브 삭제 후 씬 재시작", ref y, () =>
        {
            FindObjectOfType<QuestManager>()?.DeleteSaveFile();
            Debug.Log("[QuestTest] 세이브 삭제 완료. 씬 재시작.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }, new Color(0.6f, 0.15f, 0.15f, 0.9f));
    }

    private void SpawnButton(string label, ref float y, System.Action onClick,
        Color? bgColor = null)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(origin.x, y);
        rt.sizeDelta = new Vector2(buttonWidth, buttonHeight);

        go.GetComponent<Image>().color = bgColor ?? new Color(0.15f, 0.15f, 0.15f, 0.88f);
        go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());

        // 텍스트
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
