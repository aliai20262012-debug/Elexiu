using System;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Spherical geometry helpers shared by region placement, picking and the surface
    /// generator. Pure math on unit vectors: no scene, no physics, no randomness.
    /// </summary>
    public static class PlanetGeo
    {
        public const float MinLatitude = -90f;
        public const float MaxLatitude = 90f;
        public const float MinLongitude = -180f;
        public const float MaxLongitude = 180f;

        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Converts spherical coordinates to a unit direction. Latitude is measured from the
        /// equator (positive = north), longitude from the prime meridian (positive = east).
        /// </summary>
        public static Vector3 DirectionFromLatLon(float latitudeDegrees, float longitudeDegrees)
        {
            double latitude = Clamp(latitudeDegrees, MinLatitude, MaxLatitude) * DegToRad;
            double longitude = WrapLongitude(longitudeDegrees) * DegToRad;
            double cosLatitude = Math.Cos(latitude);

            return new Vector3(
                (float)(cosLatitude * Math.Sin(longitude)),
                (float)Math.Sin(latitude),
                (float)(cosLatitude * Math.Cos(longitude)));
        }

        /// <summary>Inverse of <see cref="DirectionFromLatLon"/>; normalises the input.</summary>
        public static void LatLonFromDirection(Vector3 direction, out float latitudeDegrees, out float longitudeDegrees)
        {
            Vector3 normalised = direction.sqrMagnitude <= 1e-8f ? Vector3.forward : direction.normalized;
            latitudeDegrees = (float)(Math.Asin(Clamp(normalised.y, -1f, 1f)) * RadToDeg);
            longitudeDegrees = (float)(Math.Atan2(normalised.x, normalised.z) * RadToDeg);
        }

        /// <summary>Angle in degrees between two directions.</summary>
        public static float AngularDistanceDegrees(Vector3 a, Vector3 b)
        {
            Vector3 left = a.normalized;
            Vector3 right = b.normalized;
            float dot = Mathf.Clamp(Vector3.Dot(left, right), -1f, 1f);
            return (float)(Math.Acos(dot) * RadToDeg);
        }

        /// <summary>Wraps a longitude into the [-180, 180] interval.</summary>
        public static float WrapLongitude(float longitudeDegrees)
        {
            if (float.IsNaN(longitudeDegrees) || float.IsInfinity(longitudeDegrees))
            {
                return 0f;
            }

            float wrapped = (longitudeDegrees + 180f) % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            return wrapped - 180f;
        }

        /// <summary>False when the direction is not a usable unit-ish vector.</summary>
        public static bool IsUsableDirection(Vector3 direction)
        {
            if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z))
            {
                return false;
            }

            if (float.IsInfinity(direction.x) || float.IsInfinity(direction.y) || float.IsInfinity(direction.z))
            {
                return false;
            }

            return direction.sqrMagnitude > 1e-8f;
        }

        /// <summary>
        /// Deterministic quasi-uniform direction for an index, used to seed region centres and
        /// surface detail. Fibonacci sphere: no random source, identical on every platform.
        /// </summary>
        public static Vector3 FibonacciDirection(int index, int count)
        {
            if (count <= 0)
            {
                return Vector3.up;
            }

            int safeIndex = index % count;
            if (safeIndex < 0)
            {
                safeIndex += count;
            }

            double golden = Math.PI * (3.0 - Math.Sqrt(5.0));
            double y = 1.0 - ((safeIndex + 0.5) * 2.0 / count);
            double radius = Math.Sqrt(Math.Max(0.0, 1.0 - (y * y)));
            double theta = golden * safeIndex;

            return new Vector3(
                (float)(Math.Cos(theta) * radius),
                (float)y,
                (float)(Math.Sin(theta) * radius));
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
