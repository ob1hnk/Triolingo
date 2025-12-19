using UnityEngine;
using TMPro;
using System.Collections;
public class TextUIController : MonoBehaviour
{
  [Header("3D 말풍선 References")]
  [SerializeField] private TextMeshProUGUI textTMP;     // Speechbubble_0002/Text Body/Text (TMP) - Canvas 안의 TextMeshProUGUI
  [SerializeField] private RectTransform textBody;       // Speechbubble_0002/Text Body - Canvas 안의 RectTransform (배경 Image 포함)
  [SerializeField] private Transform speechBubbleBg;     // Speechbubble_0002 - 배경 SpriteRenderer가 있는 GameObject (선택사항)

  [Header("Text Body Settings")]
  [SerializeField] private float maxWidth = 40f;      // 줄 넘어가는 길이
  [SerializeField] private float heightIncrement = 50f; // 줄 넘어갔을 때 증가하는 말풍선 높이
  [SerializeField] private float initialHeight = 100f;  // 초기 말풍선 높이
  [SerializeField] private float padding = 20f;         // 텍스트와 배경 사이 여백
  
  [Header("배경 스케일 설정 (선택사항)")]
  [SerializeField] private bool adjustBackgroundScale = false;  // Speechbubble_0002 배경 크기도 조정할지 여부
  [SerializeField] private float backgroundScaleFactor = 1f;    // 배경 크기 조정 팩터

  [Header("표시/숨김 설정")]
  [SerializeField] private string defaultText = "안녕하세요?";   // 게임 시작 시 기본 텍스트
  [SerializeField] private float visibleDuration = 5f;          // 말풍선 표시 시간(초)

  private float currentHeight;
  private Vector3 initialBackgroundScale;
  private Coroutine hideCoroutine;

  void Start()
  {
    // 3D 말풍선 컴포넌트 연결 확인
    if (textTMP == null)
    {
      Debug.LogError("TextMeshProUGUI 컴포넌트 미할당 - Speechbubble_0002/Text Body/Text (TMP)를 할당해주세요");
    }
    else
    {
      // Canvas 확인 (수동 생성/구성 필요)
      Canvas canvas = textTMP.GetComponentInParent<Canvas>();
      if (canvas == null)
      {
        Debug.LogError("TextMeshProUGUI가 Canvas 안에 없습니다. World Space Canvas를 수동으로 생성/배치해주세요.");
      }
      else if (canvas.renderMode != RenderMode.WorldSpace)
      {
        Debug.LogWarning($"Canvas Render Mode가 {canvas.renderMode}입니다. World Space로 설정하는 것을 권장합니다.");
      }
      else
      {
        Debug.Log($"Canvas 확인 완료 - Render Mode: {canvas.renderMode}");
      }
    }
        
    if (textBody == null)
    {
      Debug.LogError("Text Body RectTransform 컴포넌트 미할당 - Speechbubble_0002/Text Body를 할당해주세요");
    }
    
    // 배경 Transform 초기 스케일 저장
    if (speechBubbleBg != null)
    {
      initialBackgroundScale = speechBubbleBg.localScale;
    }
    
    currentHeight = initialHeight;

    // 시작 시 기본 텍스트 표시 후 자동 숨김
    if (!string.IsNullOrEmpty(defaultText))
    {
      ShowText(defaultText);
    }
    else
    {
      // 기본 텍스트가 비어 있으면 말풍선 비활성화
      SetBubbleActive(false);
    }
  }

  /// <summary>
  /// WebSocket으로부터 받은 텍스트로 3D 말풍선 업데이트
  /// </summary>
  public void UpdateText(string newText)
  {
    if (textTMP == null || textBody == null)
    {
      Debug.LogError("말풍선 컴포넌트가 제대로 할당되지 않음");
      return;
    }

    ShowText(newText);
  }

  /// <summary>
  /// 텍스트 크기에 따라 말풍선 Text Body 크기 조정 (3D 공간에서 캐릭터와 함께 움직임)
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
        
    // RectTransform 크기 적용 (World Space Canvas에서도 작동)
    textBody.sizeDelta = new Vector2(newWidth, newHeight);
    
    // Speechbubble_0002 배경 크기도 조정 (선택사항)
    // 주의: Text Body의 sizeDelta는 World Space Canvas에서 픽셀 단위로 작동합니다.
    // 배경 스케일 조정이 필요한 경우 Canvas의 스케일을 고려하여 조정해야 합니다.
    if (adjustBackgroundScale && speechBubbleBg != null)
    {
      Canvas canvas = textBody.GetComponentInParent<Canvas>();
      if (canvas != null)
      {
        // Text Body 크기에 비례하여 배경 크기 조정
        // 초기 크기 대비 비율 계산
        float widthRatio = newWidth / initialHeight;
        float heightRatio = newHeight / initialHeight;
        
        // 배경 스케일 조정 (Y축은 유지, X와 Z만 조정)
        speechBubbleBg.localScale = new Vector3(
          initialBackgroundScale.x * widthRatio * backgroundScaleFactor,
          initialBackgroundScale.y,
          initialBackgroundScale.z * heightRatio * backgroundScaleFactor
        );
      }
    }
        
    Debug.Log($"Text Body 업데이트 - Width: {newWidth}, Height: {newHeight}");
  }
    
  /// <summary>
  /// 텍스트 초기화
  /// </summary>
  public void ClearText()
  {
    // 코루틴 중지 및 말풍선 숨김
    if (hideCoroutine != null)
    {
      StopCoroutine(hideCoroutine);
      hideCoroutine = null;
    }

    textTMP.text = "";
    textBody.sizeDelta = new Vector2(textBody.sizeDelta.x, initialHeight);
    SetBubbleActive(false);
  }

  /// <summary>
  /// 텍스트를 설정하고 말풍선을 표시한 뒤 일정 시간 후 자동으로 숨김
  /// </summary>
  /// <param name="newText">표시할 텍스트</param>
  private void ShowText(string newText)
  {
    SetBubbleActive(true);

    // 텍스트 업데이트
    textTMP.text = newText;
        
    // 텍스트 렌더링 강제 업데이트 (Text Body 조절 위해)
    Canvas.ForceUpdateCanvases();
    textTMP.ForceMeshUpdate();
        
    // 텍스트의 실제 크기 계산 (계산할 텍스트, 최대 너비, 최대 높이)
    Vector2 textSize = textTMP.GetPreferredValues(newText, maxWidth - padding * 2, Mathf.Infinity);
        
    // Text Body 크기 조정
    UpdateTextBodySize(textSize);

    // 기존 코루틴이 있으면 중지
    if (hideCoroutine != null)
    {
      StopCoroutine(hideCoroutine);
    }

    // 일정 시간 후 말풍선 숨김
    hideCoroutine = StartCoroutine(HideAfterDelay(visibleDuration));
  }

  /// <summary>
  /// 말풍선과 텍스트를 켜고/끄는 헬퍼
  /// </summary>
  private void SetBubbleActive(bool isActive)
  {
    if (textBody != null)
    {
      textBody.gameObject.SetActive(isActive);
    }

    if (speechBubbleBg != null)
    {
      speechBubbleBg.gameObject.SetActive(isActive);
    }
  }

  /// <summary>
  /// 지정된 시간 후 말풍선을 숨김
  /// </summary>
  private IEnumerator HideAfterDelay(float seconds)
  {
    yield return new WaitForSeconds(seconds);
    SetBubbleActive(false);
    hideCoroutine = null;
  }
}


