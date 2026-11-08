using UnityEngine;

namespace Nexus.UI
{
    /// <summary>
    /// Fits a full screen container inside Screen.safeArea so that notches, punch holes and
    /// rounded corners on phones and tablets never cover interactive UI. On desktop the
    /// safe area equals the screen and the component is a no-op.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastWidth;
        private int _lastHeight;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                Apply();
            }
        }

        public void Apply()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Rect safeArea = Screen.safeArea;

            _lastSafeArea = safeArea;
            _lastWidth = screenWidth;
            _lastHeight = screenHeight;

            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= screenWidth;
            anchorMin.y /= screenHeight;
            anchorMax.x /= screenWidth;
            anchorMax.y /= screenHeight;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
