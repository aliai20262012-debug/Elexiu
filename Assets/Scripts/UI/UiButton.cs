using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>Reference bundle for a themed button so views can update text and state.</summary>
    public sealed class UiButton
    {
        public Button Button { get; }

        public UiLabel Label { get; }

        public RectTransform RectTransform { get; }

        public Image Background { get; }

        public UiButton(Button button, UiLabel label, RectTransform rectTransform, Image background)
        {
            Button = button;
            Label = label;
            RectTransform = rectTransform;
            Background = background;
        }

        public bool Interactable
        {
            get { return Button != null && Button.interactable; }
            set
            {
                if (Button != null)
                {
                    Button.interactable = value;
                }
            }
        }

        /// <summary>Text style used for the primary (accent) action of a screen.</summary>
        public void ApplyPrimaryStyle(Color textColor)
        {
            if (Label != null)
            {
                Label.SetColor(textColor);
            }
        }
    }
}
