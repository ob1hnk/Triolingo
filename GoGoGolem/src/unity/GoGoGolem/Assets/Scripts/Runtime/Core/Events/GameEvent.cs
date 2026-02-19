using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Events/Game Event (Void)")]
public class GameEvent : ScriptableObject
{
    private readonly List<GameEventListener> listeners = new List<GameEventListener>();
    private readonly List<Action> codeListeners = new List<Action>();

    public void Raise()
    {
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            listeners[i].OnEventRaised();
        }

        for (int i = codeListeners.Count - 1; i >= 0; i--)
        {
            codeListeners[i].Invoke();
        }
    }

    public void Register(GameEventListener listener)
    {
        if (!listeners.Contains(listener))
            listeners.Add(listener);
    }

    public void Unregister(GameEventListener listener)
    {
        listeners.Remove(listener);
    }

    public void Register(Action callback)
    {
        if (!codeListeners.Contains(callback))
            codeListeners.Add(callback);
    }

    public void Unregister(Action callback)
    {
        codeListeners.Remove(callback);
    }
}
