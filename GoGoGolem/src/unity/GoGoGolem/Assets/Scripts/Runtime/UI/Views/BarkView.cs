using UnityEngine;
using TMPro;

namespace UI.Views
{
    /// <summary>
    /// Bark UI View - 짧은 혼잣말/알림 텍스트 표시 (로직 없음)
    ///
    /// Hierarchy 구조 예시:
    ///   Canvas
    ///   └── BarkPanel (이 컴포넌트를 붙일 곳)
    ///       ├── Background (Image - 배경)
    ///       ├── SpeakerNameText (TMP - 발화자 이름, 선택)
    ///       └── MessageText (TMP - 대사)
    /// </summary>
    public class BarkView : MonoBehaviour
    {
        #region Inspector - UI References
        [Header("Panel")]
        [SerializeField] private GameObject panel;

        [Header("Content")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text messageText;
        #endregion

        #region Properties
        public bool IsOpen => panel != null && panel.activeSelf;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (panel != null)
                panel.SetActive(false);
        }
        #endregion

        #region Public API (Presenter가 호출)
        public void Show(string speaker, string message)
        {
            if (panel == null) return;

            if (speakerNameText != null)
            {
                bool hasSpeaker = !string.IsNullOrEmpty(speaker);
                speakerNameText.gameObject.SetActive(hasSpeaker);
                if (hasSpeaker) speakerNameText.text = speaker;
            }

            if (messageText != null)
                messageText.text = message;

            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel == null) return;
            panel.SetActive(false);
        }
        #endregion
    }
}