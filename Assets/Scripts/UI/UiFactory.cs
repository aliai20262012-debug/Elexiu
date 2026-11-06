using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Nexus.Configuration;

namespace Nexus.UI
{
    public enum UiTextAlignment
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    /// <summary>
    /// Builds the runtime shell UI from code. Creating the interface procedurally keeps the
    /// scenes tiny (no fragile prefab or scene references) and guarantees the same structure
    /// on every aspect ratio. All sizes are canvas reference units, so they scale with the
    /// CanvasScaler instead of being hard coded pixels.
    /// </summary>
    public static class UiFactory
    {
        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)canvasObject.transform;

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = ResponsiveCanvas.LandscapeReference;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<ResponsiveCanvas>();

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return canvas;
        }

        /// <summary>Guarantees exactly one EventSystem using the Input System UI module.</summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                return existing;
            }

            GameObject eventSystemObject = new GameObject("[EventSystem]");
            EventSystem eventSystem = eventSystemObject.AddComponent<EventSystem>();

            // The Input System UI module assigns its default navigation/point actions in OnEnable.
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            Object.DontDestroyOnLoad(eventSystemObject);
            return eventSystem;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        public static Image CreateImage(RectTransform parent, string name, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static UiLabel CreateLabel(
            RectTransform parent,
            string name,
            string text,
            float fontSize,
            Color color,
            UiTextAlignment alignment,
            float characterSpacing = 0f)
        {
            RectTransform rect = CreateRect(parent, name);

            if (UiTextBackend.IsTextMeshProAvailable)
            {
                TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
                if (UiTextBackend.TextMeshProFont != null)
                {
                    label.font = UiTextBackend.TextMeshProFont;
                }

                label.text = text;
                label.fontSize = fontSize;
                label.color = color;
                label.characterSpacing = characterSpacing;
                label.alignment = ToTextMeshProAlignment(alignment);
                label.raycastTarget = false;
                return new UiLabel(label);
            }

            Text legacyLabel = rect.gameObject.AddComponent<Text>();
            legacyLabel.font = UiTextBackend.LegacyFont;
            legacyLabel.text = text;
            legacyLabel.fontSize = Mathf.RoundToInt(fontSize);
            legacyLabel.color = color;
            legacyLabel.alignment = ToTextAnchor(alignment);
            legacyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            legacyLabel.verticalOverflow = VerticalWrapMode.Overflow;
            legacyLabel.raycastTarget = false;
            return new UiLabel(legacyLabel);
        }

        public static UiButton CreateButton(RectTransform parent, string name, string labelText, UiTheme theme)
        {
            RectTransform rect = CreateRect(parent, name);

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = theme.SurfaceNavy;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = theme.PrimaryCyan;
            colors.pressedColor = theme.AccentGreen;
            colors.selectedColor = theme.PrimaryCyan;
            colors.disabledColor = new Color(0.55f, 0.60f, 0.68f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 74f;
            layout.minHeight = 56f;
            layout.flexibleHeight = 0f;

            UiLabel label = CreateLabel(
                rect,
                "Label",
                labelText,
                theme.ButtonFontSize,
                theme.TextPrimary,
                UiTextAlignment.Center,
                4f);

            Stretch(label.RectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 4f), new Vector2(-28f, -4f));

            return new UiButton(button, label, rect, background);
        }

        public static VerticalLayoutGroup CreateVerticalStack(RectTransform parent, string name, float spacing)
        {
            RectTransform rect = CreateRect(parent, name);

            VerticalLayoutGroup group = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = true;
            group.padding = new RectOffset(0, 0, 0, 0);
            return group;
        }

        /// <summary>Stretches a rect between anchors, applying inset offsets.</summary>
        public static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        /// <summary>Places a rect at a fixed size relative to a normalised anchor point.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        public static TextAlignmentOptions ToTextMeshProAlignment(UiTextAlignment alignment)
        {
            switch (alignment)
            {
                case UiTextAlignment.Left:
                    return TextAlignmentOptions.Left;

                case UiTextAlignment.Right:
                    return TextAlignmentOptions.Right;

                default:
                    return TextAlignmentOptions.Center;
            }
        }

        public static TextAnchor ToTextAnchor(UiTextAlignment alignment)
        {
            switch (alignment)
            {
                case UiTextAlignment.Left:
                    return TextAnchor.MiddleLeft;

                case UiTextAlignment.Right:
                    return TextAnchor.MiddleRight;

                default:
                    return TextAnchor.MiddleCenter;
            }
        }
    }
}
