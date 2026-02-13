using UnityEngine;
using UI.Presenters;

/// <summary>
/// 탁상 상호작용 - 편지 쓰기
///
/// 사용법:
///   1. 탁상 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Inspector에서 letterWritePresenter 연결
///   3. 오브젝트의 Layer를 Interactable로 설정
///   4. 플레이어가 접근하면 "편지 쓰기 (E)" 표시 → E키로 편지 UI 열림
/// </summary>
public class LetterDesk : MonoBehaviour, IInteractable
{
    [SerializeField] private LetterWritePresenter letterWritePresenter;

    public string GetInteractText() => "편지 쓰기 (E)";

    public void Interact()
    {
        if (letterWritePresenter == null)
        {
            Debug.LogError("[LetterDesk] LetterWritePresenter가 연결되지 않았습니다.");
            return;
        }

        letterWritePresenter.Open();
    }
}
