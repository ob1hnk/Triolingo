using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// 인트로 씬 진행을 제어한다.
///
/// 흐름:
///   [패널 1] 온보딩 (조명/카메라·음성 안내) → 아무 키 dismiss
///   → [패널 2] 키 가이드 (WASD·E 설명) → 아무 키 dismiss
///   → DLG_INTRO 다이얼로그 실행 (내러티브)
///   → <<jump DLG_INTRO_NAMES>> (이름 입력 구간, skip 버튼 비활성)
///   → 이름 입력 완료 후 <<show_skip>> → skip 가능
///   → nextSceneName 씬 전환 및 Intro_Watched 저장
///
/// Skip 버튼:
///   - 대화 시작(StartIntroDialogue) 시점에 활성화
///   - Yarn <<hide_skip>> / <<show_skip>> 커맨드로 가시성 제어
///   - 이름 미입력 상태에서 skip → DLG_INTRO_NAMES 노드로 점프
///   - 이름 입력 완료 후 skip → 즉시 씬 전환 + Intro_Watched 저장
/// </summary>
public class IntroSceneController : MonoBehaviour
{
    private const string PrefKeyIntroWatched = "Intro_Watched";

    public static bool HasWatched => PlayerPrefs.GetInt(PrefKeyIntroWatched, 0) == 1;

    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private string nextSceneName = "World";

    [Header("온보딩 패널 (인트로 시작 전 — 조명/카메라·음성 안내)")]
    [SerializeField] private DismissablePanelController onboardingPanel;

    [Header("키 가이드 패널 (온보딩 이후 — WASD·E 조작 안내)")]
    [SerializeField] private DismissablePanelController keyGuidePanel;

    [Header("대화 UI — 온보딩 중 숨김 처리 (SetActive 금지 → alpha 사용)")]
    [Tooltip("DialogueCanvas 안의 DialoguePanel CanvasGroup. 온보딩 중 alpha=0으로 숨긴다.")]
    [SerializeField] private CanvasGroup dialoguePanelCanvasGroup;

    [Header("스킵 — Yarn <<hide_skip>> / <<show_skip>>으로 가시성 제어")]
    [SerializeField] private Button skipButton;
    [SerializeField] private IntroImagePresenter imagePresenter;
    [SerializeField] private NameInputPresenter nameInputPresenter;
    [SerializeField] private GolemNameInputPresenter golemNameInputPresenter;

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

        // Yarn 커맨드로 skip 버튼 가시성 제어
        dialogueRunner.AddCommandHandler("hide_skip", () =>
        {
            if (skipButton != null) skipButton.gameObject.SetActive(false);
        });
        dialogueRunner.AddCommandHandler("show_skip", () =>
        {
            if (skipButton != null) skipButton.gameObject.SetActive(true);
        });

        // Skip 버튼은 대화 시작(StartIntroDialogue) 전까지 비활성
        if (skipButton != null)
        {
            skipButton.gameObject.SetActive(false);
            skipButton.onClick.AddListener(OnSkipClicked);
        }

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
        if (skipButton != null)
            skipButton.onClick.RemoveListener(OnSkipClicked);
    }

    // ──────────────────────────────────────────────────────────────
    // 스킵
    // ──────────────────────────────────────────────────────────────

    private void OnSkipClicked()
    {
        if (skipButton != null)
            skipButton.gameObject.SetActive(false);

        // 안전망: 이름 입력 패널이 열려 있으면 닫기 (정상적으로는 <<hide_skip>>으로 방지됨)
        nameInputPresenter?.ForceCancel();
        golemNameInputPresenter?.ForceCancel();

        imagePresenter?.ForceHide();

        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
        {
            dialogueRunner.onDialogueComplete.RemoveListener(OnDialogueComplete);
            dialogueRunner.Stop();
        }

        // 이름 미입력 → 이름 입력 노드로 점프해서 Yarn 대화 재개
        // 이름 완료  → Intro_Watched 저장 후 즉시 씬 전환
        if (!GameManager.Instance.HasPlayerName || !GameManager.Instance.HasGolemName)
            StartCoroutine(JumpToNode("DLG_INTRO_NAMES"));
        else
        {
            PlayerPrefs.SetInt(PrefKeyIntroWatched, 1);
            PlayerPrefs.Save();
            LoadNextScene();
        }
    }

    private IEnumerator JumpToNode(string nodeName)
    {
        yield return null; // Stop() 정리 후 한 프레임 대기

        // 대화 UI 복원 (온보딩 중 skip된 경우 alpha=0일 수 있음)
        if (dialoguePanelCanvasGroup != null)
        {
            dialoguePanelCanvasGroup.alpha = 1f;
            dialoguePanelCanvasGroup.interactable = true;
            dialoguePanelCanvasGroup.blocksRaycasts = true;
        }

        dialogueRunner.onDialogueComplete.AddListener(OnDialogueComplete);
        dialogueRunner.StartDialogue(nodeName);
    }

    // ──────────────────────────────────────────────────────────────
    // 온보딩 패널 → 키 가이드 패널 → 대화
    // ──────────────────────────────────────────────────────────────

    private void OnOnboardingDismissed()
    {
        onboardingPanel.OnDismissed -= OnOnboardingDismissed;

        // 키 가이드가 있으면 온보딩 다음에 표시, 없으면 바로 대화 시작
        if (keyGuidePanel != null)
        {
            keyGuidePanel.OnDismissed += OnKeyGuideDismissed;
            keyGuidePanel.Show();
        }
        else
        {
            StartIntroDialogue();
        }
    }

    private void OnKeyGuideDismissed()
    {
        keyGuidePanel.OnDismissed -= OnKeyGuideDismissed;
        StartIntroDialogue();
    }

    private void StartIntroDialogue()
    {
        // 대화 UI 표시 (온보딩 중 숨겼던 것 복원)
        if (dialoguePanelCanvasGroup != null)
        {
            dialoguePanelCanvasGroup.alpha = 1f;
            dialoguePanelCanvasGroup.interactable = true;
            dialoguePanelCanvasGroup.blocksRaycasts = true;
        }

        // 대화 시작 시 skip 버튼 활성화
        if (skipButton != null)
            skipButton.gameObject.SetActive(true);

        dialogueRunner.StartDialogue("DLG_INTRO");
        Debug.Log($"[IntroSceneController] StartDialogue 호출 후 IsDialogueRunning: {dialogueRunner.IsDialogueRunning}");
    }

    // ──────────────────────────────────────────────────────────────
    // 대화 완료 → Intro_Watched 저장 → 씬 전환
    // ──────────────────────────────────────────────────────────────

    private void OnDialogueComplete()
    {
        // 이름이 모두 입력된 경우에만 인트로 완료 처리
        // (씬 언로드/앱 종료 시 DialogueRunner가 이벤트를 발생시키는 것에 대한 방어)
        if (!GameManager.Instance.HasPlayerName || !GameManager.Instance.HasGolemName)
            return;

        PlayerPrefs.SetInt(PrefKeyIntroWatched, 1);
        PlayerPrefs.Save();
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        SceneManager.LoadScene(nextSceneName);
    }
}
