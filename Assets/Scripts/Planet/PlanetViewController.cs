using System.Collections.Generic;
using Nexus.Configuration;
using Nexus.Core;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Scene-level owner of the planet. It resolves the authored <see cref="PlanetDefinition"/>
    /// from configuration, creates the <see cref="PlanetView"/> and guarantees the scene is
    /// lit. Phase 01 used this class as a placeholder sphere; the public surface
    /// (<see cref="Body"/>, <see cref="IsBuilt"/>, <see cref="Build()"/>) is preserved.
    /// </summary>
    public sealed class PlanetViewController : MonoBehaviour
    {
        [Tooltip("Render radius of the planet body in scene units.")]
        [SerializeField] private float _radius = PlanetView.DefaultRenderRadius;

        // Phase 01 placeholders: retained so the shipped Gameplay scene keeps its serialized
        // data. The rotation value is still applied; the surface colour is only a fallback.
        [SerializeField, HideInInspector] private float _rotationDegreesPerSecond = 5f;
        [SerializeField, HideInInspector] private Color _surfaceColor = new Color(0.078f, 0.294f, 0.412f, 1f);

        [Tooltip("Build the planet in Awake with the default seed. The gameplay scene builds it after the session starts instead.")]
        [SerializeField] private bool _buildOnAwake;

        private PlanetView _view;
        private PlanetDefinition _definition;

        public PlanetView View
        {
            get { return _view; }
        }

        public PlanetDefinition Definition
        {
            get { return _definition; }
        }

        public PlanetSurface Surface
        {
            get { return _view != null ? _view.Surface : null; }
        }

        public IReadOnlyList<PlanetRegion> Regions
        {
            get { return _view != null ? _view.Regions : (IReadOnlyList<PlanetRegion>)new PlanetRegion[0]; }
        }

        /// <summary>Planet body transform (rotates with the planet); null before <see cref="Build"/>.</summary>
        public Transform Body
        {
            get { return _view != null ? _view.BodyTransform : null; }
        }

        public bool IsBuilt
        {
            get { return _view != null && _view.IsBuilt; }
        }

        public float RenderRadius
        {
            get { return _view != null ? _view.Radius : _radius; }
        }

        private void Awake()
        {
            _definition = ResolveDefinition();

            if (_buildOnAwake)
            {
                Build();
            }
        }

        /// <summary>Builds the planet with the default seed (tests, tools).</summary>
        public void Build()
        {
            Build(0, true);
        }

        /// <summary>Builds the planet with a session seed and a quality level.</summary>
        public void Build(int sessionSeed, bool highQuality)
        {
            if (IsBuilt)
            {
                return;
            }

            if (_definition == null)
            {
                _definition = ResolveDefinition();
            }

            if (_view == null)
            {
                GameObject viewObject = new GameObject("Nexus-01");
                viewObject.transform.SetParent(transform, false);
                _view = viewObject.AddComponent<PlanetView>();
            }

            PlanetLighting.EnsureLighting();
            _view.Build(_definition, sessionSeed, _radius, highQuality);

            if (_rotationDegreesPerSecond > 0f)
            {
                _view.SetRotationDegreesPerSecond(_rotationDegreesPerSecond);
            }

            if (_surfaceColor.a <= 0f)
            {
                // Keep the serialized value usable even if a designer clears its alpha.
                _surfaceColor = new Color(0.078f, 0.294f, 0.412f, 1f);
            }
        }

        private PlanetDefinition ResolveDefinition()
        {
            IConfigurationService configuration;
            if (AppContext.IsReady && AppContext.TryGet(out configuration) && configuration.Game != null)
            {
                PlanetDefinition configured = configuration.Game.DefaultPlanet;
                if (configured != null)
                {
                    return configured;
                }
            }

            return PlanetDefinition.LoadDefault();
        }
    }
}
