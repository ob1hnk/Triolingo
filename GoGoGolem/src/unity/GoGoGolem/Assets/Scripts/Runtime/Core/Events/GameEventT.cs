using System;
using System.Collections.Generic;
using UnityEngine;

public class GameEvent<T> : ScriptableObject
{
    private readonly List<GameEventListener<T>> listeners = new List<GameEventListener<T>>();
    private readonly List<Action<T>> codeListeners = new List<Action<T>>();

    public void Raise(T value)
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            listeners[i].OnEventRaised(value);
        }

        for (int i = codeListeners.Count - 1; i >= 0; i--)
        {
            codeListeners[i].Invoke(value);
        }
    }

    public void Register(GameEventListener<T> listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(GameEventListener<T> listener)
    {
        listeners.Remove(listener);
    }

    public void Register(Action<T> callback)
    {
        if (!codeListeners.Contains(callback))
            codeListeners.Add(callback);
    }

    public void Unregister(Action<T> callback)
    {
        codeListeners.Remove(callback);
    }
}
