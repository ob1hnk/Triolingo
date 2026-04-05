using System;
using UnityEngine;
using UI.Views;

namespace UI.Presenters
{
    /// <summary>
    /// Bark Presenter - 짧은 혼잣말/알림 텍스트를 순차 표시
    ///
    /// 사용법:
    ///   // 단일 대사
    ///   BarkPresenter.Instance.Bark("주인공", "오늘 뭐 했지?");
    ///
    ///   // 시퀀스 (여러 대사를 순서대로)
    ///   BarkPresenter.Instance.Bark(new[] {
    ///       new BarkLine("주인공", "오늘 뭐 있었지?"),
    ///       new BarkLine("주인공", "편지에 적어볼까."),
    ///   });
    ///
    ///   // 완료 콜백 (모든 라인 종료 후 호출)
    ///   BarkPresenter.Instance.Bark(lines, onComplete: () => letterPresenter.Open());
    ///
    /// 발화자 이름이 "주인공"이면 PlayerData.playerName으로 치환됨.
    /// 입력(마우스 클릭 / Space / Enter)을 받으면 다음 라인으로 진행하거나 창이 닫힘.
    /// </summary>
    public class BarkPresenter : MonoBehaviour
    {
        public static BarkPresenter Instance { get; private set; }

        // PlayerData.playerName으로 치환되는 키
        private const string PlayerCharacterKey = "주인공";

        #region Inspector
        [Header("References")]
        [SerializeField] private BarkView view;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;
        #endregion

        #region Private
        private BarkLine[] _lines;
        private int _index;
        private bool _isShowing;
        // 같은 프레임에 Bark()가 호출되면 해당 입력으로 즉시 닫히지 않도록
        private bool _justOpened;
        private Action _onComplete;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_isShowing) return;

            if (_justOpened)
            {
                _justOpened = false;
                return;
            }

            if (Input.GetMouseButtonDown(0)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Advance();
            }
        }
        #endregion

        #region Public API
        /// <summary>단일 대사 출력 (편의 메소드)</summary>
        public void Bark(string speaker, string message)
        {
            Bark(new[] { new BarkLine(speaker, message) }, null);
        }

        /// <summary>여러 대사를 순차 출력</summary>
        public void Bark(BarkLine[] lines)
        {
            Bark(lines, null);
        }

        /// <summary>여러 대사를 순차 출력 + 완료 콜백</summary>
        public void Bark(BarkLine[] lines, Action onComplete)
        {
            if (view == null)
            {
                Debug.LogError("[BarkPresenter] BarkView가 연결되지 않았습니다.");
                return;
            }

            if (lines == null || lines.Length == 0) return;

            _lines = lines;
            _index = 0;
            _onComplete = onComplete;
            _isShowing = true;
            _justOpened = true;

            ShowCurrentLine();
        }

        /// <summary>즉시 숨김 (완료 콜백은 호출되지 않음)</summary>
        public void Hide()
        {
            if (!_isShowing) return;
            if (view != null) view.Hide();
            _isShowing = false;
            _lines = null;
            _onComplete = null;
            DebugLog("Hide");
        }
        #endregion

        #region Private
        private void ShowCurrentLine()
        {
            var line = _lines[_index];
            string displaySpeaker = ResolveSpeaker(line.speaker);
            DebugLog($"Line {_index + 1}/{_lines.Length}: [{displaySpeaker}] \"{line.message}\"");
            view.Show(displaySpeaker, line.message);
        }

        private void Advance()
        {
            _index++;
            if (_index >= _lines.Length)
            {
                Complete();
            }
            else
            {
                ShowCurrentLine();
            }
        }

        private void Complete()
        {
            var callback = _onComplete;
            if (view != null) view.Hide();
            _isShowing = false;
            _lines = null;
            _onComplete = null;
            DebugLog("Complete");
            callback?.Invoke();
        }

        private string ResolveSpeaker(string speaker)
        {
            if (speaker == PlayerCharacterKey
                && GameManager.Instance != null
                && GameManager.Instance.HasPlayerName)
            {
                return GameManager.Instance.PlayerName;
            }
            return speaker;
        }

        private void DebugLog(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[BarkPresenter] {message}");
        }
        #endregion
    }
}