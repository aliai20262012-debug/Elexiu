using System;
using System.Collections.Generic;

namespace Nexus.Core.State
{
    /// <summary>
    /// Explicit, table driven state machine. Illegal transitions are rejected
    /// instead of silently corrupting the application flow.
    /// </summary>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> AllowedTransitions = BuildTransitions();

        public GameState CurrentState { get; private set; } = GameState.Uninitialized;

        /// <summary>Raised after a successful transition: (previous, current).</summary>
        public event Action<GameState, GameState> StateChanged;

        public bool CanTransitionTo(GameState target)
        {
            GameState[] allowed;
            if (!AllowedTransitions.TryGetValue(CurrentState, out allowed))
            {
                return false;
            }

            for (int i = 0; i < allowed.Length; i++)
            {
                if (allowed[i] == target)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Transitions when legal. Returns false (no exception) otherwise.</summary>
        public bool TryTransitionTo(GameState target)
        {
            if (!CanTransitionTo(target))
            {
                return false;
            }

            ApplyTransition(target);
            return true;
        }

        /// <summary>Transitions when legal, throws <see cref="InvalidStateTransitionException"/> otherwise.</summary>
        public void TransitionTo(GameState target)
        {
            if (!CanTransitionTo(target))
            {
                throw new InvalidStateTransitionException(CurrentState, target);
            }

            ApplyTransition(target);
        }

        public void Reset()
        {
            CurrentState = GameState.Uninitialized;
        }

        private void ApplyTransition(GameState target)
        {
            GameState previous = CurrentState;
            CurrentState = target;

            Action<GameState, GameState> handler = StateChanged;
            if (handler != null)
            {
                handler(previous, target);
            }
        }

        private static Dictionary<GameState, GameState[]> BuildTransitions()
        {
            return new Dictionary<GameState, GameState[]>
            {
                { GameState.Uninitialized, new[] { GameState.Booting, GameState.Failed } },
                { GameState.Booting, new[] { GameState.MainMenu, GameState.Failed, GameState.ShuttingDown } },
                { GameState.MainMenu, new[] { GameState.LoadingGameplay, GameState.ShuttingDown, GameState.Failed } },
                { GameState.LoadingGameplay, new[] { GameState.Gameplay, GameState.MainMenu, GameState.Failed } },
                { GameState.Gameplay, new[] { GameState.Paused, GameState.ReturningToMenu, GameState.ShuttingDown, GameState.Failed } },
                { GameState.Paused, new[] { GameState.Gameplay, GameState.ReturningToMenu, GameState.ShuttingDown, GameState.Failed } },
                { GameState.ReturningToMenu, new[] { GameState.MainMenu, GameState.Failed } },
                { GameState.Failed, new[] { GameState.Booting, GameState.ShuttingDown } },
                { GameState.ShuttingDown, new GameState[0] }
            };
        }
    }
}
