using System;

namespace Nexus.Core.Events
{
    /// <summary>
    /// Decoupled publish/subscribe channel used to communicate between systems
    /// (simulation, presentation, persistence) without direct references.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent;

        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameEvent;

        void Publish<TEvent>(TEvent gameEvent) where TEvent : struct, IGameEvent;

        void Clear();
    }
}
