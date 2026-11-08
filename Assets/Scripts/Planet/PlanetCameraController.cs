using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Orbit camera for the planet view.
    ///
    /// Responsibilities:
    /// * smooth orbit (yaw/pitch) and exponential zoom with hard clamps,
    /// * damping towards the target pose so input never snaps the view,
    /// * framing transitions between the overview and a focused region,
    /// * keeping the camera outside the planet (no clipping) and inside sane distances.
    ///
    /// The controller is independent from input: <see cref="PlanetInputController"/> feeds it
    /// and tests call the same public methods. Nothing here reads simulation state.
    /// </summary>
    public sealed class PlanetCameraController : MonoBehaviour
    {
        private Camera _camera;
        private Transform _planet;
        private PlanetCameraSettings _settings = PlanetCameraSettings.Default;
        private float _radius = 3f;

        private float _yaw;
        private float _pitch = 14f;
        private float _distance;

        private float _targetYaw;
        private float _targetPitch = 14f;
        private float _targetDistance;

        private PlanetViewMode _mode = PlanetViewMode.Overview;

        public PlanetViewMode Mode
        {
            get { return _mode; }
        }

        public bool IsConfigured
        {
            get { return _camera != null && _planet != null; }
        }

        public PlanetCameraSettings Settings
        {
            get { return _settings; }
        }

        public float Radius
        {
            get { return _radius; }
        }

        /// <summary>Smoothed camera distance to the planet centre.</summary>
        public float Distance
        {
            get { return _distance; }
        }

        public float TargetDistance
        {
            get { return _targetDistance; }
        }

        public float YawDegrees
        {
            get { return _yaw; }
        }

        public float PitchDegrees
        {
            get { return _pitch; }
        }

        /// <summary>Direction from the planet centre towards the camera (world space).</summary>
        public Vector3 CameraDirection
        {
            get { return DirectionFor(_yaw, _pitch); }
        }

        /// <summary>True when the smoothed pose has reached the target pose.</summary>
        public bool IsSettled
        {
            get
            {
                return Mathf.Abs(_distance - _targetDistance) < (_radius * 0.002f) &&
                       Mathf.Abs(Mathf.DeltaAngle(_yaw, _targetYaw)) < 0.05f &&
                       Mathf.Abs(_pitch - _targetPitch) < 0.05f;
            }
        }

        /// <summary>Binds the controller to a camera, a planet and a radius.</summary>
        public void Configure(Camera camera, Transform planet, float radius)
        {
            Configure(camera, planet, radius, PlanetCameraSettings.Default);
        }

        public void Configure(Camera camera, Transform planet, float radius, PlanetCameraSettings settings)
        {
            _camera = camera;
            _planet = planet;
            _radius = Mathf.Max(0.1f, radius);
            _settings = settings;

            _targetDistance = _settings.OverviewDistance(_radius);
            _distance = _targetDistance;
            _yaw = 0f;
            _pitch = Mathf.Clamp(14f, _settings.MinPitchDegrees, _settings.MaxPitchDegrees);
            _targetYaw = _yaw;
            _targetPitch = _pitch;
            _mode = PlanetViewMode.Overview;

            ApplyTransform();
        }

        /// <summary>Orbits by a pixel delta (already measured since the last frame).</summary>
        public void Orbit(Vector2 pixelDelta)
        {
            if (!IsConfigured)
            {
                return;
            }

            _targetYaw = Mathf.Repeat(_targetYaw + (pixelDelta.x * _settings.OrbitDegreesPerPixel), 360f);
            _targetPitch = Mathf.Clamp(
                _targetPitch + (pixelDelta.y * _settings.OrbitDegreesPerPixel),
                _settings.MinPitchDegrees,
                _settings.MaxPitchDegrees);
        }

        /// <summary>Zooms by steps (positive = closer), clamped to the configured range.</summary>
        public void Zoom(float steps)
        {
            if (!IsConfigured || Mathf.Abs(steps) < 0.0001f)
            {
                return;
            }

            float factor = Mathf.Pow(1f - _settings.ZoomStep, steps);
            _targetDistance = Mathf.Clamp(
                _targetDistance * factor,
                _settings.MinDistance(_radius),
                _settings.MaxDistance(_radius));
        }

        /// <summary>
        /// Enters region focus: turns the camera towards <paramref name="worldDirection"/>
        /// (from the planet centre) and moves it closer.
        /// </summary>
        public void FocusOn(Vector3 worldDirection)
        {
            if (!IsConfigured || !PlanetGeo.IsUsableDirection(worldDirection))
            {
                return;
            }

            Vector3 direction = worldDirection.normalized;
            _targetPitch = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg,
                _settings.MinPitchDegrees,
                _settings.MaxPitchDegrees);
            _targetYaw = Mathf.Repeat(Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg, 360f);
            _targetDistance = _settings.FocusDistance(_radius);
            _mode = PlanetViewMode.RegionFocus;
        }

        /// <summary>Returns to the whole-planet framing without resetting the orbit angle.</summary>
        public void ReturnToOverview()
        {
            if (!IsConfigured)
            {
                return;
            }

            _targetDistance = _settings.OverviewDistance(_radius);
            _mode = PlanetViewMode.Overview;
        }

        /// <summary>Snaps the smoothed pose to the target pose (used after configure and in tests).</summary>
        public void SnapToTargets()
        {
            _yaw = _targetYaw;
            _pitch = _targetPitch;
            _distance = _targetDistance;
            ApplyTransform();
        }

        /// <summary>Advances the damping. Called from Update and directly by tests.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsConfigured)
            {
                return;
            }

            float frameDelta = Mathf.Clamp(deltaTime, 0f, 0.1f);
            float t = 1f - Mathf.Exp(-_settings.Damping * frameDelta);

            _yaw = Mathf.LerpAngle(_yaw, _targetYaw, t);
            _pitch = Mathf.Lerp(_pitch, _targetPitch, t);
            _distance = Mathf.Lerp(_distance, _targetDistance, t);

            ApplyTransform();
        }

        private void Update()
        {
            if (IsConfigured)
            {
                Tick(Time.unscaledDeltaTime);
            }
        }

        private void ApplyTransform()
        {
            if (!IsConfigured)
            {
                return;
            }

            Vector3 centre = _planet.position;
            Vector3 direction = DirectionFor(_yaw, _pitch);
            Vector3 position = centre + (direction * _distance);

            transform.position = position;
            transform.rotation = Quaternion.LookRotation(centre - position, Vector3.up);

            float nearPlane = Mathf.Max(0.03f, _radius * 0.005f);
            _camera.nearClipPlane = nearPlane;
            _camera.farClipPlane = Mathf.Max(_camera.farClipPlane, (_radius * 40f) + 100f);

            // Keep the far plane sane when the camera is far away; prevents depth precision
            // loss on mobile GPUs.
            _camera.farClipPlane = Mathf.Min(_camera.farClipPlane, (_distance + (_radius * 12f)) + 50f);
        }

        private static Vector3 DirectionFor(float yawDegrees, float pitchDegrees)
        {
            return (Quaternion.Euler(pitchDegrees, yawDegrees, 0f) * Vector3.back).normalized;
        }
    }
}
