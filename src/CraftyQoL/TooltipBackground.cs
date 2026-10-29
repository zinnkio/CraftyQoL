using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CraftyQoL.Core;

namespace CraftyQoL
{
    // Native UITooltip shares one live popup. Style the instance, never its prefab.
    internal sealed class TooltipBackground
    {
        private static readonly FieldInfo? CurrentField = typeof(UITooltip).GetField("m_current", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo? PopupField = typeof(UITooltip).GetField("m_tooltip", BindingFlags.Static | BindingFlags.NonPublic);
        private UITooltip? _current;
        private GameObject? _popup;
        private Image? _background;
        private TooltipTextLayout? _textLayout;
        private RoundedImage? _rounding;
        private bool _addedRounding;
        private readonly List<TextState> _texts=new List<TextState>();
        private sealed class TextState
        {internal TMP_Text Text=null!;internal bool Rich,Override; }
        private Color _original, _written;
        private bool _added, _disabled;

        internal void Update(NativeToolbar? toolbar, List<RecipeRow>? rows, HashSet<GameObject>? rowActions, GameObject? owner = null)
        {
            if (_disabled || CurrentField?.FieldType != typeof(UITooltip) || PopupField?.FieldType != typeof(GameObject)) return;
            try
            {
                var current = CurrentField.GetValue(null) as UITooltip;
                var popup = PopupField.GetValue(null) as GameObject;
                if (_current == current && _popup == popup) { _textLayout?.Refresh(); return; }
                bool owned = false;
                if (current && popup)
                {
                    owned = (toolbar?.Contains(current.gameObject) == true) || (rowActions?.Contains(current.gameObject) == true) ||
                        (owner && current.transform.IsChildOf(owner!.transform));
                    if (!owned && rows != null)
                        foreach (RecipeRow row in rows)
                            if (row.Element == current.gameObject) { owned = true; break; }
                }
                // Native controller transitions can reuse the popup without pointer-exit.
                // Keep our existing background rather than destroy/re-add it in the same frame.
                if (owned && _popup == popup && _background) { _current = current; _textLayout?.Refresh(); return; }
                Restore(); _current = current; _popup = popup;
                if (!owned || !popup) return;


                // Qualified native layouts: root Graphic, Bkg, Background or Image.
                _background = popup.GetComponent<Image>();
                if (!_background)
                    foreach (string name in new[] { "Bkg", "Background", "Image" })
                    {
                        Transform child = popup.transform.Find(name);
                        if (child && child.GetComponent<Image>()) { _background = child.GetComponent<Image>(); break; }
                    }
                if (!_background)
                {
                    // Generic Tooltip is a fitted root with Text and no background.
                    // Adding a root Image preserves child0 (native positioning) and layout.
                    Transform text = popup.transform.Find("Text");
                    if (!popup.GetComponent<ContentSizeFitter>() || !popup.GetComponent<HorizontalLayoutGroup>() ||
                        !text || !text.GetComponent<TMPro.TMP_Text>() || popup.GetComponent<Graphic>()) return;
                    EnableColours(popup);
                    _textLayout = new TooltipTextLayout(popup, text.GetComponent<TMPro.TMP_Text>());
                    _background = popup.AddComponent<Image>(); _added = true;
                    _background.raycastTarget = false;
                    _background.color = Color.black;
                }
                else EnableColours(popup);
                _original = _background.color;
                _written = new Color(_original.r, _original.g, _original.b, RoundedGeometry.TooltipOpacity);
                _background.color = _written;
                _rounding=UiStyle.Round(_background,RoundedGeometry.TooltipRadius,out _addedRounding);
            }
            catch (Exception error)
            {
                Restore(); _disabled = true;
                Debug.LogWarning("CraftyQoL tooltip background styling disabled: " + error.Message);
            }
        }
        private void EnableColours(GameObject popup)
        {
            foreach(TMP_Text text in popup.GetComponentsInChildren<TMP_Text>(true))
            {
                _texts.Add(new TextState{Text=text,Rich=text.richText,Override=text.overrideColorTags});
                text.richText=true;text.overrideColorTags=false;
            }
        }
        internal void Restore()
        {
            _textLayout?.Restore(); _textLayout = null;
            if(_rounding && _addedRounding){_rounding!.enabled=false;UnityEngine.Object.Destroy(_rounding);}
            _rounding=null;_addedRounding=false;
            foreach(var state in _texts)if(state.Text)
            {if(state.Text.richText)state.Text.richText=state.Rich;if(!state.Text.overrideColorTags)state.Text.overrideColorTags=state.Override;}
            _texts.Clear();
            if (_background)
            {
                if (_added) { _background.enabled = false; UnityEngine.Object.Destroy(_background); }
                else if (_background.color == _written) _background.color = _original;
            }
            _background = null; _added = false; _current = null; _popup = null;
        }
    }
}
