using NUnit.Framework;
using Nexus.Planet;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    /// <summary>
    /// Command layer of the planet input: orbit, zoom, gating and click-to-select. The gesture
    /// recognition itself (mouse drag versus touch pinch) feeds these methods, so testing them
    /// covers both input paths without synthesising device events.
    /// </summary>
    public class PlanetInputControllerTests
    {
        private GameObject _cameraObject;
        private GameObject _planetObject;
        private Camera _camera;
        private PlanetCameraController _cameraController;
        private PlanetView _view;
        private PlanetInputController _input;
        private PlanetDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = PlanetDefinition.CreateRuntimeDefault();

            _planetObject = new GameObject("InputTestPlanet");
            _view = _planetObject.AddComponent<PlanetView>();
            _view.Build(_definition, 8080, PlanetView.DefaultRenderRadius, false);

            _cameraObject = new GameObject("InputTestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
            _cameraController = _cameraObject.AddComponent<PlanetCameraController>();
            _cameraController.Configure(_camera, _planetObject.transform, _view.Radius);

            _input = _cameraObject.AddComponent<PlanetInputController>();
            _input.Initialize(_cameraController, _view, _camera);

            Physics.SyncTransforms();
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

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        public void OrbitBy_RotatesTheCameraTarget()
        {
            float yawBefore = _cameraController.YawDegrees;

            _input.OrbitBy(new Vector2(120f, 0f));
            _cameraController.SnapToTargets();

            Assert.AreNotEqual(yawBefore, _cameraController.YawDegrees, 0.001f);
        }

        [Test]
        public void ZoomBy_MovesTheTargetDistance()
        {
            float before = _cameraController.TargetDistance;

            _input.ZoomBy(2f);

            Assert.Less(_cameraController.TargetDistance, before);
        }

        [Test]
        public void ZoomBy_IsClampedByTheCamera()
        {
            for (int i = 0; i < 200; i++)
            {
                _input.ZoomBy(1f);
            }

            Assert.AreEqual(
                PlanetCameraSettings.Default.MinDistance(_view.Radius),
                _cameraController.TargetDistance,
                0.001f);
            Assert.Greater(_cameraController.TargetDistance, _view.Radius);
        }

        [Test]
        public void DisabledInput_IsIgnored()
        {
            _input.InputEnabled = false;

            float yawBefore = _cameraController.YawDegrees;
            float distanceBefore = _cameraController.TargetDistance;

            _input.OrbitBy(new Vector2(200f, 80f));
            _input.ZoomBy(3f);
            _cameraController.SnapToTargets();

            Assert.AreEqual(yawBefore, _cameraController.YawDegrees, 0.001f);
            Assert.AreEqual(distanceBefore, _cameraController.TargetDistance, 0.001f);
            Assert.IsFalse(_input.SelectAtScreenPoint(Vector2.zero));
        }

        [Test]
        public void SelectAtScreenPoint_OnThePlanet_SelectsARegion()
        {
            PlanetRegion activated = null;
            _input.RegionActivated += region => activated = region;

            Vector3 screen = _camera.WorldToScreenPoint(_planetObject.transform.position);
            bool selected = _input.SelectAtScreenPoint(new Vector2(screen.x, screen.y));

            Assert.IsTrue(selected, "The planet centre must resolve to a region.");
            Assert.IsNotNull(activated);
            Assert.AreEqual(activated.Id, _view.SelectedRegionId);
            Assert.IsTrue(_view.HasSelectionHighlight);
        }

        [Test]
        public void SelectAtScreenPoint_OffThePlanet_RaisesEmptySpaceClick()
        {
            int emptyClicks = 0;
            _input.EmptySpaceClicked += () => emptyClicks++;

            bool selected = _input.SelectAtScreenPoint(new Vector2(-5000f, -5000f));

            Assert.IsFalse(selected);
            Assert.AreEqual(1, emptyClicks);
            Assert.AreEqual(RegionLocator.NoRegion, _view.SelectedRegionId);
        }

        [Test]
        public void ClearSelection_ResetsViewAndRaisesEmptySpaceClick()
        {
            _view.SelectRegion(_view.Regions[0].Id);

            int emptyClicks = 0;
            _input.EmptySpaceClicked += () => emptyClicks++;

            _input.ClearSelection();

            Assert.AreEqual(RegionLocator.NoRegion, _view.SelectedRegionId);
            Assert.AreEqual(1, emptyClicks);
        }
    }
}
