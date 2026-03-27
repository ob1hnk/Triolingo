using System;
using UnityEngine;
using TMPro;

namespace UI.Views
{
    /// <summary>
    /// 편지 작성 UI View (표시만 담당, 로직 없음)
    ///
    /// </summary>
    public class LetterWriteView : MonoBehaviour
    {
        #region Inspector - UI References
        [Header("Panel")]
        [SerializeField] private GameObject letterPanel;

        [Header("Letter Content")]
        [SerializeField] private TMP_InputField letterInputField;
        [SerializeField] private TMP_Text charCountText;

        [Header("Hints")]
        [SerializeField] private TMP_Text keyHintText;

        [Header("Settings")]
        [SerializeField] private int maxCharacterCount = 300;
        #endregion

        #region Properties
        public string LetterContent => letterInputField != null ? letterInputField.text : "";
        public bool IsOpen => letterPanel != null && letterPanel.activeSelf;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (letterPanel != null)
                letterPanel.SetActive(false);

            SetupUI();
        }
        #endregion

        #region Public API (Presenter가 호출)
        public void Show()
        {
            if (letterPanel == null) return;

            letterPanel.SetActive(true);
            ClearInput();
            FocusInput();
        }

        public void Hide()
        {
            if (letterPanel == null) return;

            letterPanel.SetActive(false);
        }

        public void SetSending(bool isSending)
        {
            if (letterInputField != null)
                letterInputField.interactable = !isSending;

            if (keyHintText != null)
                keyHintText.text = isSending ? "편지를 보내는 중..." : "편지를 보내지 못했습니다.";
        }

        public void ShowError(string message)
        {
            Debug.LogWarning($"[LetterWriteView] {message}");
        }
        #endregion

        #region Private
        private void SetupUI()
        {
            if (letterInputField != null)
            {
                letterInputField.characterLimit = maxCharacterCount;
                letterInputField.onValueChanged.AddListener(OnInputChanged);
            }

            UpdateCharCount();
        }

        private void OnInputChanged(string text)
        {
            UpdateCharCount();
        }

        private void UpdateCharCount()
        {
            if (charCountText != null)
            {
                int current = letterInputField != null ? letterInputField.text.Length : 0;
                charCountText.text = $"{current} / {maxCharacterCount}";
            }
        }

        private void ClearInput()
        {
            if (letterInputField != null)
                letterInputField.text = "";

            UpdateCharCount();
        }

        private void FocusInput()
        {
            if (letterInputField != null)
            {
                letterInputField.Select();
                letterInputField.ActivateInputField();
            }
        }
        #endregion
    }
}
