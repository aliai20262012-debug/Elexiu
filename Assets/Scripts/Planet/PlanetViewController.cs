using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.Planet
{
    /// <summary>
    /// Placeholder planet body for the gameplay shell. Phase 01 only proves that the gameplay
    /// scene, the simulation clock and the presentation layer are wired together; real planet
    /// generation (heightfields, biomes, rings) is Phase 03 scope.
    /// </summary>
    public sealed class PlanetViewController : MonoBehaviour
    {
        [SerializeField] private float _rotationDegreesPerSecond = 5f;
        [SerializeField] private float _radius = 3f;
        [SerializeField] private Color _surfaceColor = new Color(0.078f, 0.294f, 0.412f, 1f);

        private Transform _body;

        public Transform Body
        {
            get { return _body; }
        }

        public bool IsBuilt
        {
            get { return _body != null; }
        }

        private void Awake()
        {
            Build();
        }

        private void Update()
        {
            if (_body != null)
            {
                _body.Rotate(Vector3.up, _rotationDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }

        public void Build()
        {
            if (_body != null)
            {
                return;
            }

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "PlanetBody";
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * (_radius * 2f);

            // No physics in the shell phase: keep the scene free of colliders.
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = CreateSurfaceMaterial();
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
            }

            _body = sphere.transform;
        }

        private Material CreateSurfaceMaterial()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Material source = pipeline != null ? pipeline.defaultMaterial : null;

            Material material = source != null ? new Material(source) : null;

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader);
            }

            ApplyColor(material, _surfaceColor);
            return material;
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
