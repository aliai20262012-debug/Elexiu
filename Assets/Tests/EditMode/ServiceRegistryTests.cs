using System;
using NUnit.Framework;
using Nexus.Core;

namespace Nexus.Tests.EditMode
{
    public class ServiceRegistryTests
    {
        private interface IDummy
        {
            int Value { get; }
        }

        private sealed class Dummy : IDummy, IDisposable
        {
            public int Value
            {
                get { return 7; }
            }

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        [Test]
        public void RegisterAndResolve_ReturnsSameInstance()
        {
            ServiceRegistry registry = new ServiceRegistry();
            Dummy dummy = new Dummy();
            registry.Register<IDummy>(dummy);

            IDummy resolved = registry.Get<IDummy>();

            Assert.AreSame(dummy, resolved);
            Assert.IsTrue(registry.IsRegistered<IDummy>());
            Assert.AreEqual(1, registry.Count);
        }

        [Test]
        public void Register_Duplicate_Throws()
        {
            ServiceRegistry registry = new ServiceRegistry();
            registry.Register<IDummy>(new Dummy());

            Assert.Throws<InvalidOperationException>(() => registry.Register<IDummy>(new Dummy()));
        }

        [Test]
        public void Register_Null_Throws()
        {
            ServiceRegistry registry = new ServiceRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Register<IDummy>(null));
        }

        [Test]
        public void Get_Unregistered_Throws()
        {
            ServiceRegistry registry = new ServiceRegistry();
            Assert.Throws<ServiceNotRegisteredException>(() => registry.Get<IDummy>());
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalse()
        {
            ServiceRegistry registry = new ServiceRegistry();
            IDummy resolved;

            Assert.IsFalse(registry.TryGet(out resolved));
            Assert.IsNull(resolved);
        }

        [Test]
        public void Dispose_DisposesRegisteredDisposables()
        {
            ServiceRegistry registry = new ServiceRegistry();
            Dummy dummy = new Dummy();
            registry.Register<IDummy>(dummy);

            registry.Dispose();

            Assert.IsTrue(dummy.IsDisposed);
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void AppContext_InstallAndUninstall_IsIdempotent()
        {
            ServiceRegistry registry = new ServiceRegistry();

            AppContext.Install(registry);
            Assert.IsTrue(AppContext.IsReady);

            AppContext.Uninstall();
            Assert.IsFalse(AppContext.IsReady);
            Assert.IsFalse(AppContext.TryGet<IDummy>(out _));
        }
    }
}
