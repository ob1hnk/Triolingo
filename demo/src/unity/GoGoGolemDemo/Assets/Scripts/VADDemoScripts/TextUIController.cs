using UnityEngine;
using TMPro;
public class TextUIController : MonoBehaviour
{
  [Header("UI References")]
  [SerializeField] private TextMeshProUGUI textTMP;
  [SerializeField] private RectTransform textBody;

  [Header("Text Body Settings")]
  [SerializeField] private float maxWidth = 1000f;      // 줄 넘어가는 길이
  [SerializeField] private float heightIncrement = 50f; // 줄 넘어갔을 때 증가하는 말풍선 높이
  [SerializeField] private float initialHeight = 100f;  // 초기 말풍선 높이
  [SerializeField] private float padding = 20f;         // 텍스트와 배경 사이 여백

  private float currentHeight;

  void Start()
  {
    // UI 연결 확인
    if (textTMP == null)
    {
      Debug.LogError("TextMeshProUGUI 컴포넌트 미할당");
    }
        
    if (textBody == null)
    {
      Debug.LogError("Text Body RectTransform 컴포넌트 미할당");
    }
        
    currentHeight = initialHeight;
  }

  /// <summary>
  /// WebSocket으로부터 받은 텍스트로 UI 업데이트
  /// </summary>
  public void UpdateText(string newText)
  {
    if (textTMP == null || textBody == null)
    {
      Debug.LogError("UI 컴포넌트가 제대로 할당되지 않음");
      return;
    }

    // 텍스트 업데이트
    textTMP.text = newText;
        
    // 텍스트 렌더링 강제 업데이트 (Text Body 조절 위해)
    Canvas.ForceUpdateCanvases();
    textTMP.ForceMeshUpdate();
        
    // 텍스트의 실제 크기 계산 (계산할 텍스트, 최대 너비, 최대 높이)
    Vector2 textSize = textTMP.GetPreferredValues(newText, maxWidth - padding * 2, Mathf.Infinity);
        
    // Text Body 크기 조정
    UpdateTextBodySize(textSize);
  }

  /// <summary>
  /// 텍스트 크기에 따라 TMP, Text Body 크기 조정
  /// </summary>
  private void UpdateTextBodySize(Vector2 textSize)
  {
    float newWidth = Mathf.Min(textSize.x + padding * 2, maxWidth);
    float newHeight = initialHeight;
        
    // width가 maxWidth를 넘어가면 height를 증가
    if (textSize.x + padding * 2 > maxWidth)
    {
      // 필요한 줄 수 계산
      int additionalLines = Mathf.CeilToInt((textSize.x + padding * 2 - maxWidth) / maxWidth);
      newHeight = initialHeight + (additionalLines * heightIncrement);
    }
    else
    {
      // 텍스트 높이에 맞춰 조정 (최소 높이는 initialHeight)
      newHeight = Mathf.Max(textSize.y + padding * 2, initialHeight);
    }
        
    // RectTransform 크기 적용
    textBody.sizeDelta = new Vector2(newWidth, newHeight);
        
    Debug.Log($"Text Body 업데이트 - Width: {newWidth}, Height: {newHeight}");
  }
    
  /// <summary>
  /// 텍스트 초기화
  /// </summary>
  public void ClearText()
  {
    UpdateText("");
    textBody.sizeDelta = new Vector2(textBody.sizeDelta.x, initialHeight);
  }
}


