using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Orbit camera behaviour: clamping, damping and the overview/focus transitions. These
    /// run in EditMode because the controller only needs a transform and a camera, not play
    /// mode physics or input.
    /// </summary>
    public class PlanetCameraControllerTests
    {
        private const float Radius = 3f;

        private GameObject _cameraObject;
        private GameObject _planetObject;
        private Camera _camera;
        private PlanetCameraController _controller;

        [SetUp]
        public void SetUp()
        {
            _planetObject = new GameObject("TestPlanet");
            _planetObject.transform.position = Vector3.zero;

            _cameraObject = new GameObject("TestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _controller = _cameraObject.AddComponent<PlanetCameraController>();
            _controller.Configure(_camera, _planetObject.transform, Radius);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
            {
                Object.DestroyImmediate(_cameraObject);
            }

            if (_planetObject != null)
            {
                Object.DestroyImmediate(_planetObject);
            }
        }

        [Test]
        public void Configure_AppliesOverviewFraming()
        {
            Assert.IsTrue(_controller.IsConfigured);
            Assert.AreEqual(PlanetViewMode.Overview, _controller.Mode);
            Assert.AreEqual(Radius * PlanetCameraSettings.Default.OverviewDistanceFactor, _controller.Distance, 0.001f);
            Assert.Greater(_controller.Distance, Radius, "The camera must start outside the planet.");

            Vector3 toCamera = _camera.transform.position - _planetObject.transform.position;
            Assert.AreEqual(_controller.Distance, toCamera.magnitude, 0.01f);
        }

        [Test]
        public void Zoom_IsClampedToConfiguredRange()
        {
            PlanetCameraSettings settings = PlanetCameraSettings.Default;

            for (int i = 0; i < 200; i++)
            {
                _controller.Zoom(1f);
            }

            Assert.AreEqual(settings.MinDistance(Radius), _controller.TargetDistance, 0.001f);
            Assert.Greater(_controller.TargetDistance, Radius, "The minimum distance must stay outside the planet.");

            for (int i = 0; i < 400; i++)
            {
                _controller.Zoom(-1f);
            }

            Assert.AreEqual(settings.MaxDistance(Radius), _controller.TargetDistance, 0.001f);
        }

        [Test]
        public void Orbit_ClampsPitch()
        {
            for (int i = 0; i < 50; i++)
            {
                _controller.Orbit(new Vector2(40f, 200f));
            }

            _controller.SnapToTargets();
            Assert.LessOrEqual(_controller.PitchDegrees, PlanetCameraSettings.Default.MaxPitchDegrees + 0.001f);

            for (int i = 0; i < 100; i++)
            {
                _controller.Orbit(new Vector2(0f, -200f));
            }

            _controller.SnapToTargets();
            Assert.GreaterOrEqual(_controller.PitchDegrees, PlanetCameraSettings.Default.MinPitchDegrees - 0.001f);
        }

        [Test]
        public void FocusOn_TurnsTheCameraTowardsTheTargetDirection()
        {
            Vector3 direction = PlanetGeo.DirectionFromLatLon(32f, -75f);

            _controller.FocusOn(direction);
            _controller.SnapToTargets();

            Assert.AreEqual(PlanetViewMode.RegionFocus, _controller.Mode);
            Assert.Less(Vector3.Angle(_controller.CameraDirection, direction), 1f);
            Assert.AreEqual(
                PlanetCameraSettings.Default.FocusDistance(Radius),
                _controller.Distance,
                0.001f);
        }

        [Test]
        public void FocusOn_RejectsDegenerateDirection()
        {
            _controller.FocusOn(Vector3.zero);

            Assert.AreEqual(PlanetViewMode.Overview, _controller.Mode);
        }

        [Test]
        public void ReturnToOverview_RestoresDistance()
        {
            _controller.FocusOn(PlanetGeo.DirectionFromLatLon(10f, 10f));
            _controller.SnapToTargets();

            _controller.ReturnToOverview();
            _controller.SnapToTargets();

            Assert.AreEqual(PlanetViewMode.Overview, _controller.Mode);
            Assert.AreEqual(
                PlanetCameraSettings.Default.OverviewDistance(Radius),
                _controller.Distance,
                0.001f);
        }

        [Test]
        public void Tick_DampsTowardsTheTargetWithoutOvershoot()
        {
            _controller.Zoom(3f);
            float target = _controller.TargetDistance;
            float start = _controller.Distance;

            for (int i = 0; i < 10; i++)
            {
                _controller.Tick(0.016f);
            }

            Assert.Less(_controller.Distance, start, "Zooming in must reduce the distance.");
            Assert.GreaterOrEqual(_controller.Distance, target - 0.0001f, "The damping must not overshoot.");
        }

        [Test]
        public void Tick_EventuallySettles()
        {
            _controller.Orbit(new Vector2(120f, 40f));
            _controller.Zoom(2f);

            for (int i = 0; i < 600; i++)
            {
                _controller.Tick(0.016f);
            }

            Assert.IsTrue(_controller.IsSettled);
        }

        [Test]
        public void Camera_NearPlaneStaysTinyRelativeToPlanet()
        {
            Assert.Greater(_camera.nearClipPlane, 0f);
            Assert.Less(_camera.nearClipPlane, Radius * 0.1f);
        }
    }
}
