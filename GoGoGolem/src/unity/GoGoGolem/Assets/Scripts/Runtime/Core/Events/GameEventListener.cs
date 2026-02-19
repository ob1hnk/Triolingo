using UnityEngine;
using UnityEngine.Events;

public class GameEventListener : MonoBehaviour
{
    [SerializeField] private GameEvent gameEvent;
    [SerializeField] private UnityEvent response;

    private void OnEnable()
    {
        gameEvent?.Register(this);
    }

    private void OnDisable()
    {
        gameEvent?.Unregister(this);
    }

    public void OnEventRaised()
    {
        response?.Invoke();
    }
}
