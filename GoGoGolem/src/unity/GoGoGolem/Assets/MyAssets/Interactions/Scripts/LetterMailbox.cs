using UnityEngine;
using UI.Presenters;

/// <summary>
/// 우편함 상호작용 - 답장 편지 읽기
///
/// 사용법:
///   1. 우편함 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Inspector에서 letterReadPresenter 연결
///   3. 오브젝트의 Layer를 Interactable로 설정
///   4. 플레이어가 접근하면 "편지 확인하기 (E)" 표시 → E키로 답장 UI 열림
/// </summary>
public class LetterMailbox : MonoBehaviour, IInteractable
{
    [SerializeField] private LetterReadPresenter letterReadPresenter;

    public string GetInteractText() => "편지 확인하기 (E)";

    public void Interact()
    {
        if (letterReadPresenter == null)
        {
            Debug.LogError("[LetterMailbox] LetterReadPresenter가 연결되지 않았습니다.");
            return;
        }

        letterReadPresenter.Open();
    }
}
