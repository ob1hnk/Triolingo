using TMPro;
using UnityEngine;

/// <summary>
/// NPC 머리 위 말풍선 UI.
/// NPC의 자식 World Space Canvas에 부착한다.
/// 카메라를 항상 바라보도록 LateUpdate에서 빌보드 처리한다.
/// </summary>
public class SpeechBubbleView : MonoBehaviour
{
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private TMP_Text textField;

    private Camera _mainCamera;

    private void Awake()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        Hide();
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // 카메라 뷰 평면과 항상 평행하게 유지 (빌보드)
        transform.rotation = _mainCamera.transform.rotation;
    }

    public void Show(string text)
    {
        if (textField != null) textField.text = text;
        if (bubbleRoot != null) bubbleRoot.SetActive(true);
    }

    public void Hide()
    {
        if (bubbleRoot != null) bubbleRoot.SetActive(false);
    }
}
