using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Maps a direction on the planet sphere to a region. Pure math, no scene objects: the
    /// same call is used by picking, by the surface generator (region tinting) and by the
    /// tests, which keeps selection and rendering in agreement.
    /// </summary>
    public static class RegionLocator
    {
        public const int NoRegion = -1;

        /// <summary>
        /// Finds the region that best covers <paramref name="direction"/>. Overlapping regions
        /// are resolved by the smallest normalised distance to their centre, so a small region
        /// nested inside a large one still wins when the player points at it.
        /// </summary>
        public static bool TryFindRegion(IReadOnlyList<PlanetRegion> regions, Vector3 direction, out PlanetRegion region)
        {
            region = null;
            if (regions == null || regions.Count == 0 || !PlanetGeo.IsUsableDirection(direction))
            {
                return false;
            }

            int bestIndex = NoRegion;
            float bestScore = float.MaxValue;

            for (int i = 0; i < regions.Count; i++)
            {
                PlanetRegion candidate = regions[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = PlanetGeo.AngularDistanceDegrees(candidate.Direction, direction);
                if (distance > candidate.AngularRadiusDegrees)
                {
                    continue;
                }

                float score = candidate.AngularRadiusDegrees <= 0f
                    ? distance
                    : distance / candidate.AngularRadiusDegrees;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex == NoRegion)
            {
                return false;
            }

            region = regions[bestIndex];
            return true;
        }

        /// <summary>Region id for a direction, or <see cref="NoRegion"/> when outside every region.</summary>
        public static int FindRegionId(IReadOnlyList<PlanetRegion> regions, Vector3 direction)
        {
            PlanetRegion region;
            return TryFindRegion(regions, direction, out region) ? region.Id : NoRegion;
        }

        /// <summary>Index of the region nearest to a direction, ignoring region radii.</summary>
        public static int FindNearestRegionIndex(IReadOnlyList<PlanetRegion> regions, Vector3 direction)
        {
            if (regions == null || regions.Count == 0 || !PlanetGeo.IsUsableDirection(direction))
            {
                return NoRegion;
            }

            int bestIndex = NoRegion;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < regions.Count; i++)
            {
                PlanetRegion candidate = regions[i];
                if (candidate == null)
                {
                    continue;
                }

                float distance = PlanetGeo.AngularDistanceDegrees(candidate.Direction, direction);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        public static bool TryFindRegionById(IReadOnlyList<PlanetRegion> regions, int id, out PlanetRegion region)
        {
            region = null;
            if (regions == null)
            {
                return false;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i] != null && regions[i].Id == id)
                {
                    region = regions[i];
                    return true;
                }
            }

            return false;
        }
    }
}
