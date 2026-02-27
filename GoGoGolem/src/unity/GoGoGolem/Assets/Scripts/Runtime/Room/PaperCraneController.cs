using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// 종이학 비행 애니메이션 컨트롤러
///
/// Timeline 사용 시:
///   - flyOutDirector : CraneFlyOut Timeline 재생 → 완료 시 OnFlyOutComplete
///   - flyInDirector  : CraneFlyIn  Timeline 재생 → 완료 시 OnFlyInComplete
///   - CraneRoot에 Animation Track (위치 이동) + Activation Track (PaperCrane 표시/숨김)
///
/// Timeline 미연결 시 Coroutine Fallback 자동 사용:
///   - Inspector에서 deskPoint / windowPoint / outsidePoint 연결 필요
/// </summary>
public class PaperCraneController : MonoBehaviour
{
    [Header("비행 경로 포인트 (Coroutine Fallback용)")]
    [SerializeField] private Transform deskPoint;
    [SerializeField] private Transform windowPoint;
    [SerializeField] private Transform outsidePoint;

    [Header("비행 설정 (Coroutine Fallback용)")]
    [SerializeField] private float flyDuration = 2.5f;
    [SerializeField] private float arcHeight   = 2.0f;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector flyOutDirector;
    [SerializeField] private PlayableDirector flyInDirector;
    [SerializeField] private Animator craneRootAnimator; // CraneRoot의 Animator

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // 외부 시스템(RoomStateManager)이 구독
    public event Action OnFlyOutComplete;
    public event Action OnFlyInComplete;

    private Coroutine _currentFlight;

    private void Start()
    {
        // 항상 숨긴 상태로 시작, FlyOut/FlyIn 호출 시 코드에서 직접 켜고 끔
        gameObject.SetActive(false);
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>편지 전송 시 호출: 책상 → 창문 → 창문 밖으로 날아감</summary>
    public void FlyOut()
    {
        if (flyOutDirector != null)
        {
            if (_currentFlight != null) StopCoroutine(_currentFlight);

            if (craneRootAnimator != null) craneRootAnimator.enabled = true; // Animator 재활성화
            gameObject.SetActive(true); // 학 표시

            flyOutDirector.stopped -= OnFlyOutDirectorStopped;
            flyOutDirector.stopped += OnFlyOutDirectorStopped;
            flyOutDirector.Play();

            DebugLog("Timeline으로 날아가기 시작");
        }
        else
        {
            FlyOutCoroutineFallback();
        }
    }

    /// <summary>잠들기 후 호출: 창문 밖 → 창문 → 책상으로 날아 들어옴</summary>
    public void FlyIn()
    {
        if (flyInDirector != null)
        {
            if (_currentFlight != null) StopCoroutine(_currentFlight);

            if (craneRootAnimator != null) craneRootAnimator.enabled = true; // Animator 재활성화
            gameObject.SetActive(true); // 학 표시

            flyInDirector.stopped -= OnFlyInDirectorStopped;
            flyInDirector.stopped += OnFlyInDirectorStopped;
            flyInDirector.Play();

            DebugLog("Timeline으로 귀환 시작");
        }
        else
        {
            FlyInCoroutineFallback();
        }
    }

    // ── Timeline 완료 콜백 ───────────────────────────────────────

    private void OnFlyOutDirectorStopped(PlayableDirector director)
    {
        director.stopped -= OnFlyOutDirectorStopped;
        gameObject.SetActive(false); // 창문 밖으로 나간 후 숨김
        DebugLog("Timeline 날아가기 완료");
        OnFlyOutComplete?.Invoke();
    }

    private void OnFlyInDirectorStopped(PlayableDirector director)
    {
        director.stopped -= OnFlyInDirectorStopped;
        // Animator 비활성화: Hold 모드의 마지막 위치(책상)를 고정하고 PlayableGraph 간섭 차단
        if (craneRootAnimator != null) craneRootAnimator.enabled = false;
        DebugLog("Timeline 귀환 완료");
        OnFlyInComplete?.Invoke();
    }

    // ── Coroutine Fallback ───────────────────────────────────────

    private void FlyOutCoroutineFallback()
    {
        if (deskPoint == null || windowPoint == null || outsidePoint == null)
        {
            Debug.LogError("[PaperCraneController] 비행 경로 포인트가 설정되지 않았습니다.");
            OnFlyOutComplete?.Invoke();
            return;
        }

        transform.position = deskPoint.position;
        gameObject.SetActive(true);

        if (_currentFlight != null) StopCoroutine(_currentFlight);
        _currentFlight = StartCoroutine(FlyOutSequence());
    }

    private void FlyInCoroutineFallback()
    {
        if (deskPoint == null || windowPoint == null || outsidePoint == null)
        {
            Debug.LogError("[PaperCraneController] 비행 경로 포인트가 설정되지 않았습니다.");
            OnFlyInComplete?.Invoke();
            return;
        }

        transform.position = outsidePoint.position;
        gameObject.SetActive(true);

        if (_currentFlight != null) StopCoroutine(_currentFlight);
        _currentFlight = StartCoroutine(FlyInSequence());
    }

    // ── Coroutine 시퀀스 ─────────────────────────────────────────

    private IEnumerator FlyOutSequence()
    {
        yield return new WaitForEndOfFrame();

        DebugLog("날아가기 시작: 책상 → 창문");
        float halfDuration = flyDuration * 0.5f;

        yield return StartCoroutine(FlyBezier(deskPoint.position, windowPoint.position, arcHeight, halfDuration));

        DebugLog("창문 통과 → 창문 밖");

        yield return StartCoroutine(FlyBezier(windowPoint.position, outsidePoint.position, 0f, halfDuration));

        gameObject.SetActive(false);
        DebugLog("날아가기 완료");
        OnFlyOutComplete?.Invoke();
    }

    private IEnumerator FlyInSequence()
    {
        yield return new WaitForEndOfFrame();

        DebugLog("돌아오기 시작: 창문 밖 → 창문");
        float halfDuration = flyDuration * 0.5f;

        yield return StartCoroutine(FlyBezier(outsidePoint.position, windowPoint.position, 0f, halfDuration));

        DebugLog("창문 통과 → 책상");

        yield return StartCoroutine(FlyBezier(windowPoint.position, deskPoint.position, arcHeight, halfDuration));

        DebugLog("돌아오기 완료");
        OnFlyInComplete?.Invoke();
    }

    // ── Bezier 이동 Coroutine ─────────────────────────────────────

    private IEnumerator FlyBezier(Vector3 start, Vector3 end, float arcH, float duration)
    {
        Vector3 controlPoint = (start + end) * 0.5f + Vector3.up * arcH;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 nextPos = QuadraticBezier(start, controlPoint, end, smoothT);

            Vector3 direction = nextPos - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.forward = direction.normalized;

            transform.position = nextPos;
            yield return null;
        }

        transform.position = end;
    }

    private Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    // ── Gizmo ─────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (deskPoint == null || windowPoint == null || outsidePoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(deskPoint.position, 0.1f);
        Gizmos.DrawSphere(windowPoint.position, 0.1f);
        Gizmos.DrawSphere(outsidePoint.position, 0.1f);

        Gizmos.color = Color.cyan;
        Vector3 cp1 = (deskPoint.position + windowPoint.position) * 0.5f + Vector3.up * arcHeight;
        Vector3 prev = deskPoint.position;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 next = QuadraticBezier(deskPoint.position, cp1, windowPoint.position, t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(windowPoint.position, outsidePoint.position);
    }

    private void DebugLog(string msg)
    {
        if (enableDebugLogs)
            Debug.Log($"[PaperCraneController] {msg}");
    }
}
