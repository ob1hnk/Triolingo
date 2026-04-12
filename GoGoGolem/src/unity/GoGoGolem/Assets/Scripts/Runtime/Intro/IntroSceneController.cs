using UnityEngine;
using UnityEngine.SceneManagement;
using Yarn.Unity;

/// <summary>
/// 인트로 씬 진행을 제어한다.
///
/// 흐름:
///   [패널 1] 온보딩 (조명/카메라·음성 안내) → 아무 키 dismiss
///   → DLG_INTRO 다이얼로그 실행
///   → [패널 2] 키 가이드 (WASD·E 설명) → 아무 키 dismiss
///   → nextSceneName 씬 전환
///
/// 두 패널 모두 Inspector에서 연결하지 않으면 해당 단계를 건너뛰고 다음으로 진행한다.
/// </summary>
public class IntroSceneController : MonoBehaviour
{
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string nextSceneName = "World";

    [Header("온보딩 패널 (인트로 시작 전 — 조명/카메라·음성 안내)")]
    [SerializeField] private DismissablePanelController onboardingPanel;

    [Header("키 가이드 패널 (인트로 대화 완료 후 — WASD·E 조작 안내)")]
    [SerializeField] private DismissablePanelController keyGuidePanel;

    [Header("대화 UI — 온보딩 중 숨김 처리 (SetActive 금지 → alpha 사용)")]
    [Tooltip("DialogueCanvas 안의 DialoguePanel CanvasGroup. 온보딩 중 alpha=0으로 숨긴다.")]
    [SerializeField] private CanvasGroup dialoguePanelCanvasGroup;

    private void Start()
    {
        Debug.Log("[IntroSceneController] Start() 호출됨");

        if (dialogueRunner == null)
        {
            Debug.LogError("[IntroSceneController] DialogueRunner가 연결되지 않았습니다!");
            return;
        }

        var presenters = dialogueRunner.GetComponents<Yarn.Unity.DialoguePresenterBase>();
        Debug.Log($"[IntroSceneController] DialogueRunner 연결 확인. Presenters: {presenters.Length}개");
        foreach (var p in presenters)
            Debug.Log($"  - {p.GetType().Name} (enabled={p.enabled})");

        Debug.Log($"[IntroSceneController] IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);

        // 온보딩 패널이 연결되어 있으면 먼저 표시, 없으면 바로 대화 시작
        if (onboardingPanel != null)
        {
            // 온보딩 중 대화 UI 숨김 (SetActive 금지 — TMP m_canvas 손상)
            if (dialoguePanelCanvasGroup != null)
            {
                dialoguePanelCanvasGroup.alpha = 0f;
                dialoguePanelCanvasGroup.interactable = false;
                dialoguePanelCanvasGroup.blocksRaycasts = false;
            }

            onboardingPanel.OnDismissed += OnOnboardingDismissed;
            onboardingPanel.Show();
        }
        else
        {
            StartIntroDialogue();
        }
    }

    private void OnDestroy()
    {
        if (dialogueRunner != null)
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
        if (onboardingPanel != null)
            onboardingPanel.OnDismissed -= OnOnboardingDismissed;
        if (keyGuidePanel != null)
            keyGuidePanel.OnDismissed -= OnKeyGuideDismissed;
    }

    // ──────────────────────────────────────────────────────────────
    // 온보딩 패널 → 대화
    // ──────────────────────────────────────────────────────────────

    private void OnOnboardingDismissed()
    {
        onboardingPanel.OnDismissed -= OnOnboardingDismissed;
        StartIntroDialogue();
    }

    private void StartIntroDialogue()
    {
        // 대화 UI 다시 표시 (LinePresenter가 fade-in 처리)
        if (dialoguePanelCanvasGroup != null)
        {
            dialoguePanelCanvasGroup.alpha = 1f;
            dialoguePanelCanvasGroup.interactable = true;
            dialoguePanelCanvasGroup.blocksRaycasts = true;
        }

        dialogueRunner.StartDialogue("DLG_INTRO");
        Debug.Log($"[IntroSceneController] StartDialogue 호출 후 IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
    }

    // ──────────────────────────────────────────────────────────────
    // 대화 완료 → 키 가이드 패널 → 씬 전환
    // ──────────────────────────────────────────────────────────────

    private void OnDialogueComplete()
    {
        // 키 가이드 패널이 연결되어 있으면 표시, 없으면 바로 씬 전환
        if (keyGuidePanel != null)
        {
            keyGuidePanel.OnDismissed += OnKeyGuideDismissed;
            keyGuidePanel.Show();
        }
        else
        {
            LoadNextScene();
        }
    }

    private void OnKeyGuideDismissed()
    {
        keyGuidePanel.OnDismissed -= OnKeyGuideDismissed;
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
