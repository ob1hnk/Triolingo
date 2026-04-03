using System;
using System.Threading.Tasks;
using UnityEngine;
using Multimodal.Letter;
using UI.Views;

namespace UI.Presenters
{
    /// <summary>
    /// 편지 작성 Presenter (로직 담당)
    ///
    /// 역할:
    /// - LetterDesk.Interact() → Open() 호출로 UI 열림
    /// - Enter/Esc 키 입력 처리
    /// - LetterSender를 통해 서버 전송
    /// - 게임 일시정지/재개 제어
    /// </summary>
    public class LetterWritePresenter : MonoBehaviour
    {
        #region Inspector
        [Header("References")]
        [SerializeField] private LetterWriteView view;
        [SerializeField] private LetterSender letterSender;

        [Header("Events")]
        [SerializeField] private GameEvent onLetterSubmittedEvent;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (외부 시스템이 구독 - RoomStateManager 등)
        /// <summary>Enter 즉시 발행 — UI 닫기·상태 전환 트리거용</summary>
        public event Action OnLetterSubmitted;
        /// <summary>HTTP 응답 후 발행 — taskId 저장용</summary>
        public event Action<string> OnTaskIdReceived;
        public event Action<bool> OnPanelToggled;
        #endregion

        #region Private
        private bool _isSending;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (letterSender != null)
            {
                letterSender.OnProcessing += HandleProcessing;
                letterSender.OnError += HandleError;
            }
            if (view != null)
                view.OnSubmit += HandleSubmitFromInputField;
        }

        private void OnDisable()
        {
            if (letterSender != null)
            {
                letterSender.OnProcessing -= HandleProcessing;
                letterSender.OnError -= HandleError;
            }
            if (view != null)
                view.OnSubmit -= HandleSubmitFromInputField;
        }

        private void Update()
        {
            if (!view.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
                HandleCloseRequested();
        }

        private void HandleSubmitFromInputField(string text)
        {
            DebugLog($"InputField 제출 감지 - 내용: '{text}' ({text.Length}자)");
            HandleSendRequested(text);
        }
        #endregion

        #region Public API (LetterDesk.Interact()에서 호출)
        public void Open()
        {
            if (view.IsOpen) return;

            DebugLog("편지 작성 UI 열기");

            PauseGame();
            view.Show();
            OnPanelToggled?.Invoke(true);
        }

        public void Close()
        {
            if (!view.IsOpen) return;

            DebugLog("편지 작성 UI 닫기");

            view.Hide();
            ResumeGame();
            OnPanelToggled?.Invoke(false);
        }
        #endregion

        #region Input Handlers
        private async void HandleSendRequested(string letterContent)
        {
            if (_isSending) { DebugLog("이미 전송 중 - 중복 요청 무시"); return; }

            if (string.IsNullOrWhiteSpace(letterContent))
            {
                DebugLog("전송 실패: 내용이 비어있음");
                return;
            }

            if (letterSender == null)
            {
                Debug.LogError("[LetterWritePresenter] LetterSender가 연결되지 않았습니다.");
                return;
            }

            _isSending = true;

            // 1. Enter 즉시: 캔버스 닫고 상태 전환 트리거
            DebugLog("OnLetterSubmitted 발행 - 즉시 닫기");
            Close();
            OnLetterSubmitted?.Invoke();
            if (onLetterSubmittedEvent != null) onLetterSubmittedEvent.Raise();

            // 2. 백그라운드에서 HTTP 요청 (씬 전환과 무관하게 진행)
            DebugLog($"HTTP 백그라운드 전송 시작 ({letterContent.Length}자)");
            try
            {
                string taskId = await letterSender.SendLetterAsync(letterContent);
                DebugLog($"HTTP 응답 완료 - Task ID: {taskId}");
                OnTaskIdReceived?.Invoke(taskId);
                DebugLog("OnTaskIdReceived 발행 완료");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterWritePresenter] HTTP 전송 실패: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _isSending = false;
            }
        }

        private void HandleCloseRequested()
        {
            Close();
        }
        #endregion

        #region LetterSender Event Handlers
        private void HandleProcessing(string taskId)
        {
            DebugLog($"서버 처리 중 - Task ID: {taskId}");
        }

        private void HandleError(string code, string message)
        {
            Debug.LogError($"[LetterWritePresenter] 서버 에러 [{code}]: {message}");
        }
        #endregion

        #region Game State
        private void PauseGame()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ChangeState(GameState.Paused);
            DebugLog("월드 일시정지");
        }

        private void ResumeGame()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.ChangeState(GameState.Gameplay);
            DebugLog("월드 재개");
        }
        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[LetterWritePresenter] {message}");
        }
        #endregion
    }
}
