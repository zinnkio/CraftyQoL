using System;
using System.Collections.Generic;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftyQoL
{
    // Cache only our controls. Native row GameObjects are always constructed/destroyed by Valheim.
    internal sealed class RowWidgetPool : IDisposable
    {
        private const int RetainedLimit=512;
        private readonly GameObject _parking;
        private readonly GameObject _template;
        private readonly Stack<RowWidgets> _available=new Stack<RowWidgets>();
        private bool _disposed;
        internal RowWidgetPool(InventoryGui gui)
        {
            _parking=new GameObject("CraftyQoL.RowCache",typeof(RectTransform));
            _parking.SetActive(false);
            _parking.transform.SetParent(gui.transform,false);
            try
            {
                Image native=gui.m_tabCraft.GetComponent<Image>();
                if(!native || !native.sprite)throw new NotSupportedException("Native row-button image unavailable.");
                _template=new GameObject("CraftyQoL.RowWidgets",typeof(RectTransform));
                _template.SetActive(false);_template.transform.SetParent(_parking.transform,false);
                var root=(RectTransform)_template.transform;
                root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
                var go=new GameObject("CraftyQoL.Favourite",typeof(RectTransform),typeof(Image),typeof(RowFavouriteButton));
                go.transform.SetParent(root,false);
                var rect=(RectTransform)go.transform;
                rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0.5f);
                rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(ToolbarGeometry.SmallButton,ToolbarGeometry.SmallButton);
                Image image=go.GetComponent<Image>();
                image.sprite=native.sprite;image.color=native.color;image.material=native.material;image.maskable=native.maskable;
                image.type=native.type;image.preserveAspect=native.preserveAspect;image.pixelsPerUnitMultiplier=native.pixelsPerUnitMultiplier;
                UiStyle.NativeBorder(image);UiStyle.Round(image);
                var button=go.GetComponent<RowFavouriteButton>();
                button.targetGraphic=image;button.transition=Selectable.Transition.ColorTint;button.colors=UiStyle.Colours(UiStyle.Surface);
                button.navigation=new Navigation{mode=Navigation.Mode.None};button.interactable=false;
                var tip=UiStyle.Tooltip(gui,go);tip.m_topic="";tip.m_text="";tip.m_gamepadFocusObject=null;
                UiStyle.AddStar(button);UiStyle.AddSelectionMarker(button);
            }
            catch{UnityEngine.Object.Destroy(_parking);throw;}
        }
        internal RowWidgets Rent()
        {
            if(_disposed || !_parking || !_template)throw new ObjectDisposedException(nameof(RowWidgetPool));
            while(_available.Count>0)
            {
                var item=_available.Pop();
                if(item.Alive){InventoryOpeningProfiler.Note(OpeningCapture.Event.RowWidgetReused);return item;}
                item.Destroy();
            }
            InventoryOpeningProfiler.Note(OpeningCapture.Event.RowWidgetCreated);
            return new RowWidgets(_template,_parking.transform);
        }
        internal void Return(RowWidgets item)
        {
            item.Release();
            if(!_disposed && _parking && item.Alive && _available.Count<RetainedLimit)
            {item.Root.transform.SetParent(_parking.transform,false);_available.Push(item);}
            else{InventoryOpeningProfiler.Note(OpeningCapture.Event.RowWidgetDiscarded);item.Destroy();}
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            foreach(var item in _available)item.Destroy();
            _available.Clear();
            if(_parking)UnityEngine.Object.Destroy(_parking);
        }
    }

    internal sealed class RowWidgets
    {
        internal readonly GameObject Root;
        internal readonly RowFavouriteButton Favourite;
        private readonly UITooltip _tip;
        private readonly StarGraphic _star;
        private readonly RowClickBinding _click=new RowClickBinding();
        private readonly TMP_Text?[] _badges=new TMP_Text?[3];
        internal bool Alive=>Root && Favourite && _star;
        internal RowWidgets(GameObject template,Transform parking)
        {
            Root=UnityEngine.Object.Instantiate(template,parking,false);
            try
            {
                Favourite=Root.GetComponentInChildren<RowFavouriteButton>(true);
                _tip=Favourite.GetComponent<UITooltip>();_star=Root.GetComponentInChildren<StarGraphic>(true);
                if(!Favourite || !_tip || !_star)throw new NotSupportedException("Owned row template incomplete.");
                Favourite.Initialise(_click);
            }
            catch{UnityEngine.Object.Destroy(Root);throw;}
        }
        internal void Bind(Transform row,TMP_FontAsset font,FoodFlags flags,Action toggle,bool persistable)
        {
            Root.transform.SetParent(row,false);
            Favourite.navigation=new Navigation{mode=Navigation.Mode.None};Favourite.interactable=persistable;
            _click.Bind(persistable?toggle:null);
            int count=((flags&FoodFlags.Health)!=0?1:0)+((flags&FoodFlags.Stamina)!=0?1:0)+((flags&FoodFlags.Eitr)!=0?1:0),at=0;
            for(int i=0;i<3;i++)
            {
                bool show=(flags&(FoodFlags)(1<<i))!=0;
                var text=_badges[i];
                if(show && !text)
                {
                    var go=new GameObject("CraftyQoL."+(i==0?"H":i==1?"S":"E"),typeof(RectTransform),typeof(TextMeshProUGUI));
                    go.transform.SetParent(Root.transform,false);text=go.GetComponent<TextMeshProUGUI>();_badges[i]=text;
                    text.text=i==0?"H":i==1?"S":"E";text.fontSize=11;text.color=UiStyle.FoodColour(i+1);
                    text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                    var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,0.5f);rect.sizeDelta=new Vector2(12,11);
                }
                if(text)
                {
                    text!.gameObject.SetActive(show);
                    if(show){text.font=font;text.rectTransform.anchoredPosition=new Vector2(29,(count-1)*5.5f-at++*11);}
                }
            }
        }
        internal void SetFavourite(bool favourite,bool persistable,bool saveFailed)
        {
            _star.SetFilled(favourite);_star.color=UiStyle.StarColour(favourite);
            string message=!persistable?"Recipe identity is not unique; favourite unavailable":
                (favourite?"Remove favourite":"Add favourite")+(saveFailed?"\nChanges are in memory; saving failed":"");
            _tip.Set("",message);
        }
        internal void Show()=>Root.SetActive(true);
        internal void Release()
        {
            _click.Clear();
            if(Favourite){Favourite.interactable=false;Favourite.navigation=new Navigation{mode=Navigation.Mode.None};}
            if(Root)Root.SetActive(false); // Native UITooltip.OnDisable hides a popup belonging to this control.
            if(_tip){_tip.m_text="";_tip.m_topic="";_tip.m_gamepadFocusObject=null;}
        }
        internal void Destroy(){Release();if(Root)UnityEngine.Object.Destroy(Root);}
    }
}
