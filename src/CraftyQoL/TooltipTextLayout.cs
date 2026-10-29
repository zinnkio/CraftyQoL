using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Fit the native generic popup to wrapped text, rather than to its old fixed text rectangle.
    internal sealed class TooltipTextLayout
    {
        private readonly RectTransform _root, _textRect;
        private readonly TMP_Text _text;
        private readonly HorizontalLayoutGroup _group;
        private readonly ContentSizeFitter _fitter;
        private readonly LayoutElement _element;
        private readonly bool _added, _controlWidth, _controlHeight, _expandWidth, _expandHeight;
        private readonly RectOffset _padding;
        private readonly ContentSizeFitter.FitMode _horizontal, _vertical;
        private readonly TextWrappingModes _wrapping;
        private readonly Vector2 _rootSize, _textSize;
        private readonly float _preferredWidth, _preferredHeight;
        private string? _lastText;
        private float _lastLimit;

        internal TooltipTextLayout(GameObject popup, TMP_Text text)
        {
            _root = (RectTransform)popup.transform; _text = text; _textRect = text.rectTransform;
            _group = popup.GetComponent<HorizontalLayoutGroup>(); _fitter = popup.GetComponent<ContentSizeFitter>();
            _rootSize = _root.sizeDelta; _textSize = _textRect.sizeDelta;
            _padding = _group.padding;
            _controlWidth = _group.childControlWidth; _controlHeight = _group.childControlHeight;
            _expandWidth = _group.childForceExpandWidth; _expandHeight = _group.childForceExpandHeight;
            _horizontal = _fitter.horizontalFit; _vertical = _fitter.verticalFit; _wrapping = _text.textWrappingMode;
            LayoutElement existing = text.GetComponent<LayoutElement>(); _added = !existing;
            _element = existing ? existing : text.gameObject.AddComponent<LayoutElement>();
            _preferredWidth = _element.preferredWidth; _preferredHeight = _element.preferredHeight;
            _group.padding = new RectOffset(8, 8, 6, 6);
            _group.childControlWidth = _group.childControlHeight = true;
            _group.childForceExpandWidth = _group.childForceExpandHeight = false;
            _fitter.horizontalFit = _fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _text.textWrappingMode = TextWrappingModes.Normal;
            Refresh();
        }
        internal void Refresh()
        {
            if (!_text || !_root) return;
            var canvas = _root.GetComponentInParent<Canvas>();
            var canvasRect = canvas ? canvas.transform as RectTransform : null;
            float limit = canvasRect ? Mathf.Min(360, Mathf.Max(1, canvasRect.rect.width - 32)) : 360;
            if (_lastText == _text.text && _lastLimit == limit) return;
            _lastText = _text.text; _lastLimit = limit;
            float width = Mathf.Clamp(_text.GetPreferredValues(_text.text, float.PositiveInfinity, float.PositiveInfinity).x, 1, limit);
            Vector2 measured = _text.GetPreferredValues(_text.text, width, float.PositiveInfinity);
            _element.preferredWidth = width;
            _element.preferredHeight = Mathf.Ceil(measured.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_root);
        }
        internal void Restore()
        {
            if (_text) _text.textWrappingMode = _wrapping;
            if (_element)
            {
                _element.preferredWidth = _preferredWidth; _element.preferredHeight = _preferredHeight;
                if (_added) { _element.enabled = false; Object.Destroy(_element); }
            }
            if (_group)
            {
                _group.padding = _padding;
                _group.childControlWidth = _controlWidth; _group.childControlHeight = _controlHeight;
                _group.childForceExpandWidth = _expandWidth; _group.childForceExpandHeight = _expandHeight;
            }
            if (_fitter) { _fitter.horizontalFit = _horizontal; _fitter.verticalFit = _vertical; }
            if (_textRect) _textRect.sizeDelta = _textSize;
            if (_root) { _root.sizeDelta = _rootSize; LayoutRebuilder.ForceRebuildLayoutImmediate(_root); }
        }
    }
}
