using System;
using System.Collections.Generic;
using Nexus.Core.Logging;

namespace Nexus.Core
{
    /// <summary>
    /// Minimal, explicit dependency container. Services are registered once by the
    /// composition root and resolved by abstraction (interface) afterwards.
    /// No singletons, no reflection, no service location hidden inside gameplay code.
    /// </summary>
    public sealed class ServiceRegistry : IDisposable
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<object> _registrationOrder = new List<object>();
        private bool _disposed;

        public int Count
        {
            get { return _services.Count; }
        }

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            Type key = typeof(TService);
            if (_services.ContainsKey(key))
            {
                throw new InvalidOperationException("Service " + key.Name + " is already registered.");
            }

            _services.Add(key, service);
            _registrationOrder.Add(service);
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            object value;
            if (_services.TryGetValue(typeof(TService), out value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }

        public TService Get<TService>() where TService : class
        {
            TService service;
            if (TryGet(out service))
            {
                return service;
            }

            throw new ServiceNotRegisteredException(typeof(TService));
        }

        public bool IsRegistered<TService>() where TService : class
        {
            return _services.ContainsKey(typeof(TService));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Dispose in reverse registration order so dependants die before their dependencies.
            for (int i = _registrationOrder.Count - 1; i >= 0; i--)
            {
                IDisposable disposable = _registrationOrder[i] as IDisposable;
                if (disposable == null)
                {
                    continue;
                }

                try
                {
                    disposable.Dispose();
                }
                catch (Exception exception)
                {
                    GameLog.Exception(exception, "Failed to dispose service " + _registrationOrder[i].GetType().Name + ".");
                }
            }

            _registrationOrder.Clear();
            _services.Clear();
        }
    }

    public sealed class ServiceNotRegisteredException : Exception
    {
        public ServiceNotRegisteredException(Type serviceType)
            : base("No service is registered for " + (serviceType != null ? serviceType.Name : "<null>") + ".")
        {
        }
    }
}
