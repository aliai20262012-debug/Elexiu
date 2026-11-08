using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.Planet
{
    /// <summary>
    /// Builds the planet geometry procedurally: an icosphere whose vertices are displaced by
    /// the surface height field, split into a land submesh and a flat ocean submesh, plus the
    /// small cap/ring meshes that highlight a selected region.
    ///
    /// Geometry is generated once per session and shared by the renderer and the collider;
    /// no per-frame allocations happen here.
    /// </summary>
    public static class PlanetMeshBuilder
    {
        private const float IcosahedronT = 1.61803398875f;

        /// <summary>
        /// Planet body mesh. Submesh 0 = land, submesh 1 = ocean. The ocean vertices sit on the
        /// sea sphere, land vertices are raised by the surface relief.
        /// </summary>
        public static Mesh BuildPlanetMesh(PlanetSurface surface, float radius, int subdivisions)
        {
            if (surface == null)
            {
                return null;
            }

            int safeRadius = Mathf.Clamp(subdivisions, 1, 6);
            List<Vector3> directions = new List<Vector3>(20000);
            List<int> triangles = new List<int>(60000);
            BuildIcosphere(safeRadius, directions, triangles);

            Vector3[] vertices = new Vector3[directions.Count];
            Vector2[] uvs = new Vector2[directions.Count];
            Color32[] colors = new Color32[directions.Count];
            bool[] isLand = new bool[directions.Count];

            float seaLevel = surface.SeaLevel;
            float relief = surface.Settings.Relief;

            for (int i = 0; i < directions.Count; i++)
            {
                Vector3 direction = directions[i];
                float height = surface.HeightAt(direction);
                bool land = height >= seaLevel;
                isLand[i] = land;

                float distance = radius;
                if (land)
                {
                    float elevation = Mathf.Clamp01((height - seaLevel) / Mathf.Max(0.0001f, 1f - seaLevel));
                    distance = radius * (1f + (relief * Mathf.Pow(elevation, 1.35f)));
                }

                vertices[i] = direction * distance;
                uvs[i] = EquirectUv(direction);
                colors[i] = surface.AlbedoAt(direction, height);
            }

            List<int> landTriangles = new List<int>(triangles.Count);
            List<int> waterTriangles = new List<int>(triangles.Count / 3);

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                if (isLand[a] || isLand[b] || isLand[c])
                {
                    landTriangles.Add(a);
                    landTriangles.Add(b);
                    landTriangles.Add(c);
                }
                else
                {
                    waterTriangles.Add(a);
                    waterTriangles.Add(b);
                    waterTriangles.Add(c);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "NexusPlanetSurface";
            if (vertices.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(landTriangles, 0, false);
            mesh.SetTriangles(waterTriangles, 1, false);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Smooth sphere used for the atmosphere shell and other effect shells.</summary>
        public static Mesh BuildSphereMesh(float radius, int subdivisions, string name)
        {
            List<Vector3> directions = new List<Vector3>(5000);
            List<int> triangles = new List<int>(20000);
            BuildIcosphere(Mathf.Clamp(subdivisions, 1, 5), directions, triangles);

            Vector3[] vertices = new Vector3[directions.Count];
            for (int i = 0; i < directions.Count; i++)
            {
                vertices[i] = directions[i] * radius;
            }

            Mesh mesh = new Mesh();
            mesh.name = name;
            if (vertices.Length > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Filled spherical cap covering a region, used as the selection highlight. The cap is
        /// lifted slightly above the surface so it never z-fights the terrain.
        /// </summary>
        public static Mesh BuildRegionCap(PlanetRegion region, float radius, float surfaceOffset)
        {
            return BuildRegionCap(region, radius, surfaceOffset, 3, 40, 0f, 1f);
        }

        /// <summary>
        /// Ring outline around a region. <paramref name="thicknessDegrees"/> widens the band,
        /// <paramref name="innerScale"/> selects where inside the region the band starts.
        /// </summary>
        public static Mesh BuildRegionRing(
            PlanetRegion region,
            float radius,
            float surfaceOffset,
            float thicknessDegrees)
        {
            return BuildRegionCap(
                region,
                radius,
                surfaceOffset + 0.001f,
                1,
                64,
                Mathf.Max(0f, thicknessDegrees),
                1f);
        }

        private static Mesh BuildRegionCap(
            PlanetRegion region,
            float radius,
            float surfaceOffset,
            int rings,
            int segments,
            float innerInsetDegrees,
            float outerScale)
        {
            if (region == null)
            {
                return null;
            }

            int ringCount = Mathf.Clamp(rings, 1, 8);
            int segmentCount = Mathf.Clamp(segments, 8, 128);
            float outerAngle = Mathf.Max(0.5f, region.AngularRadiusDegrees * Mathf.Clamp01(outerScale));
            float innerAngle = outerAngle - Mathf.Max(0f, innerInsetDegrees);
            float capRadius = radius * (1f + surfaceOffset);

            List<Vector3> vertices = new List<Vector3>((ringCount * segmentCount) + 1);
            List<int> triangles = new List<int>(ringCount * segmentCount * 6);
            List<Vector2> uvs = new List<Vector2>(vertices.Capacity);

            Quaternion basis = Quaternion.FromToRotation(Vector3.forward, region.Direction);
            vertices.Add(region.Direction * capRadius);
            uvs.Add(new Vector2(0.5f, 0.5f));

            bool ringOnly = innerAngle > 0.01f;

            for (int ring = 1; ring <= ringCount; ring++)
            {
                float t = ring / (float)ringCount;
                float angle = Mathf.Lerp(ringOnly ? innerAngle : 0f, outerAngle, t) * Mathf.Deg2Rad;
                float ringRadius = Mathf.Sin(angle);
                float height = Mathf.Cos(angle);

                for (int segment = 0; segment < segmentCount; segment++)
                {
                    float segmentAngle = (segment / (float)segmentCount) * Mathf.PI * 2f;
                    Vector3 local = new Vector3(Mathf.Cos(segmentAngle) * ringRadius, Mathf.Sin(segmentAngle) * ringRadius, height);
                    Vector3 direction = (basis * local).normalized;
                    vertices.Add(direction * capRadius);
                    uvs.Add(new Vector2((segment / (float)segmentCount), t));
                }
            }

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int next = (segment + 1) % segmentCount;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + segment);
            }

            for (int ring = 1; ring < ringCount; ring++)
            {
                int currentStart = 1 + ((ring - 1) * segmentCount);
                int nextStart = 1 + (ring * segmentCount);

                for (int segment = 0; segment < segmentCount; segment++)
                {
                    int next = (segment + 1) % segmentCount;
                    triangles.Add(currentStart + segment);
                    triangles.Add(currentStart + next);
                    triangles.Add(nextStart + segment);

                    triangles.Add(currentStart + next);
                    triangles.Add(nextStart + next);
                    triangles.Add(nextStart + segment);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "RegionCap_" + region.Code;
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 EquirectUv(Vector3 direction)
        {
            float u = 0.5f + (Mathf.Atan2(direction.x, direction.z) / (Mathf.PI * 2f));
            float v = 0.5f + (Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) / Mathf.PI);
            return new Vector2(u, v);
        }

        /// <summary>
        /// Appends an icosphere with outward-facing winding. <paramref name="subdivisions"/> 0
        /// yields the 20 base faces, each extra level multiplies the triangle count by four.
        /// </summary>
        private static void BuildIcosphere(int subdivisions, List<Vector3> vertices, List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();

            vertices.Add(new Vector3(-1f, IcosahedronT, 0f).normalized);
            vertices.Add(new Vector3(1f, IcosahedronT, 0f).normalized);
            vertices.Add(new Vector3(-1f, -IcosahedronT, 0f).normalized);
            vertices.Add(new Vector3(1f, -IcosahedronT, 0f).normalized);
            vertices.Add(new Vector3(0f, -1f, IcosahedronT).normalized);
            vertices.Add(new Vector3(0f, 1f, IcosahedronT).normalized);
            vertices.Add(new Vector3(0f, -1f, -IcosahedronT).normalized);
            vertices.Add(new Vector3(0f, 1f, -IcosahedronT).normalized);
            vertices.Add(new Vector3(IcosahedronT, 0f, -1f).normalized);
            vertices.Add(new Vector3(IcosahedronT, 0f, 1f).normalized);
            vertices.Add(new Vector3(-IcosahedronT, 0f, -1f).normalized);
            vertices.Add(new Vector3(-IcosahedronT, 0f, 1f).normalized);

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            List<int> current = new List<int>(faces);
            Dictionary<long, int> midpointCache = new Dictionary<long, int>(2048);

            for (int level = 0; level < subdivisions; level++)
            {
                List<int> next = new List<int>(current.Count * 4);

                for (int i = 0; i < current.Count; i += 3)
                {
                    int a = current[i];
                    int b = current[i + 1];
                    int c = current[i + 2];

                    int ab = Midpoint(a, b, vertices, midpointCache);
                    int bc = Midpoint(b, c, vertices, midpointCache);
                    int ca = Midpoint(c, a, vertices, midpointCache);

                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }

                current = next;
                midpointCache.Clear();
            }

            triangles.AddRange(current);
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + (uint)b : ((long)b << 32) + (uint)a;
            int cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            Vector3 midpoint = ((vertices[a] + vertices[b]) * 0.5f).normalized;
            vertices.Add(midpoint);
            int index = vertices.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
