using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Presentation of the planet: builds the procedural body, the ocean, the atmosphere and
    /// the region highlight, and owns everything on screen. It never talks to the simulation
    /// system graph; it reads a <see cref="PlanetDefinition"/> and a seed and exposes
    /// selection events that the scene controller translates into camera moves and UI.
    /// </summary>
    public sealed class PlanetView : MonoBehaviour
    {
        public const float DefaultRenderRadius = 3f;

        private readonly List<PlanetRegion> _regions = new List<PlanetRegion>();

        private PlanetDefinition _definition;
        private PlanetSurface _surface;

        private Transform _axis;
        private Transform _body;
        private Transform _highlightRoot;

        private Mesh _surfaceMesh;
        private Mesh _atmosphereMesh;
        private Mesh _capMesh;
        private Mesh _ringMesh;

        private Material _landMaterial;
        private Material _oceanMaterial;
        private Material _atmosphereMaterial;
        private Material _capMaterial;
        private Material _ringMaterial;

        private MeshRenderer _capRenderer;
        private MeshRenderer _ringRenderer;

        private PlanetSurfaceTextures _textures;
        private MeshCollider _collider;

        private PlanetRegion _selectedRegion;
        private float _radius = DefaultRenderRadius;
        private float _rotationDegreesPerSecond = 1.5f;
        private int _subdivisions = 5;
        private int _textureWidth = 1024;
        private float _pulsePhase;

        /// <summary>Raised when a region becomes selected (never raised for a re-selection).</summary>
        public event Action<PlanetRegion> RegionSelected;

        /// <summary>Raised when the selection is cleared.</summary>
        public event Action SelectionCleared;

        public PlanetDefinition Definition
        {
            get { return _definition; }
        }

        public PlanetSurface Surface
        {
            get { return _surface; }
        }

        public IReadOnlyList<PlanetRegion> Regions
        {
            get { return _regions; }
        }

        public float Radius
        {
            get { return _radius; }
        }

        public Transform BodyTransform
        {
            get { return _body; }
        }

        public bool IsBuilt
        {
            get { return _surfaceMesh != null && _body != null; }
        }

        public bool RotationEnabled { get; set; }

        /// <summary>Overrides the spin speed from the definition (legacy scene values).</summary>
        public void SetRotationDegreesPerSecond(float degreesPerSecond)
        {
            if (float.IsNaN(degreesPerSecond) || degreesPerSecond < 0f)
            {
                return;
            }

            _rotationDegreesPerSecond = Mathf.Clamp(degreesPerSecond, 0f, 45f);
        }

        public int MeshSubdivisions
        {
            get { return _subdivisions; }
        }

        public int TextureWidth
        {
            get { return _textureWidth; }
        }

        public PlanetRegion SelectedRegion
        {
            get { return _selectedRegion; }
        }

        /// <summary>True while the selection cap/ring is rendered.</summary>
        public bool HasSelectionHighlight
        {
            get { return _highlightRoot != null && _highlightRoot.gameObject.activeSelf; }
        }

        public int SelectedRegionId
        {
            get { return _selectedRegion != null ? _selectedRegion.Id : RegionLocator.NoRegion; }
        }

        /// <summary>
        /// Builds the planet. <paramref name="highQuality"/> lets the platform reduce the mesh
        /// and texture cost: phones get one subdivision less and half the texture width,
        /// tablets and desktop get the authored values.
        /// </summary>
        public void Build(PlanetDefinition definition, int sessionSeed, float renderRadius, bool highQuality)
        {
            if (IsBuilt)
            {
                return;
            }

            _definition = definition != null ? definition : PlanetDefinition.CreateRuntimeDefault();
            _radius = Mathf.Clamp(renderRadius, 0.5f, 100f);
            _rotationDegreesPerSecond = _definition.RotationDegreesPerSecond;
            _subdivisions = highQuality
                ? _definition.MeshSubdivisions
                : Mathf.Max(2, _definition.MeshSubdivisions - 1);
            _textureWidth = highQuality
                ? _definition.TextureResolution
                : Mathf.Max(256, _definition.TextureResolution / 2);

            _surface = new PlanetSurface(
                PlanetSurfaceSettings.FromDefinition(_definition, sessionSeed),
                _definition.Regions);

            _regions.Clear();
            IReadOnlyList<PlanetRegion> regions = _surface.Regions;
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i] != null)
                {
                    _regions.Add(regions[i]);
                }
            }

            BuildHierarchy();
            BuildSurface();
            BuildAtmosphere();
            BuildHighlight();
            RotationEnabled = true;
        }

        /// <summary>
        /// Screen point to region. Returns false when the ray misses the planet or lands
        /// outside every region; a hit inside a region selects it and raises the event.
        /// </summary>
        public bool TrySelectAtScreenPoint(Camera camera, Vector2 screenPoint)
        {
            PlanetRegion region = RegionAtScreenPoint(camera, screenPoint);
            if (region == null)
            {
                return false;
            }

            SelectRegion(region.Id);
            return true;
        }

        /// <summary>Region under a screen point, or null.</summary>
        public PlanetRegion RegionAtScreenPoint(Camera camera, Vector2 screenPoint)
        {
            if (!IsBuilt || camera == null)
            {
                return null;
            }

            Ray ray = camera.ScreenPointToRay(screenPoint);
            RaycastHit hit;
            float maxDistance = _radius * 20f;
            if (!Physics.Raycast(ray, out hit, maxDistance))
            {
                return null;
            }

            if (_collider == null || hit.collider != _collider)
            {
                return null;
            }

            Vector3 localDirection = _body.InverseTransformDirection(hit.point - _body.position);
            PlanetRegion region = _surface.RegionAt(localDirection);
            return region != null ? region : _surface.NearestRegionAt(localDirection);
        }

        /// <summary>Selects a region by id and shows the highlight. Returns the region or null.</summary>
        public PlanetRegion SelectRegion(int regionId)
        {
            if (_surface == null)
            {
                return null;
            }

            PlanetRegion region;
            if (!RegionLocator.TryFindRegionById(_regions, regionId, out region) || region == null)
            {
                return null;
            }

            bool changed = _selectedRegion == null || _selectedRegion.Id != region.Id;
            _selectedRegion = region;
            ShowHighlight(region);

            if (changed && RegionSelected != null)
            {
                RegionSelected(region);
            }

            return region;
        }

        /// <summary>Clears the selection and hides the highlight.</summary>
        public void ClearSelection()
        {
            if (_selectedRegion == null)
            {
                return;
            }

            _selectedRegion = null;
            HideHighlight();
            if (SelectionCleared != null)
            {
                SelectionCleared();
            }
        }

        /// <summary>World direction from the planet centre to the region centre.</summary>
        public Vector3 WorldDirectionOf(PlanetRegion region)
        {
            if (region == null || _body == null)
            {
                return Vector3.up;
            }

            return _body.TransformDirection(region.Direction).normalized;
        }

        /// <summary>
        /// Anchor future construction will attach to: the region centre, lifted just above the
        /// terrain so a settlement preview never z-fights the surface. Phase 04 extension point;
        /// nothing is built here yet.
        /// </summary>
        public bool TryGetSettlementAnchor(int regionId, out Vector3 position, out Vector3 normal)
        {
            position = Vector3.zero;
            normal = Vector3.up;

            PlanetRegion region;
            if (_surface == null || _body == null ||
                !RegionLocator.TryFindRegionById(_regions, regionId, out region) || region == null)
            {
                return false;
            }

            normal = WorldDirectionOf(region);
            float lift = _radius * (1f + (_definition.SurfaceRelief * 0.4f));
            position = _body.position + (normal * lift);
            return true;
        }

        /// <summary>World position of the region centre, slightly above the surface.</summary>
        public Vector3 WorldPositionOf(PlanetRegion region)
        {
            if (region == null || _body == null)
            {
                return transform.position;
            }

            return _body.position + (WorldDirectionOf(region) * (_radius * 1.02f));
        }

        private void Update()
        {
            if (!IsBuilt)
            {
                return;
            }

            if (RotationEnabled && _rotationDegreesPerSecond > 0f)
            {
                _body.Rotate(Vector3.up, _rotationDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            if (_highlightRoot != null && _highlightRoot.gameObject.activeSelf)
            {
                // Gentle breathing pulse: cheap, allocation free selection feedback.
                _pulsePhase += Time.deltaTime * 2.2f;
                float pulse = 1f + (Mathf.Sin(_pulsePhase) * 0.012f);
                _highlightRoot.localScale = new Vector3(pulse, pulse, pulse);
            }
        }

        private void OnDestroy()
        {
            ReleaseRuntimeAssets();
        }

        private void BuildHierarchy()
        {
            _axis = new GameObject("PlanetAxis").transform;
            _axis.SetParent(transform, false);
            _axis.localRotation = Quaternion.Euler(0f, 0f, -_definition.AxialTiltDegrees);

            _body = new GameObject("PlanetBody").transform;
            _body.SetParent(_axis, false);
            _body.localPosition = Vector3.zero;
        }

        private void BuildSurface()
        {
            _surfaceMesh = PlanetMeshBuilder.BuildPlanetMesh(_surface, _radius, _subdivisions);
            if (_surfaceMesh == null)
            {
                return;
            }

            GameObject surfaceObject = new GameObject("Surface");
            surfaceObject.transform.SetParent(_body, false);

            MeshFilter filter = surfaceObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _surfaceMesh;

            MeshRenderer renderer = surfaceObject.AddComponent<MeshRenderer>();
            _textures = PlanetTextureGenerator.Generate(_surface, _textureWidth, 2);
            _landMaterial = PlanetMaterials.CreateLandMaterial(_textures);
            _oceanMaterial = PlanetMaterials.CreateOceanMaterial();

            Material[] materials = new Material[2];
            materials[0] = _landMaterial;
            materials[1] = _oceanMaterial;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            _collider = surfaceObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = _surfaceMesh;
        }

        private void BuildAtmosphere()
        {
            _atmosphereMesh = PlanetMeshBuilder.BuildSphereMesh(_radius * 1.055f, 3, "NexusAtmosphere");
            if (_atmosphereMesh == null)
            {
                return;
            }

            GameObject atmosphereObject = new GameObject("Atmosphere");
            atmosphereObject.transform.SetParent(_axis, false);

            MeshFilter filter = atmosphereObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _atmosphereMesh;

            MeshRenderer renderer = atmosphereObject.AddComponent<MeshRenderer>();
            _atmosphereMaterial = PlanetMaterials.CreateAtmosphereMaterial(_definition.AtmosphereDensity);
            renderer.sharedMaterial = _atmosphereMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildHighlight()
        {
            _highlightRoot = new GameObject("RegionHighlight").transform;
            _highlightRoot.SetParent(_body, false);

            _capMaterial = PlanetMaterials.CreateHighlightMaterial(false);
            _ringMaterial = PlanetMaterials.CreateHighlightMaterial(true);

            _capRenderer = CreateHighlightRenderer("RegionCap", _capMaterial);
            _ringRenderer = CreateHighlightRenderer("RegionRing", _ringMaterial);

            HideHighlight();
        }

        private MeshRenderer CreateHighlightRenderer(string name, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(_highlightRoot, false);
            child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            child.SetActive(false);
            return renderer;
        }

        private void ShowHighlight(PlanetRegion region)
        {
            if (_highlightRoot == null || region == null)
            {
                return;
            }

            float offset = _definition.SurfaceRelief + 0.004f;
            ReplaceMesh(ref _capMesh, PlanetMeshBuilder.BuildRegionCap(region, _radius, offset));
            ReplaceMesh(ref _ringMesh, PlanetMeshBuilder.BuildRegionRing(region, _radius, offset + 0.002f, 1.6f));

            ApplyHighlightMesh(_capRenderer, _capMesh);
            ApplyHighlightMesh(_ringRenderer, _ringMesh);

            Color accent = PlanetSurface.AccentFor(region);
            PlanetMaterials.SetColor(_capMaterial, new Color(accent.r, accent.g, accent.b, 1f));
            PlanetMaterials.SetColor(_ringMaterial, new Color(0.204f, 0.827f, 0.600f, 1f));

            _pulsePhase = 0f;
            _highlightRoot.gameObject.SetActive(true);
            _highlightRoot.localScale = Vector3.one;
        }

        private void HideHighlight()
        {
            if (_highlightRoot != null)
            {
                _highlightRoot.gameObject.SetActive(false);
            }
        }

        private void ApplyHighlightMesh(MeshRenderer renderer, Mesh mesh)
        {
            if (renderer == null)
            {
                return;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null)
            {
                filter.sharedMesh = mesh;
            }

            renderer.gameObject.SetActive(mesh != null);
        }

        private static void ReplaceMesh(ref Mesh current, Mesh replacement)
        {
            if (current != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(current);
                }
                else
                {
                    DestroyImmediate(current);
                }
            }

            current = replacement;
        }

        private void ReleaseRuntimeAssets()
        {
            DestroySafely(_surfaceMesh);
            DestroySafely(_atmosphereMesh);
            DestroySafely(_capMesh);
            DestroySafely(_ringMesh);
            DestroySafely(_landMaterial);
            DestroySafely(_oceanMaterial);
            DestroySafely(_atmosphereMaterial);
            DestroySafely(_capMaterial);
            DestroySafely(_ringMaterial);

            if (_textures != null)
            {
                _textures.Release();
                _textures = null;
            }

            _surfaceMesh = null;
            _atmosphereMesh = null;
            _capMesh = null;
            _ringMesh = null;
            _collider = null;
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
