using System;
using System.Collections.Generic;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CraftyQoL
{
    internal sealed class TrackerHudRow
    {
        internal string Id = "", Title = "", Detail = "", Quantity = "";
        internal Sprite? Icon;
        internal bool Complete, EditOnly;
        internal Action? Main, Remove;
    }
    internal sealed class TrackerHud : IDisposable
    {
        private const float RowHeight = TrackerHudGeometry.RowHeight;
        private readonly GameObject _root;
        private readonly RectTransform _rect, _parent, _viewport, _content;
        private readonly CanvasGroup _group;
        private readonly Canvas _canvas, _inventoryCanvas, _hudCanvas;
        private readonly GraphicRaycaster _raycaster;
        private readonly ScrollRect _scroll;
        private readonly TrackerStore _store;
        private readonly RecipeTracker _controller;
        private readonly Button _materials, _recipes, _minimise, _hide, _previous, _next, _settings, _keepVisible;
        private readonly TMP_Text _title, _status;
        private readonly TooltipBackground _tooltipBackground = new TooltipBackground();
        private readonly List<TrackerHudRow> _rows = new List<TrackerHudRow>();
        private readonly List<PoolRow> _pool = new List<PoolRow>();
        private Vector2 _size;
        private float _width = 280, _height = 300;
        private bool _scrolling, _warning, _settingsOpen;
        private readonly PinGraphic _pinIcon;
        private bool _binding, _dragging, _hasState, _lastShow, _lastEdit, _lastMin, _lastRecipes;
        private Vector2 _dragOffset;
        private readonly List<TrackerHudRow> _allRows=new List<TrackerHudRow>();
        private int _first = -1;
        internal bool Dragging => _dragging;
        internal bool Alive => _root && _parent && _inventoryCanvas && _hudCanvas;
        internal bool CanFit => Alive && new TrackerLayout(_parent.rect.width,_parent.rect.height).CanFit;
        internal GameObject FocusTarget => _materials.gameObject;
        private sealed class PoolRow
        {
            internal readonly GameObject Root;
            internal readonly TMP_Text Title, Detail;
            internal readonly Image Icon;
            internal readonly UITooltip Tip;
            internal readonly Button Main, Remove;
            internal string Id = "";
            internal TrackerHudRow? Data;
            internal PoolRow(TrackerHud owner, InventoryGui gui)
            {
                Root = new GameObject("CraftyQoL.TrackerRow",typeof(RectTransform)); Root.transform.SetParent(owner._content,false);
                Title=owner.Text(Root.transform,12); Detail=owner.Text(Root.transform,11);Detail.alignment=TextAlignmentOptions.MidlineRight;
                Detail.enableAutoSizing=true;Detail.fontSizeMin=8;Detail.fontSizeMax=11;Icon=owner.Image(Root.transform);
                Tip=UiStyle.Tooltip(gui,Title.gameObject);Title.raycastTarget=true;
                Main=MakeButton(gui,Root.transform,"","",()=>Data?.Main?.Invoke(),28,1,210,20);
                Remove=MakeButton(gui,Root.transform,"X","Remove pin",()=>Data?.Remove?.Invoke(),280,1,18,20);
                Root.SetActive(false);
            }
        }
        internal TrackerHud(InventoryGui gui, Hud hud, TrackerStore store, RecipeTracker controller)
        {
            _store=store;_controller=controller;
            _parent=hud.m_rootObject.transform as RectTransform ?? throw new NotSupportedException("HUD root is not qualified.");
            _root=new GameObject("CraftyQoL.RecipeTracker",typeof(RectTransform),typeof(Image),typeof(CanvasGroup),typeof(Canvas),typeof(GraphicRaycaster));
            _root.SetActive(false);_rect=(RectTransform)_root.transform;_rect.SetParent(_parent,false);
            _rect.anchorMin=_rect.anchorMax=_rect.pivot=new Vector2(0,1);
            _group=_root.GetComponent<CanvasGroup>();
            _group.ignoreParentGroups=true;
            _raycaster=_root.GetComponent<GraphicRaycaster>();_raycaster.enabled=false;
            try
            {
                _hudCanvas=hud.GetComponentInParent<Canvas>() ?? throw new NotSupportedException("Native HUD canvas missing.");
                _inventoryCanvas=gui.GetComponentInParent<Canvas>() ?? throw new NotSupportedException("Native inventory canvas missing.");
                _canvas=_root.GetComponent<Canvas>();_canvas.overrideSorting=true;
                _canvas.additionalShaderChannels=_hudCanvas.additionalShaderChannels | _inventoryCanvas.additionalShaderChannels;
                Image native=gui.m_crafting.Find("Bkg").GetComponent<Image>();
                Image background=_root.GetComponent<Image>();background.color=new Color(0.045f,0.035f,0.025f,0.96f);background.raycastTarget=false;
                // Share the native LitGui material read-only: its shader follows game lighting/settings.
                // Keep the solid backing and unlit text/icons for predictable contrast.
                var frame=Image(_rect);frame.sprite=native.sprite;frame.type=native.type;frame.material=native.material;frame.color=native.color;
                var frameRect=(RectTransform)frame.transform;frameRect.anchorMin=Vector2.zero;frameRect.anchorMax=Vector2.one;frameRect.offsetMin=frameRect.offsetMax=Vector2.zero;
                UiStyle.NativeBorder(frame);
                _title=Text(_rect,12);_title.text="Recipe tracker";
                var handle=new GameObject("CraftyQoL.TrackerDrag",typeof(RectTransform),typeof(Image),typeof(TrackerDrag));
                handle.transform.SetParent(_rect,false);handle.GetComponent<Image>().color=Color.clear;handle.GetComponent<TrackerDrag>().Owner=this;
                Place((RectTransform)handle.transform,6,3,266,28);
                _minimise=MakeButton(gui,_rect,"-","Minimise/expand tracker",()=>{_store.State.Minimised=!_store.State.Minimised;_controller.Changed();},284,5,26,22);
                _minimise.gameObject.SetActive(true);
                _materials=MakeButton(gui,_rect,"Materials","Aggregate direct inputs",()=>{_store.State.RecipesTab=false;_controller.Changed();},8,34,148,24);
                _recipes=MakeButton(gui,_rect,"Recipes","Individual pinned recipes",()=>{_store.State.RecipesTab=true;_controller.Changed();},164,34,148,24);
                _hide=MakeButton(gui,_rect,"Hide completed","Toggle completed material rows",()=>{_store.State.HideCompleted=!_store.State.HideCompleted;_controller.Changed();},8,63,174,22);
                _status=Text(_rect,10);
                var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewport.transform.SetParent(_rect,false);
                _viewport=(RectTransform)viewport.transform;viewport.GetComponent<Image>().color=Color.clear;
                var content=new GameObject("Content",typeof(RectTransform));_content=(RectTransform)content.transform;_content.SetParent(_viewport,false);
                _scroll=_root.AddComponent<ScrollRect>();_scroll.viewport=_viewport;_scroll.content=_content;_scroll.horizontal=false;
                _scroll.movementType=ScrollRect.MovementType.Clamped;_scroll.scrollSensitivity=32;_scroll.inertia=false;
                _scroll.onValueChanged.AddListener(_=>BindVisible(false));
                _previous=MakeButton(gui,_rect,"Prev","Scroll up",()=>Page(-1),8,268,60,24);
                _next=MakeButton(gui,_rect,"Next","Scroll down",()=>Page(1),74,268,60,24);
                _settings=MakeButton(gui,_rect,"Settings","Tracker settings and dragging (inventory open)",ToggleSettings,0,0,62,20);
                _keepVisible=MakeButton(gui,_rect,"","Keep tracker visible when inventory is closed",controller.ToggleKeepVisible,0,0,22,22);
                _keepVisible.GetComponentInChildren<TMP_Text>(true).gameObject.SetActive(false);
                var pin=new GameObject("CraftyQoL.KeepVisible",typeof(RectTransform),typeof(PinGraphic));
                EquipmentCategoryIcons.Center((RectTransform)pin.transform,_keepVisible.transform,16);
                _pinIcon=pin.GetComponent<PinGraphic>();_pinIcon.raycastTarget=false;RefreshPin();
                for(int i=0;i<12;i++)_pool.Add(new PoolRow(this,gui));
                Layout();
            }
            catch { UnityEngine.Object.Destroy(_root); throw; }
        }
        private static Button MakeButton(InventoryGui gui,Transform parent,string label,string tooltip,Action action,float x,float y,float width,float height)
        {
            Button button=NativeToolbar.MakeButton(gui,parent,label,tooltip,()=>{if(RecipeTracker.Editing)action();},x,y,width,height);
            foreach(EventTrigger trigger in button.GetComponentsInChildren<EventTrigger>(true))
            { trigger.enabled=false;UnityEngine.Object.Destroy(trigger); }
            button.gameObject.SetActive(true);return button;
        }
        private TMP_Text Text(Transform parent,float size)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
            var t=go.GetComponent<TextMeshProUGUI>();t.font=InventoryGui.instance.m_recipeName.font;t.fontSize=size;t.richText=false;
            t.alignment=TextAlignmentOptions.MidlineLeft;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;
            return t;
        }
        private Image Image(Transform parent)
        {var go=new GameObject("Icon",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.preserveAspect=true;image.raycastTarget=false;return image;}
        private static void Place(RectTransform r,float x,float y,float w,float h)
        {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        private static void Box(Component c,float x,float y,float w,float h)=>Place((RectTransform)c.transform,x,y,w,h);
        private void SelectRows()
        { _rows.Clear();foreach(var row in _allRows)if(!row.EditOnly || _lastEdit)_rows.Add(row); }
        private void Layout()
        {
            _size=_parent.rect.size;
            var geometry=new TrackerHudGeometry(_size.x,_size.y,_rows.Count,_lastEdit,_settingsOpen,_store.State.Minimised,_warning);
            var layout=geometry.Frame;_width=layout.Width;_height=layout.Height;_scrolling=geometry.Scrolling;
            _rect.sizeDelta=new Vector2(_width,_height);
            // Position in the original HUD coordinate system, even while hosted by inventory.
            _rect.position=_parent.TransformPoint(new Vector3(_parent.rect.xMin+layout.Left(_store.State.X),_parent.rect.yMax-layout.Top(_store.State.Y),0));
            Box(_title,8,2,_width-(_lastEdit?108:16),22);
            Box(_minimise,_width-96,3,22,20);Box(_settings,_width-70,3,62,20);
            Place((RectTransform)_rect.Find("CraftyQoL.TrackerDrag"),4,1,_width-(_lastEdit?100:8),24);
            float half=(_width-20)/2;Box(_materials,6,28,half,20);Box(_recipes,14+half,28,half,20);
            Box(_keepVisible,6,52,22,22);Box(_hide,34,52,Mathf.Min(132,_width-40),20);
            Box(_status,6,geometry.ViewportY-16,_width-12,14);
            Place(_viewport,6,geometry.ViewportY,_width-12,geometry.ViewportHeight);
            Place(_content,0,0,_width-12,Mathf.Max(geometry.ViewportHeight,_rows.Count*RowHeight));
            Box(_previous,6,_height-25,46,20);Box(_next,56,_height-25,46,20);
        }
        internal bool Contains(GameObject? go)=>go && _root && go.transform.IsChildOf(_root.transform);
        internal void BeginEdit()
        {if(EventSystem.current)EventSystem.current.SetSelectedGameObject(_store.State.Minimised?_minimise.gameObject:FocusTarget);}
        private bool SetSurface(bool interactive)
        {
            // Share inventory's native raycast hierarchy while open; retain HUD coordinates and scale.
            Transform host=interactive?_inventoryCanvas.transform:_parent;
            bool changed=_rect.parent!=host;
            if(changed)_rect.SetParent(host,true);
            Vector3 source=_parent.lossyScale,target=host.lossyScale;
            var scale=new Vector3(source.x/target.x,source.y/target.y,source.z/target.z);
            if(_rect.localScale!=scale){_rect.localScale=scale;changed=true;}
            if(_rect.rotation!=_parent.rotation){_rect.rotation=_parent.rotation;changed=true;}
            var sorting=interactive?_inventoryCanvas:_hudCanvas;
            int order=sorting.sortingOrder+1;
            if(_canvas.sortingLayerID!=sorting.sortingLayerID){_canvas.sortingLayerID=sorting.sortingLayerID;changed=true;}
            if(_canvas.sortingOrder!=order){_canvas.sortingOrder=order;changed=true;}
            if(_raycaster.enabled!=interactive)_raycaster.enabled=interactive;
            return changed;
        }
        internal void Show(bool show,bool editing)
        {
            if(!Alive)return;
            bool surfaceChanged=SetSurface(show && editing);
            bool changed=surfaceChanged || !_hasState || _size!=_parent.rect.size || _lastShow!=show || _lastEdit!=editing || _lastMin!=_store.State.Minimised || _lastRecipes!=_store.State.RecipesTab;
            if(!changed)return;
            if(!editing)_settingsOpen=false;
            _hasState=true;_lastShow=show;_lastEdit=editing;_lastMin=_store.State.Minimised;_lastRecipes=_store.State.RecipesTab;
            float oldScroll=_scroll.verticalNormalizedPosition;SelectRows();Layout();_scroll.verticalNormalizedPosition=oldScroll;
            _root.SetActive(show && CanFit);_group.interactable=_group.blocksRaycasts=editing;
            _root.GetComponent<Image>().raycastTarget=editing;
            if(!show || !editing)_tooltipBackground.Restore();
            bool expanded=!_store.State.Minimised;
            _materials.gameObject.SetActive(expanded);_recipes.gameObject.SetActive(expanded);
            _viewport.gameObject.SetActive(expanded);_status.gameObject.SetActive(expanded && _warning);
            _hide.gameObject.SetActive(expanded && editing && _settingsOpen && !_store.State.RecipesTab);
            _keepVisible.gameObject.SetActive(expanded && editing && _settingsOpen);_minimise.gameObject.SetActive(editing);_settings.gameObject.SetActive(editing);
            _rect.Find("CraftyQoL.TrackerDrag").gameObject.SetActive(editing && _settingsOpen);
            _previous.gameObject.SetActive(expanded && editing && _scrolling);_next.gameObject.SetActive(expanded && editing && _scrolling);
            _title.text=editing && _settingsOpen?"Tracker - drag":"Tracker";
            _settings.GetComponentInChildren<TMP_Text>(true).text=_settingsOpen?"Done":"Settings";
            _minimise.GetComponentInChildren<TMP_Text>(true).text=expanded?"-":"+";
            BindVisible(true);WireNavigation();
        }
        private void ToggleSettings()
        {
            if(!_lastEdit)return;
            _settingsOpen=!_settingsOpen;
            if(_settingsOpen && _store.State.Minimised){_store.State.Minimised=false;_controller.Changed();}
            EndDrag();_hasState=false;Show(_lastShow,_lastEdit);
        }
        internal bool CloseSettings()
        {
            if(!_settingsOpen)return false;
            ToggleSettings();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(_settings.gameObject);
            return true;
        }
        internal void RefreshPin()
        {
            bool pinned=_store.State.KeepVisible;
            _pinIcon.color=UiStyle.StarColour(pinned);
            UiStyle.Selected(_keepVisible,pinned,Color.white);
            _keepVisible.GetComponent<UITooltip>().Set("",pinned?"Unpin tracker: show only while inventory is open":"Pin tracker: also show during gameplay");
        }
        internal void Render(List<TrackerNote> notes,List<TrackerMaterial> materials,bool saveFailed)
        {
            _allRows.Clear();
            UiStyle.Selected(_materials,!_store.State.RecipesTab,Color.white);UiStyle.Selected(_recipes,_store.State.RecipesTab,Color.white);
            _hide.GetComponentInChildren<TMP_Text>(true).text=_store.State.HideCompleted?"Show completed":"Hide completed";
            UiStyle.Selected(_hide,_store.State.HideCompleted,Color.white);
            int unavailable=0;
            foreach(var note in notes)if(note.Plan==null)unavailable++;
            _warning=saveFailed || unavailable>0;
            _status.text=saveFailed?"Not saved - changes are in memory":unavailable>0?unavailable+" unavailable pin(s) - Recipes to remove":"";
            if(_store.State.RecipesTab)
            {
                foreach(TrackerNote note in notes)
                {
                    string key=note.Pin.Key;
                    var header=new TrackerHudRow{Id=key,Title=note.Plan==null?"Unavailable pin":(note.Pin.Expanded?"[-] ":"[+] ")+note.Plan.Name,
                        Detail=note.Plan==null?"Missing, changed or not yet known":"1 craft -> "+note.Plan.OutputAmount+" item(s)",Icon=note.Icon,
                        Remove=()=>_controller.Remove(key)};
                    if(note.Plan!=null)header.Main=()=>{note.Pin.Expanded=!note.Pin.Expanded;_controller.Changed();};
                    _allRows.Add(header);
                    if(note.Plan==null || !note.Pin.Expanded)continue;
                    if(note.Options.Count>1)_allRows.Add(new TrackerHudRow{Id=key+"/choice",Title="Ingredient / quality",Detail="Planning only; crafting chooses native input",EditOnly=true,Main=()=>_controller.Alternative(key)});
                    int n=0;
                    foreach(TrackerNeed need in note.Plan.Needs)
                    {
                        long owned=_controller.Owned(need.Id,need.Quality);
                        _allRows.Add(new TrackerHudRow{Id=key+"/"+n++,Title=need.Name+(need.Quality>0?" (quality "+need.Quality+")":""),Icon=_controller.Icon(need.Id),
                            Quantity=TrackerText.Quantity(owned,need.Amount),Detail="One recipe craft; deficit "+Math.Max(0,need.Amount-owned),Complete=owned>=need.Amount});
                    }
                }
            }
            else foreach(var m in materials)
            {
                if(_store.State.HideCompleted && m.Complete)continue;
                _allRows.Add(new TrackerHudRow{Id=m.Id,Title=m.Name,Icon=_controller.Icon(m.Id),
                    Quantity=TrackerText.Quantity(m.Owned,m.Required),Detail="Combined direct inputs; deficit "+m.Deficit+" (quality constraints apply)",Complete=m.Complete});
            }
            if(_allRows.Count==0)_allRows.Add(new TrackerHudRow{Id="empty",Title="No materials to show",Detail=unavailable>0?"Unavailable pins contribute no requirements":"All shown materials complete or no inputs"});
            float old=_scroll.verticalNormalizedPosition;SelectRows();Layout();_scroll.verticalNormalizedPosition=old;_hasState=false;BindVisible(true);
        }
        private void Page(int direction)
        {
            float span=Mathf.Max(0,_content.rect.height-_viewport.rect.height);
            _scroll.verticalNormalizedPosition=span<=0?1:Mathf.Clamp01(_scroll.verticalNormalizedPosition-direction*_viewport.rect.height/span);
            BindVisible(false);
        }
        private void BindVisible(bool force)
        {
            if(_binding || _pool.Count==0)return;
            int first=Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0,_content.anchoredPosition.y)/RowHeight),0,Mathf.Max(0,_rows.Count-1));
            if(!force && first==_first)return;
            _binding=true;_first=first;
            try
            {
                int visible=Mathf.Min(_pool.Count,Mathf.CeilToInt(_viewport.rect.height/RowHeight)+1);
                for(int i=0;i<_pool.Count;i++)
                {
                    var pool=_pool[i];int index=first+i;bool active=i<visible && index<_rows.Count;
                    if(EventSystem.current && Contains(EventSystem.current.currentSelectedGameObject) &&
                        EventSystem.current.currentSelectedGameObject.transform.IsChildOf(pool.Root.transform) && (!active || pool.Id!=_rows[index].Id))
                        EventSystem.current.SetSelectedGameObject(FocusTarget);
                    pool.Root.SetActive(active);if(!active)continue;
                    var row=_rows[index];pool.Data=row;pool.Id=row.Id;Place((RectTransform)pool.Root.transform,0,index*RowHeight,_width-12,RowHeight);
                    Box(pool.Icon,0,2,20,20);pool.Icon.sprite=row.Icon;pool.Icon.enabled=row.Icon;
                    float management=_lastEdit && row.Remove!=null?22:0;
                    float quantityWidth=row.Quantity.Length==0?0:Mathf.Min((_width-40)*0.45f,Mathf.Max(48,pool.Detail.GetPreferredValues(row.Quantity).x+4));
                    float textWidth=_width-40-management-quantityWidth;
                    Box(pool.Title,24,0,textWidth,RowHeight);Box(pool.Detail,_width-12-quantityWidth,0,quantityWidth,RowHeight);
                    Color colour=row.Complete?new Color(0.68f,0.8f,0.61f):new Color(0.96f,0.93f,0.86f);
                    pool.Title.text=row.Title;pool.Title.color=colour;pool.Detail.text=row.Quantity;pool.Detail.color=colour;
                    pool.Tip.Set("",row.Title+(row.Quantity.Length>0?"  "+row.Quantity:"")+"\n"+row.Detail);
                    bool action=_lastEdit && row.Main!=null;
                    pool.Main.gameObject.SetActive(action);pool.Title.gameObject.SetActive(!action);
                    Box(pool.Main,24,1,textWidth,22);
                    var mainText=pool.Main.GetComponentInChildren<TMP_Text>(true);mainText.text=row.Title;mainText.richText=false;
                    mainText.textWrappingMode=TextWrappingModes.NoWrap;mainText.overflowMode=TextOverflowModes.Ellipsis;
                    pool.Main.GetComponent<UITooltip>().Set("",row.Title+"\n"+row.Detail);
                    Set(pool.Remove,_lastEdit?row.Remove:null,_width-34);
                }
                WireNavigation();
            }
            finally{_binding=false;}
        }
        private static void Set(Button button,Action? action,float x){Box(button,x,0,18,20);button.gameObject.SetActive(action!=null);}
        private void WireNavigation()
        {
            if(!_lastEdit)return;
            var controls=new List<Selectable>();
            void Add(Selectable button){if(button.gameObject.activeSelf && button.interactable)controls.Add(button);}
            Add(_materials);Add(_recipes);Add(_settings);Add(_minimise);Add(_keepVisible);Add(_hide);
            if(!_store.State.Minimised)
                foreach(var row in _pool)if(row.Root.activeSelf)
                    foreach(var button in new[]{row.Main,row.Remove})Add(button);
            Add(_previous);Add(_next);
            if(controls.Count==0)return;
            for(int i=0;i<controls.Count;i++)controls[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnLeft=controls[(i+controls.Count-1)%controls.Count],selectOnUp=controls[(i+controls.Count-1)%controls.Count],
                selectOnRight=controls[(i+1)%controls.Count],selectOnDown=controls[(i+1)%controls.Count]};
            if(EventSystem.current && Contains(EventSystem.current.currentSelectedGameObject) && !EventSystem.current.currentSelectedGameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_store.State.Minimised?_minimise.gameObject:FocusTarget);
        }
        internal void BeginDrag(PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left || !_settingsOpen || !RecipeTracker.Editing || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent,e.position,e.pressEventCamera,out Vector2 point))return;
            Vector3 origin=_parent.InverseTransformPoint(_rect.position);
            _dragging=true;_dragOffset=new Vector2(point.x-origin.x,point.y-origin.y);
        }
        internal void Drag(PointerEventData e)
        {
            if(!_dragging || !RecipeTracker.Editing || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent,e.position,e.pressEventCamera,out Vector2 point))return;
            Vector2 position=new Vector2(point.x-_parent.rect.xMin,point.y-_parent.rect.yMax)-_dragOffset;
            var layout=new TrackerLayout(_size.x,_size.y,_height);
            _store.State.X=layout.NormalX(position.x);_store.State.Y=layout.NormalY(-position.y);Layout();
        }
        internal void EndDrag(){if(!_dragging)return;_dragging=false;_controller.Changed();}
        internal void StyleTooltip(bool active)
        {if(active)_tooltipBackground.Update(null,null,null,_root);else _tooltipBackground.Restore();}
        public void Dispose(){_tooltipBackground.Restore();if(_root){_root.SetActive(false);UnityEngine.Object.Destroy(_root);}}
    }
    public sealed class TrackerDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        internal TrackerHud Owner=null!;
        public void OnBeginDrag(PointerEventData e)=>Owner.BeginDrag(e);
        public void OnDrag(PointerEventData e)=>Owner.Drag(e);
        public void OnEndDrag(PointerEventData e)=>Owner.EndDrag();
    }
}
