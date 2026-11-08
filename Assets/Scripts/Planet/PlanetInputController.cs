using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Nexus.Planet
{
    /// <summary>
    /// Translates mouse and touch input into planet commands.
    ///
    /// Mouse: drag to orbit, wheel to zoom, click to select a region.
    /// Touch: one finger drags to orbit, two fingers pinch to zoom, a tap selects a region.
    ///
    /// Gesture recognition lives here only; the camera and the planet expose plain methods,
    /// which keeps both testable without synthesising device input.
    /// </summary>
    public sealed class PlanetInputController : MonoBehaviour
    {
        private PlanetCameraController _cameraController;
        private PlanetView _view;
        private Camera _renderCamera;

        private bool _dragging;
        private Vector2 _lastPointerPosition;
        private float _dragDistance;
        private float _pressStartTime;
        private float _previousPinchDistance = -1f;

        /// <summary>Raised when a tap/click selects a region.</summary>
        public event Action<PlanetRegion> RegionActivated;

        /// <summary>Raised when a tap/click lands on the planet outside every region.</summary>
        public event Action EmptySpaceClicked;

        /// <summary>Disables input (used while a camera transition is running).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Maximum movement in pixels that still counts as a tap.</summary>
        public float TapMaxDragPixels { get; set; } = 10f;

        /// <summary>Maximum press duration in seconds that still counts as a tap.</summary>
        public float TapMaxDurationSeconds { get; set; } = 0.45f;

        public bool IsConfigured
        {
            get { return _cameraController != null && _view != null && _renderCamera != null; }
        }

        public void Initialize(PlanetCameraController cameraController, PlanetView view, Camera renderCamera)
        {
            _cameraController = cameraController;
            _view = view;
            _renderCamera = renderCamera;
            ResetGesture();
        }

        /// <summary>Orbits by a pixel delta (positive x = right, positive y = up).</summary>
        public void OrbitBy(Vector2 pixelDelta)
        {
            if (!InputEnabled || _cameraController == null)
            {
                return;
            }

            _cameraController.Orbit(pixelDelta);
        }

        /// <summary>Zooms by steps (positive = closer).</summary>
        public void ZoomBy(float steps)
        {
            if (!InputEnabled || _cameraController == null)
            {
                return;
            }

            _cameraController.Zoom(steps);
        }

        /// <summary>
        /// Selects the region under a screen point. Returns true when a region was hit and
        /// raised <see cref="RegionActivated"/>.
        /// </summary>
        public bool SelectAtScreenPoint(Vector2 screenPoint)
        {
            if (!InputEnabled || _view == null || _renderCamera == null)
            {
                return false;
            }

            PlanetRegion region;
            if (_view.TrySelectAtScreenPoint(_renderCamera, screenPoint))
            {
                region = _view.SelectedRegion;
                if (region != null && RegionActivated != null)
                {
                    RegionActivated(region);
                }

                return region != null;
            }

            if (EmptySpaceClicked != null)
            {
                EmptySpaceClicked();
            }

            return false;
        }

        /// <summary>Clears the selection, used by the Escape key and the panel close button.</summary>
        public void ClearSelection()
        {
            if (_view != null)
            {
                _view.ClearSelection();
            }

            if (EmptySpaceClicked != null)
            {
                EmptySpaceClicked();
            }
        }

        private void Update()
        {
            if (!IsConfigured || !InputEnabled)
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClearSelection();
                return;
            }

            HandleTouch();
            HandleMouse();
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // Unity simulates a mouse from touches; never drive the camera twice.
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
            {
                BeginDrag(position);
            }
            else if (_dragging && mouse.leftButton.isPressed)
            {
                ContinueDrag(position);
            }
            else if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                FinishDrag(position);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
            {
                ZoomBy(scroll / 120f);
            }
        }

        private void HandleTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            var touches = touchscreen.touches;
            int pressedCount = 0;
            Vector2 first = Vector2.zero;
            Vector2 second = Vector2.zero;

            for (int i = 0; i < touches.Count; i++)
            {
                if (!touches[i].press.isPressed)
                {
                    continue;
                }

                if (pressedCount == 0)
                {
                    first = touches[i].position.ReadValue();
                }
                else if (pressedCount == 1)
                {
                    second = touches[i].position.ReadValue();
                }

                pressedCount++;
            }

            if (pressedCount >= 2)
            {
                float distance = Vector2.Distance(first, second);
                if (_previousPinchDistance > 0f)
                {
                    float delta = distance - _previousPinchDistance;
                    float steps = delta / Mathf.Max(80f, Screen.height * 0.25f);
                    ZoomBy(steps * 2f);
                }

                _previousPinchDistance = distance;
                _dragging = false;
                return;
            }

            _previousPinchDistance = -1f;

            if (pressedCount == 1)
            {
                if (!_dragging)
                {
                    BeginDrag(first);
                }
                else
                {
                    ContinueDrag(first);
                }

                return;
            }

            if (_dragging)
            {
                FinishDrag(_lastPointerPosition);
            }
        }

        private void BeginDrag(Vector2 position)
        {
            _dragging = true;
            _lastPointerPosition = position;
            _dragDistance = 0f;
            _pressStartTime = Time.unscaledTime;
        }

        private void ContinueDrag(Vector2 position)
        {
            Vector2 delta = position - _lastPointerPosition;
            _dragDistance += delta.magnitude;
            _lastPointerPosition = position;

            if (_dragDistance > TapMaxDragPixels)
            {
                OrbitBy(delta);
            }
        }

        private void FinishDrag(Vector2 position)
        {
            bool isTap = _dragDistance <= TapMaxDragPixels &&
                         (Time.unscaledTime - _pressStartTime) <= TapMaxDurationSeconds &&
                         !IsPointerOverUi();

            ResetGesture();

            if (isTap)
            {
                SelectAtScreenPoint(position);
            }
        }

        private void ResetGesture()
        {
            _dragging = false;
            _dragDistance = 0f;
            _previousPinchDistance = -1f;
        }

        private static bool IsPointerOverUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.isPressed && eventSystem.IsPointerOverGameObject(touches[i].touchId.ReadValue()))
                    {
                        return true;
                    }
                }
            }

            return eventSystem.IsPointerOverGameObject();
        }
    }
}
