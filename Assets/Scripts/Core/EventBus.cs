using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>
    /// Tiny typed publish/subscribe bus for decoupling modules (combat to audio/VFX/UI, etc.).
    /// Events are structs; handlers are plain delegates. Subscribers must unsubscribe in OnDisable.
    /// Raising iterates backwards so a handler may safely unsubscribe itself.
    /// </summary>
    public static class EventBus<T> where T : struct
    {
        static readonly List<Action<T>> Handlers = new List<Action<T>>();

        static EventBus() => EventBusRegistry.Register(Handlers.Clear);

        public static void Subscribe(Action<T> handler)
        {
            if (handler != null && !Handlers.Contains(handler)) Handlers.Add(handler);
        }

        public static void Unsubscribe(Action<T> handler) => Handlers.Remove(handler);

        public static void Raise(in T evt)
        {
            for (int i = Handlers.Count - 1; i >= 0; i--)
            {
                if (i >= Handlers.Count) continue;
                try { Handlers[i](evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static int SubscriberCount => Handlers.Count;
    }

    /// <summary>Clears every bus when entering Play Mode (domain reload may be disabled).</summary>
    public static class EventBusRegistry
    {
        static readonly List<Action> Clearers = new List<Action>();

        public static void Register(Action clear) => Clearers.Add(clear);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearAll()
        {
            foreach (var c in Clearers) c();
        }
    }
}
