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
    /// - letter_id로 Firebase에서 답장 조회
    /// - LetterReader를 통해 비동기 조회
    /// - 게임 일시정지/재개 제어
    ///
    /// letter_id 제공 방식:
    /// - 실제 게임: LoadManager.Instance.GetLetterId() (다른 팀원 구현)
    /// - 테스트: Inspector의 testLetterId에 하드코딩
    ///
    /// LoadManager 연동 예시 (나중에 다른 팀원이 구현):
    ///   string letterId = LoadManager.Instance.GetLetterId();
    ///   letterReadPresenter.Open(letterId);
    /// </summary>
    public class LetterReadPresenter : MonoBehaviour
    {
        #region Inspector
        [Header("References")]
        [SerializeField] private LetterReadView view;
        [SerializeField] private LetterReader letterReader;

        [Header("Test (LoadManager 대신 하드코딩)")]
        [Tooltip("Firebase에 있는 실제 task_id를 입력하세요")]
        [SerializeField] private string testLetterId = "";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events (외부 시스템이 구독)
        public event Action<bool> OnPanelToggled;
        #endregion

        #region Private
        private bool _isLoading;
        private float _savedTimeScale;
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
        /// RoomStateManager가 편지 전송 후 taskId를 전달할 때 호출.
        /// Open() 호출 전에 설정해두면 testLetterId 대신 사용됨.
        /// </summary>
        public void SetTaskId(string taskId)
        {
            testLetterId = taskId;
            DebugLog($"TaskId 설정됨: {taskId}");
        }

        /// <summary>
        /// 편지 읽기 UI를 연다.
        /// letterId가 null이면 testLetterId(하드코딩 또는 SetTaskId로 설정된 값) 사용.
        ///
        /// 사용법:
        ///   LetterDesk에서:       presenter.Open();          // testLetterId 사용
        ///   LoadManager 연동:     presenter.Open(letterId);   // 매니저에서 받은 id 사용
        /// </summary>
        public void Open(string letterId = null)
        {
            if (view.IsOpen) return;

            string id = letterId ?? testLetterId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[LetterReadPresenter] letter_id가 없습니다. testLetterId를 Inspector에 입력하세요.");
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
                Debug.Log($"[LetterReadPresenter] {message}");
        }
        #endregion
    }
}
