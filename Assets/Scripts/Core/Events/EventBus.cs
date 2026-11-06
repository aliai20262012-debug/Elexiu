using System;
using System.Collections.Generic;
using Nexus.Core.Logging;

namespace Nexus.Core.Events
{
    /// <summary>
    /// Default in-process event bus. A failing subscriber is logged and can never
    /// stop the remaining subscribers from running.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            List<Delegate> handlers;
            if (!_handlers.TryGetValue(typeof(TEvent), out handlers))
            {
                handlers = new List<Delegate>();
                _handlers.Add(typeof(TEvent), handlers);
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            List<Delegate> handlers;
            if (_handlers.TryGetValue(typeof(TEvent), out handlers))
            {
                handlers.Remove(handler);
            }
        }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent
        {
            List<Delegate> handlers;
            if (!_handlers.TryGetValue(typeof(TEvent), out handlers) || handlers.Count == 0)
            {
                return;
            }

            // Snapshot: handlers are allowed to subscribe/unsubscribe while reacting.
            Delegate[] snapshot = handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    ((Action<TEvent>)snapshot[i]).Invoke(gameEvent);
                }
                catch (Exception exception)
                {
                    GameLog.Exception(exception, "Event handler failed while handling " + typeof(TEvent).Name + ".");
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
