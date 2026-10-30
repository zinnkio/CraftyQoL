using System;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CraftyQoL
{
    internal sealed class TrackerPinControl : IDisposable
    {
        internal readonly Button Button;
        private readonly InventoryGui _gui;
        private readonly RectTransform _craft;
        private readonly RectTransform _buttonRect;
        private readonly Vector2 _originalOffset, _writtenOffset;
        private readonly PinGraphic _icon;
        private bool _layoutApplied;
        private readonly GameObject _root;
        private bool _disabled;
        private Recipe? _recipe;
        private string _origin = "";
        private int _revision = -1;
        internal TrackerPinControl(InventoryGui gui)
        {
            _gui = gui;
            _craft=gui.m_craftButton ? gui.m_craftButton.transform as RectTransform ?? throw new NotSupportedException("Craft button is not a RectTransform.") : throw new NotSupportedException("Craft button missing.");
            if(!(_craft.parent is RectTransform panel) || !TrackerPinGeometry.Fits(_craft.rect.width))
                throw new NotSupportedException("Craft button panel cannot fit a tracker pin.");
            _originalOffset=_craft.offsetMax;_writtenOffset=_originalOffset-new Vector2(TrackerPinGeometry.Reserve,0);
            _root = new GameObject("CraftyQoL.TrackerPin",typeof(RectTransform));
            var root=(RectTransform)_root.transform;root.SetParent(panel,false);
            root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            try
            {
                Button = NativeToolbar.MakeButton(gui, root, "", "Pin recipe to tracker (independent of favourites)", Toggle, 0,0,TrackerPinGeometry.Size,_craft.rect.height);
                foreach(EventTrigger trigger in Button.GetComponentsInChildren<EventTrigger>(true))
                { trigger.enabled=false;UnityEngine.Object.Destroy(trigger); }
                var buttonRect=(RectTransform)Button.transform;
                _buttonRect=buttonRect;
                buttonRect.anchorMin=buttonRect.anchorMax=buttonRect.pivot=new Vector2(1,0.5f);
                buttonRect.anchoredPosition=new Vector2(-TrackerPinGeometry.Gap,0);
                MatchCraftHeight();
                Button.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
                var graphic=new GameObject("CraftyQoL.PinIcon",typeof(RectTransform),typeof(PinGraphic));
                var iconRect=(RectTransform)graphic.transform;iconRect.SetParent(Button.transform,false);
                iconRect.anchorMin=iconRect.anchorMax=iconRect.pivot=new Vector2(0.5f,0.5f);iconRect.sizeDelta=new Vector2(22,22);
                _icon=graphic.GetComponent<PinGraphic>();_icon.raycastTarget=false;
                Button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = gui.m_craftButton, selectOnLeft = gui.m_craftButton, selectOnRight = gui.m_craftButton, selectOnUp = gui.m_craftButton };
                Button.gameObject.SetActive(false);
            }
            catch { UnityEngine.Object.Destroy(_root); throw; }
        }
        internal void Refresh()
        {
            if(_disabled)return;
            try { RefreshCore(); }
            catch(Exception error) { _disabled=true;Hide();Debug.LogWarning("CraftyQoL detail pin disabled: "+error.Message); }
        }
        internal bool Contains(GameObject? selected) => selected && Button && selected.transform.IsChildOf(Button.transform);
        private void RefreshCore()
        {
            var recipe = (Recipe)GameContracts.PairRecipe.GetValue(GameContracts.SelectedPair.GetValue(_gui),null);
            RecipeTracker? tracker = RecipeTracker.Current;
            bool show = _gui.InCraftTab() && recipe && tracker && tracker!.Ready && _gui.m_craftButton.gameObject.activeInHierarchy;
            if(!show){Hide();return;}
            ApplyLayout();
            Button.gameObject.SetActive(true);
            if (_recipe == recipe && _revision == tracker!.Revision) return;
            string? station = StationScope.Identify(_gui);
            if (station == null) { Hide(); return; }
            _recipe = recipe; _origin = station; _revision = tracker!.Revision;
            var pin = tracker.Candidate(recipe!, _origin);
            Button.interactable = pin != null;
            bool pinned = pin != null && tracker.Contains(pin.Key);
            Button.GetComponent<UITooltip>().Set("",pinned?"Unpin recipe from tracker":"Pin one craft to tracker (independent of favourites)");
            _icon.color=!Button.interactable?UiStyle.Neutral:UiStyle.StarColour(pinned);
            UiStyle.Selected(Button, pinned, Color.white);
        }
        internal void Toggle()
        {
            if (!InventoryPresentation.Open) return;
            try
            {
                Refresh();
                if (_disabled || !_recipe || !Button.interactable || StationScope.Identify(_gui) != _origin) return;
                RecipeTracker.Current?.Toggle(_recipe!, _origin); _revision = -1; Refresh();
            }
            catch(Exception error) { _disabled=true;Hide();Debug.LogWarning("CraftyQoL pin action disabled: "+error.Message); }
        }
        internal void SetInteraction(bool active)
        {
            if (!_root) return;
            var group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = active;
        }
        private void ApplyLayout()
        {
            if(_layoutApplied)
            {if(_craft.offsetMax!=_writtenOffset)throw new NotSupportedException("Craft button layout changed externally.");return;}
            if(_craft.offsetMax!=_originalOffset || !TrackerPinGeometry.Fits(_craft.rect.width))throw new NotSupportedException("Craft button layout no longer qualifies.");
            MatchCraftHeight();
            _craft.offsetMax=_writtenOffset;_layoutApplied=true;
        }
        private void MatchCraftHeight()
        {
            // The owned root fills the Craft button's parent, so matching its vertical
            // anchors/offsets aligns both edges without moving or resizing the native button.
            _buttonRect.anchorMin=new Vector2(1,_craft.anchorMin.y);
            _buttonRect.anchorMax=new Vector2(1,_craft.anchorMax.y);
            _buttonRect.pivot=new Vector2(1,_craft.pivot.y);
            var min=_buttonRect.offsetMin;var max=_buttonRect.offsetMax;
            min.y=_craft.offsetMin.y;max.y=_craft.offsetMax.y;
            _buttonRect.offsetMin=min;_buttonRect.offsetMax=max;
        }
        private void RestoreLayout()
        {if(_layoutApplied && _craft && _craft.offsetMax==_writtenOffset)_craft.offsetMax=_originalOffset;_layoutApplied=false;}
        internal void Hide()
        {
            RestoreLayout();
            if (EventSystem.current && Contains(EventSystem.current.currentSelectedGameObject)) EventSystem.current.SetSelectedGameObject(null);
            if (Button) Button.gameObject.SetActive(false); _recipe = null; _revision = -1;
        }
        public void Dispose() { Hide(); if (_root) UnityEngine.Object.Destroy(_root); }
    }
}
