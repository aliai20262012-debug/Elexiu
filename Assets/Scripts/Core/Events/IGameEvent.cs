namespace Nexus.Core.Events
{
    /// <summary>
    /// Marker interface for all application events. Events are value types so that
    /// publishing never allocates and handlers can never mutate shared state.
    /// </summary>
    public interface IGameEvent
    {
    }
}
