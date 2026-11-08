using UnityEngine;
using TMPro;

namespace Nexus.Configuration
{
    /// <summary>
    /// Presentation theme for the shell UI: deep navy surfaces, electric cyan accents,
    /// subtle green highlights. Keeping colours and sizes in an asset lets the art
    /// direction evolve without touching layout code.
    /// </summary>
    [CreateAssetMenu(menuName = "Nexus/UI/Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Palette")]
        [SerializeField] private Color _backgroundDeepNavy = new Color(0.035f, 0.055f, 0.110f, 1f);
        [SerializeField] private Color _surfaceNavy = new Color(0.063f, 0.094f, 0.165f, 0.92f);
        [SerializeField] private Color _panelNavy = new Color(0.047f, 0.071f, 0.129f, 0.98f);
        [SerializeField] private Color _primaryCyan = new Color(0.133f, 0.827f, 0.933f, 1f);
        [SerializeField] private Color _accentGreen = new Color(0.204f, 0.827f, 0.600f, 1f);
        [SerializeField] private Color _textPrimary = new Color(0.902f, 0.949f, 0.980f, 1f);
        [SerializeField] private Color _textMuted = new Color(0.408f, 0.502f, 0.612f, 1f);
        [SerializeField] private Color _danger = new Color(0.937f, 0.353f, 0.353f, 1f);

        [Header("Typography")]
        [SerializeField] private float _titleFontSize = 92f;
        [SerializeField] private float _sectionFontSize = 22f;
        [SerializeField] private float _buttonFontSize = 26f;
        [SerializeField] private float _bodyFontSize = 18f;
        [SerializeField] private float _titleCharacterSpacing = 18f;

        [Header("Optional")]
        [Tooltip("Leave empty to use the TextMeshPro default font asset.")]
        [SerializeField] private TMP_FontAsset _fontOverride;

        public Color BackgroundDeepNavy { get { return _backgroundDeepNavy; } }

        public Color SurfaceNavy { get { return _surfaceNavy; } }

        public Color PanelNavy { get { return _panelNavy; } }

        public Color PrimaryCyan { get { return _primaryCyan; } }

        public Color AccentGreen { get { return _accentGreen; } }

        public Color TextPrimary { get { return _textPrimary; } }

        public Color TextMuted { get { return _textMuted; } }

        public Color Danger { get { return _danger; } }

        public float TitleFontSize { get { return _titleFontSize; } }

        public float SectionFontSize { get { return _sectionFontSize; } }

        public float ButtonFontSize { get { return _buttonFontSize; } }

        public float BodyFontSize { get { return _bodyFontSize; } }

        public float TitleCharacterSpacing { get { return _titleCharacterSpacing; } }

        public TMP_FontAsset FontOverride { get { return _fontOverride; } }

        public static UiTheme CreateDefault()
        {
            UiTheme theme = CreateInstance<UiTheme>();
            theme.name = "UiTheme (runtime default)";
            theme.hideFlags = HideFlags.DontSave;
            return theme;
        }
    }
}
