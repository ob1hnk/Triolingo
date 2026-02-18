using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 종이학 비행 애니메이션 컨트롤러 (DOTween 없이 Coroutine + Bezier 구현)
///
/// 사용법:
///   1. PaperCrane 오브젝트에 이 컴포넌트 추가
///   2. Inspector에서 3개의 Empty Transform 연결:
///      - deskPoint    : 책상 위 시작/착지 위치
///      - windowPoint  : 창문 안쪽 통과 지점
///      - outsidePoint : 창문 바깥 사라지는 지점
///   3. RoomStateManager가 FlyOut() / FlyIn() 호출
///   4. OnFlyOutComplete / OnFlyInComplete 이벤트를 RoomStateManager가 구독
///
/// 비행 경로: Quadratic Bezier 곡선 (호(arc) 형태)
///   FlyOut: deskPoint → [arc] → windowPoint → outsidePoint
///   FlyIn:  outsidePoint → windowPoint → [arc] → deskPoint
/// </summary>
public class PaperCraneController : MonoBehaviour
{
    [Header("비행 경로 포인트 (Empty Transform)")]
    [SerializeField] private Transform deskPoint;
    [SerializeField] private Transform windowPoint;
    [SerializeField] private Transform outsidePoint;

    [Header("비행 설정")]
    [SerializeField] private float flyDuration = 2.5f;
    [SerializeField] private float arcHeight   = 2.0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // 외부 시스템(RoomStateManager)이 구독
    public event Action OnFlyOutComplete;
    public event Action OnFlyInComplete;

    private Coroutine _currentFlight;

    private void Start()
    {
        // 초기에는 책상 위에 위치, 비활성화
        if (deskPoint != null)
            transform.position = deskPoint.position;

        gameObject.SetActive(false);
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>편지 전송 시 호출: 책상 → 창문 → 창문 밖으로 날아감</summary>
    public void FlyOut()
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

        // 2-구간 비행: desk→window (arc), window→outside (직선)
        _currentFlight = StartCoroutine(FlyOutSequence());
    }

    /// <summary>잠들기 후 호출: 창문 밖 → 창문 → 책상으로 날아 들어옴</summary>
    public void FlyIn()
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

        // 2-구간 비행: outside→window (직선), window→desk (arc)
        _currentFlight = StartCoroutine(FlyInSequence());
    }

    // ── Coroutine 시퀀스 ────────────────────────────────────────

    private IEnumerator FlyOutSequence()
    {
        // 첫 프레임 대기 (timeScale=0 복구 후 시작)
        yield return new WaitForEndOfFrame();

        DebugLog("날아가기 시작: 책상 → 창문");
        float halfDuration = flyDuration * 0.5f;

        // 구간 1: 책상 → 창문 (arc)
        yield return StartCoroutine(FlyBezier(
            deskPoint.position,
            windowPoint.position,
            arcHeight,
            halfDuration
        ));

        DebugLog("창문 통과 → 창문 밖");

        // 구간 2: 창문 → 바깥 (직선, 호 없음)
        yield return StartCoroutine(FlyBezier(
            windowPoint.position,
            outsidePoint.position,
            0f,
            halfDuration
        ));

        gameObject.SetActive(false);
        DebugLog("날아가기 완료");
        OnFlyOutComplete?.Invoke();
    }

    private IEnumerator FlyInSequence()
    {
        yield return new WaitForEndOfFrame();

        DebugLog("돌아오기 시작: 창문 밖 → 창문");
        float halfDuration = flyDuration * 0.5f;

        // 구간 1: 바깥 → 창문 (직선)
        yield return StartCoroutine(FlyBezier(
            outsidePoint.position,
            windowPoint.position,
            0f,
            halfDuration
        ));

        DebugLog("창문 통과 → 책상");

        // 구간 2: 창문 → 책상 (arc)
        yield return StartCoroutine(FlyBezier(
            windowPoint.position,
            deskPoint.position,
            arcHeight,
            halfDuration
        ));

        DebugLog("돌아오기 완료");
        OnFlyInComplete?.Invoke();
    }

    // ── Bezier 이동 Coroutine ────────────────────────────────────

    /// <summary>
    /// Quadratic Bezier 곡선을 따라 start → end로 이동
    /// arcH > 0이면 중간 지점을 위로 올려 호(arc) 형태 생성
    /// </summary>
    private IEnumerator FlyBezier(Vector3 start, Vector3 end, float arcH, float duration)
    {
        // Bezier 제어점: 시작-끝 중간을 arcH만큼 위로
        Vector3 controlPoint = (start + end) * 0.5f + Vector3.up * arcH;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float rawT    = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, rawT); // 가속/감속

            Vector3 nextPos = QuadraticBezier(start, controlPoint, end, smoothT);

            // 이동 방향으로 학 회전
            Vector3 direction = nextPos - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.forward = direction.normalized;

            transform.position = nextPos;

            yield return null;
        }

        // 정확히 목표 위치에 맞춤
        transform.position = end;
    }

    // ── 수학 유틸 ────────────────────────────────────────────────

    /// <summary>2차 Bezier: (1-t)²·p0 + 2(1-t)t·p1 + t²·p2</summary>
    private Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0
             + 2f * u * t * p1
             + t * t * p2;
    }

    // ── Gizmo (에디터 시각화) ─────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (deskPoint == null || windowPoint == null || outsidePoint == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(deskPoint.position, 0.1f);
        Gizmos.DrawSphere(windowPoint.position, 0.1f);
        Gizmos.DrawSphere(outsidePoint.position, 0.1f);

        // 비행 경로 시각화 (30 단계)
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
