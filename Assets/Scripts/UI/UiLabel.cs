using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nexus.UI
{
    /// <summary>
    /// Small facade over either a TextMeshPro label or a legacy uGUI label.
    /// The shell prefers TextMeshPro; the fallback keeps the project playable in a fresh
    /// clone where the TMP Essential Resources have not been imported yet.
    /// </summary>
    public sealed class UiLabel
    {
        private readonly TextMeshProUGUI _textMeshPro;
        private readonly Text _legacyText;

        public GameObject GameObject { get; }

        public RectTransform RectTransform { get; }

        public bool UsesTextMeshPro
        {
            get { return _textMeshPro != null; }
        }

        public UiLabel(TextMeshProUGUI textMeshPro)
        {
            _textMeshPro = textMeshPro;
            GameObject = textMeshPro.gameObject;
            RectTransform = textMeshPro.rectTransform;
        }

        public UiLabel(Text legacyText)
        {
            _legacyText = legacyText;
            GameObject = legacyText.gameObject;
            RectTransform = legacyText.rectTransform;
        }

        public string Text
        {
            get
            {
                if (_textMeshPro != null)
                {
                    return _textMeshPro.text;
                }

                return _legacyText != null ? _legacyText.text : string.Empty;
            }
            set
            {
                if (_textMeshPro != null)
                {
                    _textMeshPro.text = value;
                }
                else if (_legacyText != null)
                {
                    _legacyText.text = value;
                }
            }
        }

        public void SetColor(Color color)
        {
            if (_textMeshPro != null)
            {
                _textMeshPro.color = color;
            }
            else if (_legacyText != null)
            {
                _legacyText.color = color;
            }
        }

        public void SetFontSize(float size)
        {
            if (_textMeshPro != null)
            {
                _textMeshPro.fontSize = size;
            }
            else if (_legacyText != null)
            {
                _legacyText.fontSize = Mathf.RoundToInt(size);
            }
        }

        public void SetCharacterSpacing(float spacing)
        {
            if (_textMeshPro != null)
            {
                _textMeshPro.characterSpacing = spacing;
            }
        }

        public void SetVisible(bool visible)
        {
            if (GameObject != null)
            {
                GameObject.SetActive(visible);
            }
        }
    }
}
