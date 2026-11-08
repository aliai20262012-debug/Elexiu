using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Deterministic, allocation free 3D value noise used by the surface generator.
    ///
    /// It is a pure function of (seed, position): the same inputs produce the same planet on
    /// every platform and in every test run, which is what makes the surface reproducible
    /// from the session seed alone.
    /// </summary>
    public static class PlanetNoise
    {
        /// <summary>Hash of an integer lattice point, normalised to 0..1.</summary>
        public static float Hash01(int seed, int x, int y, int z)
        {
            unchecked
            {
                uint hash = (uint)seed;
                hash ^= (uint)x * 0x8DA6B343u;
                hash ^= (uint)y * 0xD8163841u;
                hash ^= (uint)z * 0xCB1AB31Fu;
                hash ^= hash >> 15;
                hash *= 0x2C1B3C6Du;
                hash ^= hash >> 12;
                hash *= 0x297A2D39u;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFFu) / 16777215f;
            }
        }

        /// <summary>Smooth (quintic) value noise at a 3D position, in 0..1.</summary>
        public static float Value(Vector3 position, int seed)
        {
            int x0 = Mathf.FloorToInt(position.x);
            int y0 = Mathf.FloorToInt(position.y);
            int z0 = Mathf.FloorToInt(position.z);

            float fx = Quintic(position.x - x0);
            float fy = Quintic(position.y - y0);
            float fz = Quintic(position.z - z0);

            float c000 = Hash01(seed, x0, y0, z0);
            float c100 = Hash01(seed, x0 + 1, y0, z0);
            float c010 = Hash01(seed, x0, y0 + 1, z0);
            float c110 = Hash01(seed, x0 + 1, y0 + 1, z0);
            float c001 = Hash01(seed, x0, y0, z0 + 1);
            float c101 = Hash01(seed, x0 + 1, y0, z0 + 1);
            float c011 = Hash01(seed, x0, y0 + 1, z0 + 1);
            float c111 = Hash01(seed, x0 + 1, y0 + 1, z0 + 1);

            float x00 = Mathf.Lerp(c000, c100, fx);
            float x10 = Mathf.Lerp(c010, c110, fx);
            float x01 = Mathf.Lerp(c001, c101, fx);
            float x11 = Mathf.Lerp(c011, c111, fx);

            float y0Lerp = Mathf.Lerp(x00, x10, fy);
            float y1Lerp = Mathf.Lerp(x01, x11, fy);
            return Mathf.Lerp(y0Lerp, y1Lerp, fz);
        }

        /// <summary>Fractal Brownian motion of <see cref="Value"/>, normalised to 0..1.</summary>
        public static float Fbm(Vector3 position, int seed, int octaves)
        {
            return Fbm(position, seed, octaves, 2.02f, 0.5f);
        }

        public static float Fbm(Vector3 position, int seed, int octaves, float lacunarity, float gain)
        {
            if (octaves < 1)
            {
                return 0.5f;
            }

            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Value(position * frequency, seed + (i * 131)) * amplitude;
                total += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return total <= 0f ? 0.5f : Mathf.Clamp01(sum / total);
        }

        /// <summary>
        /// Ridged multifractal (0..1) with sharp crests; used for mountain ranges.
        /// </summary>
        public static float RidgedFbm(Vector3 position, int seed, int octaves)
        {
            if (octaves < 1)
            {
                return 0f;
            }

            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < octaves; i++)
            {
                float value = Value(position * frequency, seed + (i * 197));
                float ridge = 1f - Mathf.Abs((value * 2f) - 1f);
                sum += ridge * ridge * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.03f;
            }

            return total <= 0f ? 0f : Mathf.Clamp01(sum / total);
        }

        private static float Quintic(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
