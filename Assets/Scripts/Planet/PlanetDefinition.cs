using System.Collections.Generic;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Authored definition of a planet: identity, physical constants, surface generation
    /// tuning, render quality and the region set. All values are validated and clamped on
    /// read, so an asset edited badly still produces a playable planet.
    ///
    /// The region table lives in a JSON TextAsset referenced from this asset
    /// (<c>Resources/Config/Planet/*.json</c>) because it is a list of dozens of values that
    /// designers and tools edit as data; the planet identity and generation tuning stay in
    /// the ScriptableObject so Unity's inspector can tweak them.
    /// </summary>
    [CreateAssetMenu(menuName = "Nexus/Planet/Planet Definition", fileName = "PlanetDefinition")]
    public sealed class PlanetDefinition : ScriptableObject
    {
        public const string DefaultResourcePath = "Config/Planet/Nexus-01";

        [Header("Identity")]
        [SerializeField] private string _planetId = "NEXUS-01";
        [SerializeField] private string _displayName = "NEXUS-01";
        [SerializeField] private string _designation = "CLASS-K TERRESTRIAL // CANDIDATE 07";
        [SerializeField] private int _seed = 20261106;

        [Header("Physical")]
        [Tooltip("Equatorial radius in kilometres. Display data; the render radius is a scene scale.")]
        [SerializeField] private float _radiusKm = 6180f;
        [SerializeField] private float _gravity = 0.97f;
        [Tooltip("Length of one simulated planet day in simulation seconds.")]
        [SerializeField] private float _dayLengthSeconds = 900f;
        [Tooltip("Days in one orbital (season) cycle.")]
        [SerializeField] private float _orbitalPeriodDays = 96f;
        [SerializeField] private float _axialTiltDegrees = 23.4f;
        [Tooltip("Visual spin of the planet body in degrees per real second.")]
        [SerializeField] private float _rotationDegreesPerSecond = 1.5f;

        [Header("Surface generation")]
        [Tooltip("Normalised height threshold between ocean and land (0..1).")]
        [SerializeField] private float _seaLevel = 0.5f;
        [SerializeField] private float _continentFrequency = 1.35f;
        [SerializeField] private float _detailFrequency = 7.5f;
        [SerializeField] private float _ridgeStrength = 0.55f;
        [Tooltip("Maximum land elevation as a fraction of the render radius.")]
        [SerializeField] private float _surfaceRelief = 0.03f;
        [Tooltip("Ocean depth as a fraction of the render radius.")]
        [SerializeField] private float _oceanDepth = 0.012f;

        [Header("Quality")]
        [Tooltip("Icosphere subdivision level of the land mesh (2..6).")]
        [SerializeField] private int _meshSubdivisions = 5;
        [Tooltip("Equirectangular texture width; height is half.")]
        [SerializeField] private int _textureResolution = 512;
        [SerializeField] private float _atmosphereDensity = 0.65f;

        [Header("Data")]
        [Tooltip("JSON file with the region table. Empty falls back to the built-in NEXUS-01 set.")]
        [SerializeField] private TextAsset _regionFile;

        private List<PlanetRegion> _regions;
        private List<string> _regionIssues = new List<string>();
        private List<PlanetRegion> _runtimeRegions;

        public string PlanetId
        {
            get { return string.IsNullOrEmpty(_planetId) ? "NEXUS-01" : _planetId.Trim(); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(_displayName) ? PlanetId : _displayName.Trim(); }
        }

        public string Designation
        {
            get { return string.IsNullOrEmpty(_designation) ? "UNCLASSIFIED" : _designation.Trim(); }
        }

        public int Seed
        {
            get { return _seed; }
        }

        public float RadiusKm
        {
            get { return Mathf.Clamp(_radiusKm, 500f, 20000f); }
        }

        public float Gravity
        {
            get { return Mathf.Clamp(_gravity, 0.05f, 5f); }
        }

        public float DayLengthSeconds
        {
            get { return Mathf.Clamp(_dayLengthSeconds, 60f, 1000000f); }
        }

        public float OrbitalPeriodDays
        {
            get { return Mathf.Clamp(_orbitalPeriodDays, 1f, 10000f); }
        }

        public float AxialTiltDegrees
        {
            get { return Mathf.Clamp(_axialTiltDegrees, 0f, 90f); }
        }

        public float RotationDegreesPerSecond
        {
            get { return Mathf.Clamp(_rotationDegreesPerSecond, 0f, 45f); }
        }

        public float SeaLevel
        {
            get { return Mathf.Clamp(_seaLevel, 0.05f, 0.95f); }
        }

        public float ContinentFrequency
        {
            get { return Mathf.Clamp(_continentFrequency, 0.2f, 6f); }
        }

        public float DetailFrequency
        {
            get { return Mathf.Clamp(_detailFrequency, 1f, 32f); }
        }

        public float RidgeStrength
        {
            get { return Mathf.Clamp(_ridgeStrength, 0f, 1f); }
        }

        public float SurfaceRelief
        {
            get { return Mathf.Clamp(_surfaceRelief, 0.002f, 0.15f); }
        }

        public float OceanDepth
        {
            get { return Mathf.Clamp(_oceanDepth, 0f, 0.08f); }
        }

        public int MeshSubdivisions
        {
            get { return Mathf.Clamp(_meshSubdivisions, 2, 6); }
        }

        public int TextureResolution
        {
            get { return Mathf.Clamp(_textureResolution, 128, 2048); }
        }

        public float AtmosphereDensity
        {
            get { return Mathf.Clamp(_atmosphereDensity, 0f, 2f); }
        }

        public TextAsset RegionFile
        {
            get { return _regionFile; }
        }

        /// <summary>Validated region set; parsed once per definition instance.</summary>
        public IReadOnlyList<PlanetRegion> Regions
        {
            get
            {
                EnsureRegions();
                return _regions;
            }
        }

        /// <summary>Every problem found while loading the region data (empty when clean).</summary>
        public IReadOnlyList<string> RegionIssues
        {
            get
            {
                EnsureRegions();
                return _regionIssues;
            }
        }

        /// <summary>Deterministic surface seed: planet seed combined with the session seed.</summary>
        public int ResolveSurfaceSeed(int sessionSeed)
        {
            unchecked
            {
                int mixed = _seed ^ (sessionSeed * 397);
                mixed ^= mixed >> 13;
                mixed *= 1274126177;
                mixed ^= mixed >> 16;
                return mixed;
            }
        }

        /// <summary>Parses (or falls back to) the region table. Safe to call repeatedly.</summary>
        public void EnsureRegions()
        {
            if (_regions != null)
            {
                return;
            }

            _regionIssues = new List<string>();
            List<PlanetRegion> parsed = null;

            if (_regionFile != null && !string.IsNullOrEmpty(_regionFile.text))
            {
                parsed = new List<PlanetRegion>(PlanetRegionParser.Parse(_regionFile.text, PlanetId, _regionIssues));
            }
            else
            {
                _regionIssues.Add("no region file assigned; using the built-in NEXUS-01 region set");
            }

            if (parsed != null && parsed.Count > 0)
            {
                _regions = parsed;
                return;
            }

            if (parsed != null)
            {
                _regionIssues.Add("region file produced no usable regions; using the built-in NEXUS-01 set");
            }

            _regions = new List<PlanetRegion>(_runtimeRegions ?? PlanetRegionLibrary.CreateDefaultRegions());
        }

        /// <summary>Replaces the region set (tests and tools). Not used at runtime.</summary>
        public void SetRuntimeRegions(IReadOnlyList<PlanetRegion> regions)
        {
            _runtimeRegions = regions != null ? new List<PlanetRegion>(regions) : null;
            _regions = _runtimeRegions != null ? new List<PlanetRegion>(regions) : null;
            _regionIssues = new List<string>();
        }

        /// <summary>
        /// Loads the shipped definition, or builds a validated runtime default when the asset is
        /// missing. Never returns null.
        /// </summary>
        public static PlanetDefinition LoadDefault()
        {
            PlanetDefinition definition = Resources.Load<PlanetDefinition>(DefaultResourcePath);
            if (definition != null)
            {
                return definition;
            }

            if (RuntimeFallback == null)
            {
                RuntimeFallback = CreateRuntimeDefault();
            }

            return RuntimeFallback;
        }

        /// <summary>Cached in-memory definition used when the asset is missing.</summary>
        public static PlanetDefinition RuntimeFallback { get; private set; }

        /// <summary>In-memory definition with the built-in region set.</summary>
        public static PlanetDefinition CreateRuntimeDefault()
        {
            PlanetDefinition definition = CreateInstance<PlanetDefinition>();
            definition.name = "PlanetDefinition (runtime default)";
            definition.hideFlags = HideFlags.DontSave;
            definition.SetRuntimeRegions(PlanetRegionLibrary.CreateDefaultRegions());
            return definition;
        }
    }
}
