using UnityEngine;

public class NPC : MonoBehaviour, IInteractable
{
    public string GetInteractText() => "대화하기 (E)";

    public void Interact()
    {
        Debug.Log("NPC와 대화를 시작합니다: '못보던 얼굴인데. 누구시죠?'");
    }
}