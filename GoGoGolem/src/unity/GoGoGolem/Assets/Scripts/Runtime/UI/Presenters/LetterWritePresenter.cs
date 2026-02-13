using System;
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

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (외부 시스템이 구독 - QuestManager 등)
        public event Action<string> OnLetterSent;
        public event Action<bool> OnPanelToggled;
        #endregion

        #region Private
        private bool _isSending;
        private float _savedTimeScale;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (letterSender != null)
            {
                letterSender.OnProcessing += HandleProcessing;
                letterSender.OnError += HandleError;
            }
        }

        private void OnDisable()
        {
            if (letterSender != null)
            {
                letterSender.OnProcessing -= HandleProcessing;
                letterSender.OnError -= HandleError;
            }
        }

        private void Update()
        {
            if (!view.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                HandleSendRequested(view.LetterContent);
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleCloseRequested();
            }
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
            if (_isSending) return;

            if (string.IsNullOrWhiteSpace(letterContent))
            {
                view.ShowError("편지 내용을 작성해주세요.");
                return;
            }

            if (letterSender == null)
            {
                Debug.LogError("[LetterWritePresenter] LetterSender가 연결되지 않았습니다.");
                view.ShowError("전송 시스템이 준비되지 않았습니다.");
                return;
            }

            try
            {
                _isSending = true;
                view.SetSending(true);
                DebugLog($"편지 전송 시작 ({letterContent.Length}자)");

                string taskId = await letterSender.SendLetterAsync(letterContent);

                DebugLog($"편지 전송 완료 - Task ID: {taskId}");
                OnLetterSent?.Invoke(taskId);

                Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterWritePresenter] 전송 실패: {ex.Message}");
                view.ShowError("편지 전송에 실패했습니다. 다시 시도해주세요.");
                view.SetSending(false);
            }
            finally
            {
                _isSending = false;
            }
        }

        private void HandleCloseRequested()
        {
            if (_isSending)
            {
                DebugLog("전송 중에는 닫을 수 없음");
                return;
            }

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
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            DebugLog("월드 일시정지");
        }

        private void ResumeGame()
        {
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
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
