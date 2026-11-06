namespace Nexus.Core.State
{
    /// <summary>
    /// High level application states. Gameplay specific sub-states (screens,
    /// overlays) belong to the gameplay layer, not to the application state machine.
    /// </summary>
    public enum GameState
    {
        Uninitialized = 0,
        Booting = 1,
        MainMenu = 2,
        LoadingGameplay = 3,
        Gameplay = 4,
        Paused = 5,
        ReturningToMenu = 6,
        ShuttingDown = 7,
        Failed = 8
    }
}
