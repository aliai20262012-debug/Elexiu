using System;
using NUnit.Framework;
using Nexus.Core.Events;

namespace Nexus.Tests.EditMode
{
    public class EventBusTests
    {
        private readonly struct TestEvent : IGameEvent
        {
            public readonly int Value;

            public TestEvent(int value)
            {
                Value = value;
            }
        }

        private readonly struct OtherEvent : IGameEvent
        {
        }

        [Test]
        public void Publish_DeliversToSubscriber()
        {
            EventBus bus = new EventBus();
            int received = 0;

            bus.Subscribe<TestEvent>(gameEvent => received = gameEvent.Value);
            bus.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Publish_WithoutSubscribers_DoesNothing()
        {
            EventBus bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Publish(new OtherEvent()));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            EventBus bus = new EventBus();
            int received = 0;
            Action<TestEvent> handler = gameEvent => received++;

            bus.Subscribe(handler);
            bus.Publish(new TestEvent(1));
            bus.Unsubscribe(handler);
            bus.Publish(new TestEvent(2));

            Assert.AreEqual(1, received);
        }

        [Test]
        public void Subscribe_SameHandlerTwice_DeliversOnce()
        {
            EventBus bus = new EventBus();
            int received = 0;
            Action<TestEvent> handler = gameEvent => received++;

            bus.Subscribe(handler);
            bus.Subscribe(handler);
            bus.Publish(new TestEvent(1));

            Assert.AreEqual(1, received);
        }

        [Test]
        public void FailingHandler_DoesNotStopOtherHandlers()
        {
            EventBus bus = new EventBus();
            bool secondHandlerRan = false;

            bus.Subscribe<TestEvent>(gameEvent => throw new InvalidOperationException("boom"));
            bus.Subscribe<TestEvent>(gameEvent => secondHandlerRan = true);

            Assert.DoesNotThrow(() => bus.Publish(new TestEvent(1)));
            Assert.IsTrue(secondHandlerRan);
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            EventBus bus = new EventBus();
            int received = 0;

            bus.Subscribe<TestEvent>(gameEvent => received++);
            bus.Clear();
            bus.Publish(new TestEvent(1));

            Assert.AreEqual(0, received);
        }

        [Test]
        public void Subscribe_NullHandler_Throws()
        {
            EventBus bus = new EventBus();
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe<TestEvent>(null));
        }
    }
}
