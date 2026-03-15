using UnityEngine;
using UI.Presenters;

/// <summary>
/// 책상 상호작용 - 편지 쓰기 / 편지 읽기 (낮/밤 상태에 따라 전환)
///
/// 사용법:
///   1. Desk 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Layer를 Interactable로 설정
///   3. Inspector에서 letterWritePresenter, letterReadPresenter 연결
///   4. RoomStateManager.HandleFlyInComplete()에서 SetMode(DeskMode.Read) 호출
/// </summary>
public class LetterDesk : MonoBehaviour, IInteractable
{
    public enum DeskMode { Write, Read }

    [SerializeField] private LetterWritePresenter letterWritePresenter;
    [SerializeField] private LetterReadPresenter  letterReadPresenter;

    private DeskMode _mode = DeskMode.Write;

    public void SetMode(DeskMode mode) => _mode = mode;

    public string GetInteractText() =>
        _mode == DeskMode.Write ? "편지 쓰기 (E)" : "편지 읽기 (E)";

    public void Interact()
    {
        if (_mode == DeskMode.Write)
        {
            if (letterWritePresenter == null)
            {
                Debug.LogError("[LetterDesk] LetterWritePresenter가 연결되지 않았습니다.");
                return;
            }
            letterWritePresenter.Open();
        }
        else
        {
            if (letterReadPresenter == null)
            {
                Debug.LogError("[LetterDesk] LetterReadPresenter가 연결되지 않았습니다.");
                return;
            }
            letterReadPresenter.Open();
        }
    }
}
