using System;
using UnityEngine;

/// <summary>
/// 침대 상호작용 - 잠들기
///
/// 사용법:
///   1. Bed 오브젝트에 이 컴포넌트 + Collider 추가
///   2. Layer를 Interactable로 설정
///   3. RoomStateManager가 OnSlept 이벤트를 구독하여 학 귀환 애니메이션 트리거
///
/// 초기 상태: Collider는 비활성(낮에는 잠들기 불가)
/// RoomStateManager가 낮→밤 전환 후 Collider를 활성화
/// </summary>
public class BedInteraction : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] private InteractionPromptData promptData;

    public event Action OnSlept;

    public InteractionType InteractionType => InteractionType.Sleep;
    public string GetActionLabel() => promptData != null ? promptData.ActionLabel : "자러가기";
    public Sprite GetKeyHintSprite() => promptData != null ? promptData.KeyHintSprite : null;

    public void Interact()
    {
        Debug.Log("[BedInteraction] 자러가기 시작");
        OnSlept?.Invoke();
    }
}
