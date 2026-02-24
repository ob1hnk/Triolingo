using UnityEngine;
using TMPro;

namespace UI.Views
{
    /// <summary>
    /// 편지 읽기 UI View (답장 표시만 담당, 로직 없음)
    ///
    /// Hierarchy 구조:
    ///   Canvas
    ///   └── LetterReadPanel (이 컴포넌트를 붙일 곳)
    ///       ├── Overlay (Image - 반투명 어두운 배경)
    ///       ├── LetterPaper (Image - 편지지)
    ///       │   └── LetterContentText (TMP - 답장 내용)
    ///       └── KeyHintText (TMP - "Esc: 닫기")
    /// </summary>
    public class LetterReadView : MonoBehaviour
    {
        #region Inspector - UI References
        [Header("Panel")]
        [SerializeField] private GameObject letterPanel;

        [Header("Content")]
        [SerializeField] private TMP_Text letterContentText;

        [Header("Hints")]
        [SerializeField] private TMP_Text keyHintText;
        #endregion

        #region Properties
        public bool IsOpen => letterPanel != null && letterPanel.activeSelf;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (letterPanel != null)
                letterPanel.SetActive(false);
        }
        #endregion

        #region Public API (Presenter가 호출)
        public void Show()
        {
            if (letterPanel == null) return;

            letterPanel.SetActive(true);

            if (letterContentText != null)
                letterContentText.text = "";

            if (keyHintText != null)
                keyHintText.text = "불러오는 중...";
        }

        public void Hide()
        {
            if (letterPanel == null) return;

            letterPanel.SetActive(false);
        }

        public void ShowLetterContent(string content)
        {
            if (letterContentText != null)
                letterContentText.text = content;

            if (keyHintText != null)
                keyHintText.text = "Esc: 닫기";
        }

        public void ShowMessage(string message)
        {
            if (letterContentText != null)
                letterContentText.text = message;

            if (keyHintText != null)
                keyHintText.text = "Esc: 닫기";
        }
        #endregion
    }
}
