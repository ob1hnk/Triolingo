using System;
using UnityEngine;
using Multimodal.Letter;
using UI.Views;

namespace UI.Presenters
{
    /// <summary>
    /// 편지 읽기 Presenter (로직 담당)
    ///
    /// 역할:
    /// - GameManager.CurrentLetterId로 Firebase에서 답장 조회
    /// - LetterReader를 통해 비동기 조회
    /// - GameStateManager를 통한 게임 상태 전환
    /// </summary>
    public class LetterReadPresenter : MonoBehaviour
    {
        #region Inspector
        [Header("References")]
        [SerializeField] private LetterReadView view;
        [SerializeField] private LetterReader letterReader;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (외부 시스템이 구독)
        public event Action<bool> OnPanelToggled;
        #endregion

        #region Private
        private bool _isLoading;
        private GameState _previousState;
        #endregion

        #region Unity Lifecycle
        private void Update()
        {
            if (!view.IsOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// 편지 읽기 UI를 연다.
        /// GameManager.CurrentLetterId에서 letter_id를 가져온다.
        /// </summary>
        public void Open()
        {
            if (view.IsOpen) return;

            string id = GameManager.Instance != null ? GameManager.Instance.CurrentLetterId : null;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[LetterReadPresenter] letter_id가 없습니다. GameManager.CurrentLetterId를 확인하세요.");
                return;
            }

            DebugLog($"편지 읽기 UI 열기 - letter_id: {id}");

            PauseGame();
            view.Show();
            OnPanelToggled?.Invoke(true);

            FetchLetter(id);
        }

        public void Close()
        {
            if (!view.IsOpen) return;

            DebugLog("편지 읽기 UI 닫기");

            view.Hide();
            ResumeGame();
            OnPanelToggled?.Invoke(false);
        }
        #endregion

        #region Fetch
        private async void FetchLetter(string letterId)
        {
            if (_isLoading) return;

            if (letterReader == null)
            {
                Debug.LogError("[LetterReadPresenter] LetterReader가 연결되지 않았습니다.");
                view.ShowMessage("시스템 오류: LetterReader 미연결");
                return;
            }

            try
            {
                _isLoading = true;
                DebugLog($"Firebase에서 답장 조회 중... (task_id: {letterId})");

                LetterResponse response = await letterReader.FetchResponseByTaskIdAsync(letterId);

                if (response == null)
                {
                    DebugLog("답장이 아직 없음");
                    view.ShowMessage("아직 답장이 오지 않았습니다.\n조금만 기다려주세요.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(response.GeneratedResponseLetter))
                {
                    DebugLog("답장 내용이 비어있음");
                    view.ShowMessage("답장이 아직 작성 중입니다.");
                    return;
                }

                DebugLog($"답장 수신 완료 ({response.GeneratedResponseLetter.Length}자)");
                view.ShowLetterContent(response.GeneratedResponseLetter);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LetterReadPresenter] 조회 실패: {ex.Message}");
                view.ShowMessage("편지를 불러오는데 실패했습니다.");
            }
            finally
            {
                _isLoading = false;
            }
        }
        #endregion

        #region Game State
        private void PauseGame()
        {
            if (GameStateManager.Instance == null) return;
            _previousState = GameStateManager.Instance.CurrentState;
            GameStateManager.Instance.ChangeState(GameState.LetterUI);
            DebugLog("GameState → LetterUI");
        }

        private void ResumeGame()
        {
            if (GameStateManager.Instance == null) return;
            GameStateManager.Instance.ChangeState(_previousState);
            DebugLog($"GameState → {_previousState}");
        }
        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[LetterReadPresenter] {message}");
        }
        #endregion
    }
}
