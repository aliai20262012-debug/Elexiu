using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.Planet
{
    /// <summary>
    /// Makes sure the planet is lit even in a bare scene: a warm key light (the local star),
    /// a cold cyan rim light for the sci-fi identity, and a flat ambient floor so the night
    /// side stays readable instead of pitch black.
    ///
    /// Existing scene lights are reused, never duplicated.
    /// </summary>
    public static class PlanetLighting
    {
        public const string SunName = "Nexus Sun";
        public const string RimName = "Nexus Rim Light";

        private static readonly Color SunColor = new Color(1.0f, 0.972f, 0.925f, 1f);
        private static readonly Color RimColor = new Color(0.133f, 0.827f, 0.933f, 1f);
        private static readonly Color AmbientFloor = new Color(0.055f, 0.078f, 0.115f, 1f);

        /// <summary>Ensures key/rim lights and ambient settings exist. Returns the key light.</summary>
        public static Light EnsureLighting()
        {
            Light key = FindDirectionalLight();
            if (key == null)
            {
                key = CreateDirectional(SunName, SunColor, 1.15f, Quaternion.Euler(28f, -38f, 0f));
            }

            EnsureRimLight();
            ApplyAmbient();
            return key;
        }

        /// <summary>Removes the lights this helper created (scene teardown / tests).</summary>
        public static void ReleaseOwnedLights()
        {
            DestroyIfPresent(SunName);
            DestroyIfPresent(RimName);
        }

        public static void ApplyAmbient()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientFloor;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
        }

        private static void EnsureRimLight()
        {
            Light rim = FindByName(RimName);
            if (rim == null)
            {
                rim = CreateDirectional(RimName, RimColor, 0.28f, Quaternion.Euler(-18f, 140f, 0f));
            }

            rim.shadows = LightShadows.None;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional && lights[i].isActiveAndEnabled &&
                    lights[i].name != RimName)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private static Light FindByName(string name)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].name == name)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private static Light CreateDirectional(string name, Color color, float intensity, Quaternion rotation)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.rotation = rotation;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            return light;
        }

        private static void DestroyIfPresent(string name)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null || lights[i].name != name)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(lights[i].gameObject);
                }
                else
                {
                    Object.DestroyImmediate(lights[i].gameObject);
                }
            }
        }
    }
}
