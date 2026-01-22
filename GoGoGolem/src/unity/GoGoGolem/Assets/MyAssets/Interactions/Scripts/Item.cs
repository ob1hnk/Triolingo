using UnityEngine;

public class Item : MonoBehaviour, IInteractable
{
    public string GetInteractText() => "줍기 (E)";

    public void Interact()
    {
        Debug.Log("아이템을 획득했습니다.");
        Destroy(gameObject); // 아이템을 줍고 사라지게 함
    }
}