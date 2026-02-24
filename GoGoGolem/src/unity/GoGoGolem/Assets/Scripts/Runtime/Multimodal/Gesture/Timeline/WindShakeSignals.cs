// WindShakeSignals.cs
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.Playables;

namespace Demo.GestureDetection
{
    [System.Serializable]
    public class WindShakeStartSignal : ScriptableObject, INotification
    {
        public PropertyName id => new PropertyName("WindShakeStart");
    }

    [System.Serializable]
    public class WindShakeStopSignal : ScriptableObject, INotification
    {
        public PropertyName id => new PropertyName("WindShakeStop");
    }
}