using System;
using System.Collections;
using UnityEngine;
using UI.Presenters;

/// <summary>
/// Room 씬의 낮/밤 상태를 중앙에서 관리
///
/// 상태 전환:
///   Day  →(편지 전송)→  NightWaiting  →(잠들기)→  NightReady
///
/// 의존성 (Inspector 연결 필수):
///   - letterWritePresenter : OnLetterSent 이벤트 구독
///   - letterReadPresenter  : SetTaskId() 호출
///   - letterDesk           : SetMode(Write/Read) 호출
///   - craneController      : FlyOut/FlyIn 호출, 완료 이벤트 구독
///   - bedInteraction       : OnSlept 이벤트 구독
///   - sunLight             : 낮/밤 조명 강도 제어
///   - nightView            : 밤 하늘 오브젝트 ON/OFF
///   - deskCollider         : 낮에만 활성
///   - bedCollider          : 밤(NightWaiting)에만 활성
/// </summary>
public class RoomStateManager : MonoBehaviour
{
    // ── 상태 ────────────────────────────────────────────────────
    public enum RoomState { Day, NightWaiting, NightReady }
    public RoomState CurrentState { get; private set; } = RoomState.Day;

    // ── Inspector ───────────────────────────────────────────────
    [Header("Letter System")]
    [SerializeField] private LetterWritePresenter letterWritePresenter;
    [SerializeField] private LetterReadPresenter  letterReadPresenter;
    [SerializeField] private LetterDesk           letterDesk;

    [Header("Crane Animation")]
    [SerializeField] private PaperCraneController craneController;

    [Header("Bed Interaction")]
    [SerializeField] private BedInteraction bedInteraction;

    [Header("Lighting")]
    [SerializeField] private Light    sunLight;
    [SerializeField] private float    dayLightIntensity  = 1.2f;
    [SerializeField] private float    fadeDuration       = 1.5f;
    [SerializeField] private Color nightAmbientColor = new Color(0.1176f, 0.1176f, 0.1961f);

    [Header("Night View")]
    [SerializeField] private GameObject nightView;

    [Header("Colliders")]
    [SerializeField] private Collider deskCollider;
    [SerializeField] private Collider bedCollider;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ── Private ─────────────────────────────────────────────────
    private string _savedTaskId;
    private Coroutine _fadeLightCoroutine;

    // ── Unity Lifecycle ─────────────────────────────────────────

    private void OnEnable()
    {
        if (letterWritePresenter != null)
            letterWritePresenter.OnLetterSent += HandleLetterSent;

        if (bedInteraction != null)
            bedInteraction.OnSlept += HandleSlept;

        if (craneController != null)
        {
            craneController.OnFlyOutComplete += HandleFlyOutComplete;
            craneController.OnFlyInComplete  += HandleFlyInComplete;
        }
    }

    private void OnDisable()
    {
        if (letterWritePresenter != null)
            letterWritePresenter.OnLetterSent -= HandleLetterSent;

        if (bedInteraction != null)
            bedInteraction.OnSlept -= HandleSlept;

        if (craneController != null)
        {
            craneController.OnFlyOutComplete -= HandleFlyOutComplete;
            craneController.OnFlyInComplete  -= HandleFlyInComplete;
        }
    }

    private void Start()
    {
        ApplyDayState();
    }

    // ── Event Handlers ──────────────────────────────────────────

    /// <summary>LetterWritePresenter가 편지를 성공적으로 전송하면 호출됨</summary>
    private void HandleLetterSent(string taskId)
    {
        DebugLog($"편지 전송 완료 (taskId={taskId}). 학 날아가기 시작.");
        _savedTaskId = taskId;

        if (letterReadPresenter != null)
            letterReadPresenter.SetTaskId(taskId);

        if (craneController != null)
            craneController.FlyOut();
        else
            HandleFlyOutComplete(); // 학 없으면 즉시 전환
    }

    /// <summary>학이 창문 밖으로 사라진 후 호출됨</summary>
    private void HandleFlyOutComplete()
    {
        DebugLog("학 날아가기 완료. 밤으로 전환.");
        CurrentState = RoomState.NightWaiting;

        // 조명 끄기 (Coroutine)
        if (_fadeLightCoroutine != null) StopCoroutine(_fadeLightCoroutine);
        _fadeLightCoroutine = StartCoroutine(FadeLightCoroutine(sunLight, 0f, fadeDuration, () =>
        {
            // 조명이 꺼진 후 GameObject 비활성화 + 밤 뷰 전환
            if (sunLight != null) sunLight.gameObject.SetActive(false);
            if (nightView != null) nightView.SetActive(true);
        }));

        // 책상 비활성화 (낮에만 쓰기 가능)
        if (deskCollider != null) deskCollider.enabled = false;

        // 침대 활성화 ("잠들기 (E)")
        if (bedCollider != null) bedCollider.enabled = true;
    }

    /// <summary>Bed 상호작용(잠들기) 후 호출됨</summary>
    private void HandleSlept()
    {
        DebugLog("잠들기. 학 귀환 시작.");

        // 침대 비활성화 (한 번만 가능)
        if (bedCollider != null) bedCollider.enabled = false;

        if (craneController != null)
            craneController.FlyIn();
        else
            HandleFlyInComplete(); // 학 없으면 즉시 전환
    }

    /// <summary>학이 책상에 착지한 후 호출됨</summary>
    private void HandleFlyInComplete()
    {
        DebugLog("학 귀환 완료. 편지 읽기 모드로 전환.");
        CurrentState = RoomState.NightReady;

        // 책상을 읽기 모드로 전환
        if (letterDesk != null)
            letterDesk.SetMode(LetterDesk.DeskMode.Read);

        // 책상 다시 활성화 ("편지 읽기 (E)")
        if (deskCollider != null) deskCollider.enabled = true;
    }

    // ── State Apply ─────────────────────────────────────────────

    private void ApplyDayState()
    {
        CurrentState = RoomState.Day;

        // 조명
        if (sunLight != null) sunLight.intensity = dayLightIntensity;

        // 밤 뷰 비활성
        if (nightView != null) nightView.SetActive(false);

        // 책상: 쓰기 모드, 활성
        if (letterDesk != null) letterDesk.SetMode(LetterDesk.DeskMode.Write);
        if (deskCollider != null) deskCollider.enabled = true;

        // 침대: 비활성 (낮에는 못 잠)
        if (bedCollider != null) bedCollider.enabled = false;

        DebugLog("낮 상태 적용 완료");
    }

    // ── Coroutines ───────────────────────────────────────────────

    private IEnumerator FadeLightCoroutine(Light light, float targetIntensity, float duration, Action onComplete = null)
    {
        if (light == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float startIntensity = light.intensity;
        Color startAmbient   = RenderSettings.ambientLight;
        Color targetAmbient  = targetIntensity > 0f ? startAmbient : nightAmbientColor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            light.intensity             = Mathf.Lerp(startIntensity, targetIntensity, t);
            RenderSettings.ambientLight = Color.Lerp(startAmbient, targetAmbient, t);
            yield return null;
        }

        light.intensity             = targetIntensity;
        RenderSettings.ambientLight = targetAmbient;
        onComplete?.Invoke();
    }

    // ── Debug ────────────────────────────────────────────────────

    private void DebugLog(string msg)
    {
        if (enableDebugLogs)
            Debug.Log($"[RoomStateManager] {msg}");
    }
}
