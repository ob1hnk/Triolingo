using System;
using UnityEngine;
using UI.Presenters;

/// <summary>
/// Room 씬 상태 관리
///
/// 상태:
///   BeforeLetter — 편지 쓰기 가능, 침대 E → "자기 전에 부모님께 편지를 쓰자!" 로그
///   AfterLetter  — 책상 비활성, 침대 E → SleepTimeline 재생
///   Morning      — 침대 비활성, 책상 "편지 읽기" 모드
///
/// 전환:
///   BeforeLetter →(편지 전송)→ AfterLetter →(잠들기)→ Morning
/// </summary>
public class RoomStateManager : MonoBehaviour
{
    // ── 상태 ────────────────────────────────────────────────────
    public enum RoomState { BeforeLetter, AfterLetter, Morning }
    public RoomState CurrentState { get; private set; } = RoomState.BeforeLetter;

    public event Action<RoomState> OnStateChanged;

    // ── Inspector ───────────────────────────────────────────────
    [Header("Letter System")]
    [SerializeField] private LetterWritePresenter letterWritePresenter;
    [SerializeField] private LetterDesk           letterDesk;

    [Header("Bed Interaction")]
    [SerializeField] private BedInteraction bedInteraction;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    // ── Unity Lifecycle ─────────────────────────────────────────

    private void OnEnable()
    {
        if (letterWritePresenter != null)
        {
            letterWritePresenter.OnLetterSubmitted += HandleLetterSubmitted;
            letterWritePresenter.OnTaskIdReceived  += HandleTaskIdReceived;
        }

        if (bedInteraction != null)
            bedInteraction.OnSlept += HandleSlept;
    }

    private void OnDisable()
    {
        if (letterWritePresenter != null)
        {
            letterWritePresenter.OnLetterSubmitted -= HandleLetterSubmitted;
            letterWritePresenter.OnTaskIdReceived  -= HandleTaskIdReceived;
        }

        if (bedInteraction != null)
            bedInteraction.OnSlept -= HandleSlept;
    }

    private void Start()
    {
        ApplyState(RoomState.BeforeLetter);
    }

    // ── Public ──────────────────────────────────────────────────

    /// <summary>외부에서 상태를 직접 설정 (테스트용)</summary>
    public void SetState(RoomState state)
    {
        ApplyState(state);
    }

    // ── Event Handlers ──────────────────────────────────────────

    private void HandleLetterSubmitted()
    {
        DebugLog("편지 제출됨. AfterLetter로 전환.");
        ApplyState(RoomState.AfterLetter);
    }

    private void HandleTaskIdReceived(string taskId)
    {
        DebugLog($"taskId 수신 (taskId={taskId}).");

        if (GameManager.Instance != null)
            GameManager.Instance.SetLetterId(taskId);
    }

    private void HandleSlept()
    {
        DebugLog("잠들기 완료. Morning으로 전환.");
        ApplyState(RoomState.Morning);
    }

    // ── State Apply ─────────────────────────────────────────────

    private void ApplyState(RoomState state)
    {
        CurrentState = state;
        DebugLog($"상태 전환: {state}");

        switch (state)
        {
            case RoomState.BeforeLetter:
                ApplyBeforeLetter();
                break;
            case RoomState.AfterLetter:
                ApplyAfterLetter();
                break;
            case RoomState.Morning:
                ApplyMorning();
                break;
        }

        OnStateChanged?.Invoke(state);
    }

    private void ApplyBeforeLetter()
    {
        // 책상: 쓰기 모드, 활성
        if (letterDesk != null)
        {
            letterDesk.SetMode(LetterDesk.DeskMode.Write);
            letterDesk.SetCanInteract(true);
        }

        // 침대: 프롬프트 표시, E 누르면 메시지만 출력
        if (bedInteraction != null)
        {
            bedInteraction.SetCanInteract(true);
            bedInteraction.SetBlockedMessage("자기 전에 부모님께 편지를 쓰자!");
        }
    }

    private void ApplyAfterLetter()
    {
        // 책상: 비활성 (프롬프트 표시 안 됨)
        if (letterDesk != null) letterDesk.SetCanInteract(false);

        // 침대: 잠들기 가능 (블록 해제)
        if (bedInteraction != null)
        {
            bedInteraction.SetCanInteract(true);
            bedInteraction.SetBlockedMessage(null);
        }
    }

    private void ApplyMorning()
    {
        // 침대: 비활성 (프롬프트 표시 안 됨)
        if (bedInteraction != null)
        {
            bedInteraction.SetCanInteract(false);
            bedInteraction.SetBlockedMessage(null);
        }

        // 책상: 읽기 모드, 활성
        if (letterDesk != null)
        {
            letterDesk.SetMode(LetterDesk.DeskMode.Read);
            letterDesk.SetCanInteract(true);
        }
    }

    // ── Debug ────────────────────────────────────────────────────

    private void DebugLog(string msg)
    {
        if (enableDebugLogs)
            Debug.Log($"[RoomStateManager] {msg}");
    }
}