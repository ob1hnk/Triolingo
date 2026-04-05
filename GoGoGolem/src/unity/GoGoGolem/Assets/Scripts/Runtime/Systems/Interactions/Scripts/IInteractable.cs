using UnityEngine;

public enum InteractionType
{
    Gather,       // 아이템 줍기
    TalkNPC,      // NPC 대화
    TalkGolem,    // 골렘 대화
    WriteLetter,  // 편지 쓰기 / 읽기
    Sleep,        // 잠들기
    UseWindSkill, // 배치된 아이템에 바람 스킬 시전
    ChangeScene, // 씬 전환
}

public interface IInteractable
{
    InteractionType InteractionType { get; }
    bool CanInteract { get; }
    string GetActionLabel();
    Sprite GetKeyHintSprite();
    Vector3 GetPromptOffset();
    void Interact();
}
