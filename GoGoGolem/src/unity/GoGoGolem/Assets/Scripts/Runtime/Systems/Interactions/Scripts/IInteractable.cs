public interface IInteractable
{
    string GetInteractText(); // 화면에 표시할 메시지 (예: "대화하기", "줍기")
    void Interact();          // 실제 실행될 기능
}
