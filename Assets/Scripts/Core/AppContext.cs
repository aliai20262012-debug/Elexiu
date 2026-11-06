using System;
using UnityEngine;

namespace Nexus.Core
{
    /// <summary>
    /// The single intentional static access point of the application.
    /// The bootstrap scene installs the service registry here; scenes and views
    /// resolve their dependencies from it instead of using hard coded singletons.
    /// Everything else in the codebase is instance based and constructor injected.
    /// </summary>
    public static class AppContext
    {
        public static ServiceRegistry Services { get; private set; }

        public static bool IsReady
        {
            get { return Services != null; }
        }

        public static void Install(ServiceRegistry services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (Services != null && !ReferenceEquals(Services, services))
            {
                throw new InvalidOperationException("Application services are already installed. Call AppContext.Uninstall() first.");
            }

            Services = services;
        }

        public static void Uninstall()
        {
            ServiceRegistry services = Services;
            Services = null;

            if (services != null)
            {
                services.Dispose();
            }
        }

        public static bool TryGet<TService>(out TService service) where TService : class
        {
            ServiceRegistry services = Services;
            if (services != null)
            {
                return services.TryGet(out service);
            }

            service = null;
            return false;
        }

        public static TService Get<TService>() where TService : class
        {
            ServiceRegistry services = Services;
            if (services == null)
            {
                throw new InvalidOperationException("Application services have not been installed yet.");
            }

            return services.Get<TService>();
        }

        /// <summary>
        /// Guards against stale statics when "Enter Play Mode Options" is ever configured
        /// without a domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticsForPlayMode()
        {
            Services = null;
        }
    }
}
