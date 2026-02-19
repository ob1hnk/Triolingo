using UnityEngine;
using UnityEngine.Events;

public class GameEventListener<T> : MonoBehaviour
{
    [SerializeField] private GameEvent<T> gameEvent;
    [SerializeField] private UnityEvent<T> response;

    private void OnEnable()
    {
        gameEvent?.Register(this);
    }

    private void OnDisable()
    {
        gameEvent?.Unregister(this);
    }

    public void OnEventRaised(T value)
    {
        response?.Invoke(value);
    }
}
