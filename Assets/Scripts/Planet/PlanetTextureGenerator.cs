using System;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>Pair of baked textures plus their size, owned by the planet renderer.</summary>
    public sealed class PlanetSurfaceTextures
    {
        public PlanetSurfaceTextures(Texture2D albedo, Texture2D normal, int width, int height)
        {
            Albedo = albedo;
            Normal = normal;
            Width = width;
            Height = height;
        }

        public Texture2D Albedo { get; private set; }

        public Texture2D Normal { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public bool IsValid
        {
            get { return Albedo != null && Normal != null; }
        }

        /// <summary>Destroys the runtime textures (they are not assets).</summary>
        public void Release()
        {
            if (Albedo != null)
            {
                UnityEngine.Object.Destroy(Albedo);
                Albedo = null;
            }

            if (Normal != null)
            {
                UnityEngine.Object.Destroy(Normal);
                Normal = null;
            }
        }
    }

    /// <summary>
    /// Bakes an equirectangular albedo map and a matching tangent-space normal map from the
    /// procedural surface. One height grid is shared by both maps, so the cost stays linear
    /// in texels instead of re-sampling the noise field for every map.
    /// </summary>
    public static class PlanetTextureGenerator
    {
        private static readonly Color DeepestWater = new Color(0.010f, 0.043f, 0.110f, 1f);

        /// <summary>
        /// Generates the maps. <paramref name="width"/> is clamped to 128..2048 and the height
        /// is half the width (2:1 equirectangular).
        /// </summary>
        public static PlanetSurfaceTextures Generate(PlanetSurface surface, int width, int octaves)
        {
            if (surface == null)
            {
                return null;
            }

            int mapWidth = Mathf.Clamp(width, 128, 2048);
            if (mapWidth % 2 != 0)
            {
                mapWidth--;
            }

            int mapHeight = Mathf.Max(64, mapWidth / 2);
            int texelCount = mapWidth * mapHeight;

            float[] heights = new float[texelCount];
            Vector3[] directions = new Vector3[texelCount];

            int usedOctaves = Mathf.Clamp(octaves, 1, PlanetSurface.DetailOctaves);
            for (int y = 0; y < mapHeight; y++)
            {
                float v = (y + 0.5f) / mapHeight;
                float latitude = ((v - 0.5f) * 180f);

                for (int x = 0; x < mapWidth; x++)
                {
                    float u = (x + 0.5f) / mapWidth;
                    float longitude = ((u - 0.5f) * 360f);
                    Vector3 direction = PlanetGeo.DirectionFromLatLon(latitude, longitude);
                    int index = (y * mapWidth) + x;
                    directions[index] = direction;
                    heights[index] = surface.HeightAt(direction, usedOctaves);
                }
            }

            Texture2D albedo = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, true, false)
            {
                name = "NexusPlanetAlbedo",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };

            Texture2D normal = new Texture2D(mapWidth, mapHeight, TextureFormat.RGBA32, true, true)
            {
                name = "NexusPlanetNormal",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2
            };

            Color32[] albedoPixels = new Color32[texelCount];
            Color32[] normalPixels = new Color32[texelCount];

            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    int index = (y * mapWidth) + x;
                    Vector3 direction = directions[index];
                    float height = heights[index];

                    Color color = surface.AlbedoAt(direction, height);
                    if (!surface.IsLand(direction, height))
                    {
                        // Darken trenches a little further than the palette does, so the deep
                        // basins read as depth under the flat ocean material.
                        float depth = Mathf.Clamp01((surface.SeaLevel - height) / Mathf.Max(0.0001f, surface.SeaLevel));
                        color = Color.Lerp(color, DeepestWater, Mathf.Pow(depth, 1.6f) * 0.6f);
                    }

                    albedoPixels[index] = color;
                    normalPixels[index] = EncodeNormal(
                        surface,
                        mapWidth,
                        mapHeight,
                        x,
                        y,
                        directions,
                        heights);
                }
            }

            albedo.SetPixels32(albedoPixels);
            albedo.Apply(true, false);

            normal.SetPixels32(normalPixels);
            normal.Apply(true, false);

            return new PlanetSurfaceTextures(albedo, normal, mapWidth, mapHeight);
        }

        /// <summary>
        /// Tangent-space normal from the height grid. The local east/north/up basis follows the
        /// equirectangular UV mapping, which is what Unity's tangent reconstruction expects.
        /// </summary>
        private static Color32 EncodeNormal(
            PlanetSurface surface,
            int width,
            int height,
            int x,
            int y,
            Vector3[] directions,
            float[] heights)
        {
            int right = (x + 1) % width;
            int left = (x - 1 + width) % width;
            int up = Mathf.Clamp(y + 1, 0, height - 1);
            int down = Mathf.Clamp(y - 1, 0, height - 1);

            Vector3 direction = directions[(y * width) + x];
            Vector3 east = Vector3.Cross(Vector3.up, direction);
            if (east.sqrMagnitude < 1e-6f)
            {
                east = Vector3.right;
            }

            east.Normalize();
            Vector3 north = Vector3.Cross(direction, east).normalized;

            Vector3 current = DisplacedPoint(surface, direction, heights[(y * width) + x]);
            Vector3 eastPoint = DisplacedPoint(surface, directions[(y * width) + right], heights[(y * width) + right]);
            Vector3 westPoint = DisplacedPoint(surface, directions[(y * width) + left], heights[(y * width) + left]);
            Vector3 northPoint = DisplacedPoint(surface, directions[(up * width) + x], heights[(up * width) + x]);
            Vector3 southPoint = DisplacedPoint(surface, directions[(down * width) + x], heights[(down * width) + x]);

            Vector3 derivativeEast = (eastPoint - westPoint) * 0.5f;
            Vector3 derivativeNorth = (northPoint - southPoint) * 0.5f;

            Vector3 normalWorld = Vector3.Cross(derivativeNorth, derivativeEast);
            if (Vector3.Dot(normalWorld, direction) < 0f)
            {
                normalWorld = -normalWorld;
            }

            Vector3 tangentNormal = new Vector3(
                Vector3.Dot(normalWorld, east),
                Vector3.Dot(normalWorld, north),
                Vector3.Dot(normalWorld, direction));

            if (tangentNormal.sqrMagnitude < 1e-8f)
            {
                tangentNormal = Vector3.forward;
            }

            tangentNormal.Normalize();

            // Flatten the map towards (0,0,1): a fully detailed normal map on a low-poly
            // sphere reads as noise, and the authored relief is already geometric.
            tangentNormal = new Vector3(tangentNormal.x * 0.65f, tangentNormal.y * 0.65f, tangentNormal.z);
            tangentNormal.Normalize();

            return new Color32(
                (byte)Mathf.Clamp((int)((tangentNormal.x * 0.5f + 0.5f) * 255f), 0, 255),
                (byte)Mathf.Clamp((int)((tangentNormal.y * 0.5f + 0.5f) * 255f), 0, 255),
                (byte)Mathf.Clamp((int)((tangentNormal.z * 0.5f + 0.5f) * 255f), 0, 255),
                255);
        }

        private static Vector3 DisplacedPoint(PlanetSurface surface, Vector3 direction, float height)
        {
            float relief = surface.Settings.Relief;
            float seaLevel = surface.SeaLevel;
            if (height < seaLevel)
            {
                return direction;
            }

            float elevation = Mathf.Clamp01((height - seaLevel) / Mathf.Max(0.0001f, 1f - seaLevel));
            return direction * (1f + (relief * Mathf.Pow(elevation, 1.35f)));
        }
    }
}
