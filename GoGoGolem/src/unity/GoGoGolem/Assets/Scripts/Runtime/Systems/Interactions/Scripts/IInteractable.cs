public enum InteractionType
{
    Gather, // 아이템 줍기
    Talk,   // NPC 대화
}

public interface IInteractable
{
    InteractionType InteractionType { get; }
    string GetInteractText();
    void Interact();
}
