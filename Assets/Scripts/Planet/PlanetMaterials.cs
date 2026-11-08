using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.Planet
{
    /// <summary>
    /// Creates the runtime materials of the planet. Only stock URP shaders are used for the
    /// surface, the ocean and the region overlays; the single custom shader (a Fresnel
    /// atmosphere shell) is loaded from Resources and degrades to a stock Unlit material when
    /// it is missing, so a stripped build can never render a pink planet.
    /// </summary>
    public static class PlanetMaterials
    {
        public const string AtmosphereShaderResourcePath = "Shaders/PlanetAtmosphere";
        public const string AtmosphereShaderName = "Nexus/Planet Atmosphere";

        private static readonly Color OceanDeep = new Color(0.020f, 0.094f, 0.196f, 1f);
        private static readonly Color HighlightCyan = new Color(0.133f, 0.827f, 0.933f, 1f);
        private static readonly Color HighlightGreen = new Color(0.204f, 0.827f, 0.600f, 1f);

        /// <summary>Textured, physically lit land material.</summary>
        public static Material CreateLandMaterial(PlanetSurfaceTextures textures)
        {
            Material material = CreateLitMaterial("NexusPlanetLand");
            if (material == null)
            {
                return null;
            }

            if (textures != null && textures.IsValid)
            {
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", textures.Albedo);
                }

                material.mainTexture = textures.Albedo;

                if (material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", textures.Normal);
                    material.SetFloat("_BumpScale", 0.85f);
                    material.EnableKeyword("_NORMALMAP");
                }
            }

            SetColor(material, Color.white);
            SetFloat(material, "_Smoothness", 0.22f);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_SpecularHighlights", 1f);
            return material;
        }

        /// <summary>Flat, high-smoothness ocean so the sun produces a bright specular path.</summary>
        public static Material CreateOceanMaterial()
        {
            Material material = CreateLitMaterial("NexusPlanetOcean");
            if (material == null)
            {
                return null;
            }

            SetColor(material, OceanDeep);
            SetFloat(material, "_Smoothness", 0.92f);
            SetFloat(material, "_Metallic", 0.15f);
            SetFloat(material, "_SpecularHighlights", 1f);
            return material;
        }

        /// <summary>Fresnel atmosphere shell; falls back to a soft unlit haze.</summary>
        public static Material CreateAtmosphereMaterial(float density)
        {
            float clampedDensity = Mathf.Clamp(density, 0f, 2f);
            Shader shader = Resources.Load<Shader>(AtmosphereShaderResourcePath);
            if (shader == null)
            {
                shader = Shader.Find(AtmosphereShaderName);
            }

            Material material;
            if (shader != null)
            {
                material = new Material(shader)
                {
                    name = "NexusPlanetAtmosphere",
                    renderQueue = (int)RenderQueue.Transparent
                };
            }
            else
            {
                material = CreateOverlayMaterial("NexusPlanetAtmosphere", new Color(HighlightCyan.r, HighlightCyan.g, HighlightCyan.b, 0.06f));
                if (material == null)
                {
                    return null;
                }

                return material;
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", new Color(HighlightCyan.r, HighlightCyan.g, HighlightCyan.b, 1f));
            }

            if (material.HasProperty("_Intensity"))
            {
                material.SetFloat("_Intensity", 0.55f + (clampedDensity * 0.85f));
            }

            if (material.HasProperty("_Power"))
            {
                material.SetFloat("_Power", 2.6f);
            }

            return material;
        }

        /// <summary>Additive overlay used for the region cap and its outline ring.</summary>
        public static Material CreateHighlightMaterial(bool ring)
        {
            Color color = ring ? HighlightGreen : HighlightCyan;
            Material material = CreateOverlayMaterial(ring ? "NexusRegionRing" : "NexusRegionCap", color);
            SetFloat(material, "_Intensity", ring ? 1.35f : 0.55f);
            return material;
        }

        /// <summary>Additive, depth-testing, non-writing unlit material.</summary>
        public static Material CreateOverlayMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = name,
                renderQueue = (int)RenderQueue.Transparent
            };

            SetColor(material, color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 2f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.One);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            return material;
        }

        private static Material CreateLitMaterial(string name)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            return new Material(shader) { name = name };
        }

        /// <summary>Writes both the URP (<c>_BaseColor</c>) and legacy (<c>_Color</c>) names.</summary>
        public static void SetColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
