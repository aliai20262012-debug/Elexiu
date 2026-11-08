using System;

namespace Nexus.Core.State
{
    public sealed class InvalidStateTransitionException : Exception
    {
        public GameState From { get; }

        public GameState To { get; }

        public InvalidStateTransitionException(GameState from, GameState to)
            : base("Invalid game state transition: " + from + " -> " + to + ".")
        {
            From = from;
            To = to;
        }
    }
}
