using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Keeps the CanvasScaler correct across orientation and window size changes.
    /// Portrait phones scale from a 1080x1920 reference, landscape devices from 1920x1080,
    /// and the width/height match factor is derived from the aspect ratio so no layout is
    /// ever stretched or clipped.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class ResponsiveCanvas : MonoBehaviour
    {
        public static readonly Vector2 LandscapeReference = new Vector2(1920f, 1080f);
        public static readonly Vector2 PortraitReference = new Vector2(1080f, 1920f);

        private CanvasScaler _scaler;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                Apply();
            }
        }

        /// <summary>Recomputes reference resolution and match factor for the current screen.</summary>
        public void Apply()
        {
            if (_scaler == null)
            {
                _scaler = GetComponent<CanvasScaler>();
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            float aspect = _lastHeight > 0 ? (float)_lastWidth / _lastHeight : 16f / 9f;
            bool portrait = aspect < 1f;

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            _scaler.referenceResolution = portrait ? PortraitReference : LandscapeReference;
            _scaler.matchWidthOrHeight = ComputeMatchFactor(aspect);
        }

        /// <summary>
        /// 0 = match width (portrait), 1 = match height (ultra wide).
        /// Pure function so it can be unit tested.
        /// </summary>
        public static float ComputeMatchFactor(float aspect)
        {
            if (aspect <= 0f)
            {
                return 0.5f;
            }

            if (aspect < 1f)
            {
                return 0f;
            }

            if (aspect < 1.5f)
            {
                return 0.4f;
            }

            if (aspect < 2.1f)
            {
                return 0.5f;
            }

            return 1f;
        }
    }
}
