using UnityEngine;

public enum InteractionType
{
    Gather,      // 아이템 줍기
    Talk,        // NPC 대화
    WriteLetter, // 편지 쓰기 / 읽기
    Sleep,       // 잠들기
}

public interface IInteractable
{
    InteractionType InteractionType { get; }
    string GetActionLabel();
    Sprite GetKeyHintSprite();
    void Interact();
}
