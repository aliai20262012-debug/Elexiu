using System.Collections.Generic;
using Nexus.Configuration;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Creates runtime configuration instances for tests and destroys them afterwards so the
    /// editor does not leak ScriptableObjects between test runs.
    /// </summary>
    internal static class TestAssets
    {
        private static readonly List<Object> Created = new List<Object>();

        public static GameConfiguration CreateConfiguration()
        {
            GameConfiguration configuration = GameConfiguration.CreateDefault();
            Created.Add(configuration);
            return configuration;
        }

        public static UiTheme CreateTheme()
        {
            UiTheme theme = UiTheme.CreateDefault();
            Created.Add(theme);
            return theme;
        }

        public static IConfigurationService CreateConfigurationService()
        {
            return new ConfigurationService(CreateConfiguration(), CreateTheme());
        }

        public static void DestroyAll()
        {
            for (int i = 0; i < Created.Count; i++)
            {
                if (Created[i] != null)
                {
                    Object.DestroyImmediate(Created[i]);
                }
            }

            Created.Clear();
        }
    }
}
