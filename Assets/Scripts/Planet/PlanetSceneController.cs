using Nexus.Core.Events;
using Nexus.Core.Platform;
using Nexus.UI;
using UnityEngine;

namespace Nexus.Planet
{
    /// <summary>
    /// Coordinates the planet presentation: input, orbit camera, selection highlight, region
    /// panel and the planet day feed.
    ///
    /// It is the only place that connects player input to camera motion and UI, so the
    /// simulation system, the camera and the panel all stay independently testable.
    /// </summary>
    public sealed class PlanetSceneController : MonoBehaviour
    {
        private PlanetViewController _planet;
        private PlanetCameraController _cameraController;
        private PlanetInputController _input;
        private RegionInfoPanel _panel;
        private IEventBus _eventBus;

        private bool _initialized;
        private int _selectedRegionId = RegionLocator.NoRegion;
        private long _dayNumber;
        private float _seasonPhase;

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public PlanetViewController Planet
        {
            get { return _planet; }
        }

        public PlanetCameraController CameraController
        {
            get { return _cameraController; }
        }

        public PlanetInputController Input
        {
            get { return _input; }
        }

        public RegionInfoPanel Panel
        {
            get { return _panel; }
        }

        public PlanetViewMode Mode
        {
            get { return _cameraController != null ? _cameraController.Mode : PlanetViewMode.Overview; }
        }

        public int SelectedRegionId
        {
            get { return _selectedRegionId; }
        }

        /// <summary>Wires the planet, camera, input and panel together. Safe to call once.</summary>
        public void Initialize(
            PlanetViewController planet,
            GameplayHud hud,
            Camera camera,
            IEventBus eventBus,
            IPlatformInfo platform)
        {
            if (_initialized || planet == null || camera == null)
            {
                return;
            }

            _planet = planet;
            _eventBus = eventBus;
            _panel = hud != null ? hud.RegionPanel : null;

            PlanetCameraSettings settings = platform != null && platform.IsMobile && !platform.IsTablet
                ? PlanetCameraSettings.Compact
                : PlanetCameraSettings.Default;

            _cameraController = camera.GetComponent<PlanetCameraController>();
            if (_cameraController == null)
            {
                _cameraController = camera.gameObject.AddComponent<PlanetCameraController>();
            }

            _cameraController.Configure(camera, planet.transform, planet.RenderRadius, settings);

            _input = camera.GetComponent<PlanetInputController>();
            if (_input == null)
            {
                _input = camera.gameObject.AddComponent<PlanetInputController>();
            }

            _input.Initialize(_cameraController, planet.View, camera);
            _input.RegionActivated += HandleRegionActivated;
            _input.EmptySpaceClicked += HandleEmptySpaceClicked;

            PlanetView view = planet.View;
            if (view != null)
            {
                view.RegionSelected += HandleViewRegionSelected;
                view.SelectionCleared += HandleViewSelectionCleared;
                view.RotationEnabled = true;
            }

            if (_panel != null)
            {
                _panel.SetVisible(false);
                _panel.FocusRequested += HandleFocusRequested;
                _panel.OverviewRequested += ReturnToOverview;
            }

            if (_eventBus != null)
            {
                _eventBus.Subscribe<PlanetEnvironmentUpdatedEvent>(HandleEnvironmentUpdated);
            }

            _initialized = true;
        }

        /// <summary>Called by the input controller when a tap lands inside a region.</summary>
        public void HandleRegionActivated(PlanetRegion region)
        {
            if (region == null || _planet == null || _planet.View == null)
            {
                return;
            }

            _planet.View.SelectRegion(region.Id);
        }

        /// <summary>Flies the camera to the selected region without changing the selection.</summary>
        public void HandleFocusRequested()
        {
            if (_planet == null || _planet.View == null || _cameraController == null)
            {
                return;
            }

            PlanetRegion region = _planet.View.SelectedRegion;
            if (region == null)
            {
                return;
            }

            _planet.View.RotationEnabled = false;
            _cameraController.FocusOn(_planet.View.WorldDirectionOf(region));
        }

        /// <summary>Returns to the planet overview and clears the selection.</summary>
        public void ReturnToOverview()
        {
            if (_planet == null || _planet.View == null)
            {
                return;
            }

            _planet.View.ClearSelection();
            _planet.View.RotationEnabled = true;

            if (_cameraController != null)
            {
                _cameraController.ReturnToOverview();
            }

            if (_panel != null)
            {
                _panel.Hide();
            }
        }

        private void HandleEmptySpaceClicked()
        {
            ReturnToOverview();
        }

        private void HandleViewRegionSelected(PlanetRegion region)
        {
            if (region == null)
            {
                return;
            }

            _selectedRegionId = region.Id;

            if (_panel != null)
            {
                _panel.Show(region);
                _panel.SetEnvironment(
                    PlanetSimulationState.TemperatureFor(region, _seasonPhase),
                    PlanetLabels.Season(_seasonPhase),
                    _dayNumber);
            }

            HandleFocusRequested();

            if (_eventBus != null)
            {
                _eventBus.Publish(new PlanetRegionSelectedEvent(region.Id, region.Code, region.Name));
            }
        }

        private void HandleViewSelectionCleared()
        {
            int previous = _selectedRegionId;
            _selectedRegionId = RegionLocator.NoRegion;

            if (_panel != null)
            {
                _panel.Hide();
            }

            if (previous != RegionLocator.NoRegion && _eventBus != null)
            {
                _eventBus.Publish(new PlanetRegionClearedEvent(previous));
            }
        }

        private void HandleEnvironmentUpdated(PlanetEnvironmentUpdatedEvent gameEvent)
        {
            _dayNumber = gameEvent.DayNumber;
            _seasonPhase = gameEvent.SeasonPhase;

            if (_panel == null || !_panel.IsVisible || _panel.Region == null)
            {
                return;
            }

            _panel.SetEnvironment(
                PlanetSimulationState.TemperatureFor(_panel.Region, _seasonPhase),
                PlanetLabels.Season(_seasonPhase),
                _dayNumber);
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.RegionActivated -= HandleRegionActivated;
                _input.EmptySpaceClicked -= HandleEmptySpaceClicked;
            }

            if (_planet != null && _planet.View != null)
            {
                _planet.View.RegionSelected -= HandleViewRegionSelected;
                _planet.View.SelectionCleared -= HandleViewSelectionCleared;
            }

            if (_panel != null)
            {
                _panel.FocusRequested -= HandleFocusRequested;
                _panel.OverviewRequested -= ReturnToOverview;
            }

            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<PlanetEnvironmentUpdatedEvent>(HandleEnvironmentUpdated);
            }

            _initialized = false;
        }
    }
}
