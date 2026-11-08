using NUnit.Framework;
using Nexus.Core.State;

namespace Nexus.Tests.EditMode
{
    public class GameStateMachineTests
    {
        [Test]
        public void NewMachine_StartsUninitialized()
        {
            GameStateMachine machine = new GameStateMachine();
            Assert.AreEqual(GameState.Uninitialized, machine.CurrentState);
        }

        [Test]
        public void BootSequence_ReachesGameplay()
        {
            GameStateMachine machine = new GameStateMachine();

            machine.TransitionTo(GameState.Booting);
            machine.TransitionTo(GameState.MainMenu);
            machine.TransitionTo(GameState.LoadingGameplay);
            machine.TransitionTo(GameState.Gameplay);

            Assert.AreEqual(GameState.Gameplay, machine.CurrentState);
        }

        [Test]
        public void IllegalTransition_Throws()
        {
            GameStateMachine machine = new GameStateMachine();

            Assert.Throws<InvalidStateTransitionException>(() => machine.TransitionTo(GameState.Gameplay));
            Assert.AreEqual(GameState.Uninitialized, machine.CurrentState, "A rejected transition must not change the state.");
        }

        [Test]
        public void TryTransitionTo_ReturnsFalse_ForIllegalTarget()
        {
            GameStateMachine machine = new GameStateMachine();
            machine.TransitionTo(GameState.Booting);

            Assert.IsFalse(machine.TryTransitionTo(GameState.Paused));
            Assert.AreEqual(GameState.Booting, machine.CurrentState);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndCurrentState()
        {
            GameStateMachine machine = new GameStateMachine();
            GameState reportedPrevious = GameState.Uninitialized;
            GameState reportedCurrent = GameState.Uninitialized;
            int calls = 0;

            machine.StateChanged += (previous, current) =>
            {
                reportedPrevious = previous;
                reportedCurrent = current;
                calls++;
            };

            machine.TransitionTo(GameState.Booting);

            Assert.AreEqual(1, calls);
            Assert.AreEqual(GameState.Uninitialized, reportedPrevious);
            Assert.AreEqual(GameState.Booting, reportedCurrent);
        }

        [Test]
        public void ShuttingDown_IsTerminal()
        {
            GameStateMachine machine = new GameStateMachine();
            machine.TransitionTo(GameState.Booting);
            machine.TransitionTo(GameState.ShuttingDown);

            Assert.IsFalse(machine.CanTransitionTo(GameState.MainMenu));
            Assert.IsFalse(machine.CanTransitionTo(GameState.Booting));
        }
    }
}
