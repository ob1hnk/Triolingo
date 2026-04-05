using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Room 씬 테스트용 UI 버튼 핸들러.
/// 각 public 메서드를 Button OnClick()에 연결하여 사용.
/// </summary>
public class RoomTestButtons : MonoBehaviour
{
    [SerializeField] private PlayableDirector afterLetterTimeline;
    [SerializeField] private BedInteraction bedInteraction;

    /// <summary>AfterLetter 타임라인 실행 (Button OnClick에 연결)</summary>
    public void OnClickAfterLetterTimeline()
    {
        Debug.Log("[RoomTestButtons] AfterLetter 타임라인 실행");
        if (afterLetterTimeline != null)
            afterLetterTimeline.Play();
        else
            Debug.LogWarning("[RoomTestButtons] afterLetterTimeline이 연결되지 않았습니다.");
    }

    /// <summary>Sleep 타임라인 실행 (Button OnClick에 연결)</summary>
    public void OnClickSleepTimeline()
    {
        Debug.Log("[RoomTestButtons] Sleep 타임라인 실행");
        if (bedInteraction != null)
            bedInteraction.Interact();
        else
            Debug.LogWarning("[RoomTestButtons] bedInteraction이 연결되지 않았습니다.");
    }
}
