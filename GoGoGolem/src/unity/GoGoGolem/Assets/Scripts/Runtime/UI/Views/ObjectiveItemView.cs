using UnityEngine;
using TMPro;

public class ObjectiveItemView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI objectiveText;
    
    private string objectiveId;
    private bool isCompleted;
    
    // 색상 설정
    private Color completedColor = new Color(0.47f, 0.47f, 0.47f); // 어두운 회색
    private Color incompleteColor = new Color(0.86f, 0.86f, 0.86f); // 밝은 회색
    
    /// <summary>
    /// 목표 초기화
    /// </summary>
    public void Initialize(string objectiveId, string text, bool completed)
    {
        this.objectiveId = objectiveId;
        this.isCompleted = completed;
        
        objectiveText.text = text;
        UpdateVisual();
    }
    
    /// <summary>
    /// 완료 상태 설정
    /// </summary>
    public void SetCompleted(bool completed)
    {
        isCompleted = completed;
        UpdateVisual();
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
    
    /// <summary>
    /// 시각적 업데이트
    /// </summary>
    private void UpdateVisual()
    {
        if (isCompleted)
        {
            // 완료: 어두운 색 + 취소선
            objectiveText.color = completedColor;
            objectiveText.fontStyle = FontStyles.Strikethrough;
        }
        else
        {
            // 미완료: 밝은 색
            objectiveText.color = incompleteColor;
            objectiveText.fontStyle = FontStyles.Normal;
        }
    }
}