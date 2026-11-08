using TMPro;
using UnityEngine;

namespace Nexus.UI
{
    /// <summary>
    /// Detects at runtime whether TextMeshPro has usable font resources.
    /// TMP Essential Resources are imported by the editor on first use; until then the
    /// shell falls back to legacy uGUI text instead of rendering invisible labels.
    /// </summary>
    public static class UiTextBackend
    {
        private const string TmpDefaultFontResource = "Fonts & Materials/LiberationSans SDF";
        private const string LegacyFontResource = "LegacyRuntime.ttf";
        private const string LegacyFontResourceFallback = "Arial.ttf";

        private static bool _resolved;
        private static bool _textMeshProAvailable;
        private static TMP_FontAsset _tmpFont;
        private static Font _legacyFont;

        public static bool IsTextMeshProAvailable
        {
            get
            {
                Resolve();
                return _textMeshProAvailable;
            }
        }

        public static TMP_FontAsset TextMeshProFont
        {
            get
            {
                Resolve();
                return _tmpFont;
            }
        }

        public static Font LegacyFont
        {
            get
            {
                Resolve();
                return _legacyFont;
            }
        }

        public static void Reset()
        {
            _resolved = false;
            _tmpFont = null;
            _legacyFont = null;
            _textMeshProAvailable = false;
        }

        private static void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;

            try
            {
                // TMP_Settings.defaultFontAsset is static and returns null when the
                // TMP Essential Resources have not been imported yet.
                TMP_FontAsset font = TMP_Settings.defaultFontAsset;
                if (font == null)
                {
                    font = Resources.Load<TMP_FontAsset>(TmpDefaultFontResource);
                }

                _tmpFont = font;
            }
            catch (System.Exception)
            {
                _tmpFont = null;
            }

            _textMeshProAvailable = _tmpFont != null;

            if (_textMeshProAvailable)
            {
                return;
            }

            try
            {
                _legacyFont = Resources.GetBuiltinResource<Font>(LegacyFontResource);
            }
            catch (System.Exception)
            {
                _legacyFont = null;
            }

            if (_legacyFont == null)
            {
                try
                {
                    _legacyFont = Resources.GetBuiltinResource<Font>(LegacyFontResourceFallback);
                }
                catch (System.Exception)
                {
                    _legacyFont = null;
                }
            }
        }
    }
}
