using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CraftyQoL.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftyQoL
{
    public sealed class StorageSorting : MonoBehaviour
    {
        internal static StorageSorting? Current;
        private static readonly HashSet<string> Supported=new HashSet<string>(StringComparer.Ordinal){"piece_chest_wood","piece_chest","piece_chest_blackmetal","piece_chest_private","piece_chest_barrel","piece_chest_grausten","piece_chest_warderobe","Cart","Karve","VikingShip","VikingShip_Ashlands"};
        private StorageBindings? _bindings;
        private readonly StorageCatalogue _catalogue=new StorageCatalogue();
        private InventoryGui? _gui;
        private Button? _button;
        private CanvasGroup? _group;
        private bool _disabled,_busy,_wasOpen,_shown;
        private int _thread,_released=-1;
        private Container? _blocked,_shownContainer;
        private uint _blockedRevision;
        private const string Instruction="Sort & Stack: Category. Up from the top container row selects this button; B returns.";
        private string _status=Instruction;
        private string _written="";
        private GameObject? _previousFocus;
        private readonly Vector3[] _corners=new Vector3[4];
        internal static bool Released=>Current && Current!._released==Time.frameCount;
        internal static FocusMode Focus=>Current && !Current!._disabled && InventoryPresentation.Open && Current._shown && Current._button && Current._button!.gameObject.activeInHierarchy && InventoryPresentation.CanInteract && EventSystem.current &&
            EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Current._button!.transform)?FocusMode.Storage:FocusMode.None;
        internal static bool BlocksGameInput=>Focus!=FocusMode.None || Released;
        internal void Initialise()
        {
            Current=this;_thread=Thread.CurrentThread.ManagedThreadId;
            try{_bindings=new StorageBindings();}
            catch(Exception error){_disabled=true;Debug.LogWarning("CraftyQoL storage disabled safely: "+error.Message);}
        }
        private sealed class Context
        {
            internal readonly Container Container;internal readonly Inventory Inventory;internal readonly ZNetView View;internal readonly ZDO Data;
            internal readonly long Owner;internal readonly uint Revision;
            internal Context(Container c,Inventory inv,ZNetView view,ZDO data){Container=c;Inventory=inv;View=view;Data=data;Owner=data.GetOwner();Revision=data.DataRevision;}
        }
        private bool TryContext(out Context? context,out string reason,bool capture=true)
        {
            context=null;reason="Storage is not ready.";
            var gui=InventoryGui.instance;var player=Player.m_localPlayer;
            if(_disabled || _bindings==null || Plugin.Instance==null || !Plugin.Instance.Enabled.Value || !gui || !player || !InventoryPresentation.CanInteract ||
                !Game.instance || !ZNet.instance || ZNet.instance.GetWorld()==null || !ZNetScene.instance || !ObjectDB.instance || player.IsDead() || player.IsTeleporting() || player.InCutscene() ||
                Menu.IsActive() || Console.IsVisible() || Minimap.IsOpen() || TextInput.IsVisible() || UnifiedPopup.IsVisible() || StoreGui.IsVisible() || Chat.instance && Chat.instance.HasFocus())return false;
            var container=_bindings.Current.GetValue(gui) as Container;
            if(!container || container!.GetType()!=typeof(Container) || container.m_autoDestroyEmpty || !container.isActiveAndEnabled || !gui.m_container.gameObject.activeInHierarchy)return false;
            var root=container.m_rootObjectOverride?container.m_rootObjectOverride.gameObject:container.gameObject;
            string id=Utils.GetPrefabName(root);if(!Supported.Contains(id)){reason="Unsupported storage container.";return false;}
            var view=_bindings.View.GetValue(container) as ZNetView;
            if(!view || !view!.IsValid() || !view.IsOwner() || !container.IsOwner() || !container.IsInUse() || !ReferenceEquals(view,root.GetComponent<ZNetView>()) ||
                (bool)_bindings.Loading.GetValue(container) || container.m_wagon && container.m_wagon.InUse()){reason="Waiting for native container ownership/use.";return false;}
            var data=view.GetZDO();if(data==null || data.GetOwner()!=ZDOMan.GetSessionID() || data.GetPrefab()!=id.GetStableHashCode())return false;
            var profile=Game.instance.GetPlayerProfile();if(profile==null || !_bindings.Access(container,profile.GetPlayerID()) ||
                container.m_checkGuardStone && !PrivateArea.CheckAccess(container.transform.position,0f,flash:false) ||
                Vector3.Distance(container.transform.position,player.transform.position)>gui.m_autoCloseDistance){reason="Container access is unavailable.";return false;}
            var inventory=container.GetInventory();
            if(inventory==null || inventory.GetType()!=typeof(Inventory) || ReferenceEquals(inventory,player.GetInventory()) || (bool)_bindings.Temporary.GetValue(inventory) ||
                (bool)_bindings.CheatedPopup.GetValue(inventory) || !_bindings.SingleNativeSave(container,inventory)){reason="Nonstandard storage inventory or change handlers.";return false;}
            if(inventory.GetAllItems().Count==0){reason="Container is empty.";return false;}
            if(inventory.GetAllItems().Count>StoragePlanner.MaxItems || inventory.GetWidth()<1 || inventory.GetWidth()>255 || inventory.GetHeight()<1 || inventory.GetHeight()>255)return false;
            if(capture)context=new Context(container,inventory,view,data);return true;
        }
        private static string Relative(Transform child,Transform root)
        {string path="";int depth=0;while(child!=root){if(!child.parent || ++depth>16)return "?";path="/"+child.name+path;child=child.parent;}return path;}
        private bool QualifiedRoot(Context context)
        {
            var c=context.Container;var root=c.m_rootObjectOverride?c.m_rootObjectOverride.gameObject:c.gameObject;
            var prefab=ZNetScene.instance.GetPrefab(Utils.GetPrefabName(root));if(!prefab)return false;
            var expected=prefab.GetComponentsInChildren<Container>(true);var actual=root.GetComponentsInChildren<Container>(true);
            return expected.Length==1 && actual.Length==1 && expected[0].GetType()==typeof(Container) && ReferenceEquals(actual[0],c) && Relative(c.transform,root.transform)==Relative(expected[0].transform,prefab.transform);
        }
        private byte[]? Saved(Context context)=>context.View && context.View.IsValid() && ReferenceEquals(context.View.GetZDO(),context.Data)?context.Data.GetByteArray(_bindings!.ItemsKey):null;
        private bool Authority(Context original)
        {
            if(!TryContext(out var now,out _ ) || now==null)return false;
            return ReferenceEquals(now.Container,original.Container) && ReferenceEquals(now.Inventory,original.Inventory) && ReferenceEquals(now.View,original.View) && ReferenceEquals(now.Data,original.Data) &&
                now.Owner==original.Owner && now.Revision==original.Revision;
        }
        private void Sort()
        {
            if(_busy || _disabled || !_shown || !_button || !_button!.interactable || Thread.CurrentThread.ManagedThreadId!=_thread)return;
            _busy=true;Context? context=null;
            try
            {
                if(!TryContext(out context,out string reason) || context==null){_status=reason;return;}
                if(context.Container!=_shownContainer){_status="Container changed; retry the button.";return;}
                string? conflict=_bindings!.Conflict();if(conflict!=null)throw new InvalidDataException(conflict);
                if(!QualifiedRoot(context))throw new InvalidDataException("Storage prefab/slot layout is not qualified.");
                byte[]? saved=Saved(context);if(saved==null || saved.Length>StorageSnapshot.MaxPacket)throw new InvalidDataException("Native storage data is unavailable.");
                byte[] original=(byte[])saved.Clone();var snapshot=new StorageSnapshot(context.Inventory,_bindings,_catalogue,original);
                var captured=context;
                bool notified=false;
                StoragePublication Publication()
                {
                    bool expected=StorageSnapshot.Equal(Saved(captured),snapshot.Expected);
                    bool receipt=captured.Container && (uint)_bindings.LastRevision.GetValue(captured.Container)==captured.Data.DataRevision;
                    bool changed=captured.Data.DataRevision!=captured.Revision;
                    return StorageReceipt.State(expected,StorageSnapshot.Equal(Saved(captured),original),changed,receipt,notified,StorageSnapshot.Equal(snapshot.Expected,original));
                }
                bool OriginalNetwork()=>StorageSnapshot.Equal(Saved(captured),original);
                var result=StorageTransaction.Execute(snapshot.Plan.Changed,
                    ()=>Authority(captured) && OriginalNetwork() && snapshot.OriginalState(),snapshot.Apply,
                    ()=>Authority(captured) && OriginalNetwork() && snapshot.AppliedState(),
                    ()=>{_bindings.Notify(captured.Inventory,false,false);notified=true;},Publication,snapshot.AppliedState,
                    ()=>captured.Container && captured.View && captured.View.IsValid() && ReferenceEquals(captured.Container.GetInventory(),captured.Inventory) && OriginalNetwork() && snapshot.JournalOnly(),
                    snapshot.Rollback,()=>OriginalNetwork() && snapshot.OriginalState());
                if(result==StorageResult.Indeterminate)
                { _disabled=true;Hide();Debug.LogError("CraftyQoL storage sorting disabled: publication/rollback could not be verified. No newer network state was overwritten; reopen and inspect the container.");return; }
                _status=result==StorageResult.Applied?"Last sort: complete (Category).":result==StorageResult.Unchanged?"Last sort: already organised.":"Sort aborted; original inventory restored or untouched.";
                if(result==StorageResult.Aborted){_blocked=context.Container;_blockedRevision=context.Data.DataRevision;}
            }
            catch(Exception error)
            {
                _status="Sort unavailable: "+error.Message;
                if(context!=null){_blocked=context.Container;_blockedRevision=context.Data.DataRevision;}
                Debug.LogWarning("CraftyQoL storage: "+error.Message);
            }
            finally{_busy=false;_released=Time.frameCount;}
        }
        private UiBox Bounds(RectTransform rect,RectTransform parent)
        {
            rect.GetWorldCorners(_corners);float left=float.MaxValue,right=float.MinValue,top=float.MaxValue,bottom=float.MinValue;
            foreach(var corner in _corners){var point=parent.InverseTransformPoint(corner);float x=point.x-parent.rect.xMin,y=parent.rect.yMax-point.y;left=Mathf.Min(left,x);right=Mathf.Max(right,x);top=Mathf.Min(top,y);bottom=Mathf.Max(bottom,y);}
            return new UiBox(left,top,right-left,bottom-top);
        }
        private bool Layout()
        {
            if(!_gui || !_button)return false;var parent=_gui!.m_container;
            if(!StorageGeometry.Place(parent.rect.width,parent.rect.height,Bounds(_gui.m_containerName.rectTransform,parent),Bounds((RectTransform)_gui.m_stackAllButton.transform,parent),
                Bounds((RectTransform)_gui.m_takeAllButton.transform,parent),Bounds(_gui.m_containerWeight.rectTransform,parent),out var box))return false;
            var rect=(RectTransform)_button!.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(box.X,-box.Y);rect.sizeDelta=new Vector2(box.Width,box.Height);return true;
        }
        private void Create(InventoryGui gui)
        {
            DisposeButton();_gui=gui;
            _button=NativeToolbar.MakeButton(gui,gui.m_container,"Sort","Sort & Stack: Category",Sort,0,0,44,22);
            foreach(EventTrigger trigger in _button.GetComponentsInChildren<EventTrigger>(true)){trigger.enabled=false;UnityEngine.Object.Destroy(trigger);}
            _group=_button.gameObject.AddComponent<CanvasGroup>();
            _button.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=_button,selectOnRight=_button,selectOnUp=_button,selectOnDown=_button};
            _button.gameObject.SetActive(false);
        }
        private void Update()
        {
            if(_disabled || _bindings==null)return;
            try
            {
                if(!InventoryPresentation.Open)
                {
                    _blocked=null;_wasOpen=false;ReturnFocus();_bindings.ClearTarget();
                    if(!Player.m_localPlayer){_catalogue.Invalidate();DisposeButton();return;}
                    if(_shown && _gui && InventoryPresentation.CanAnimateClose(_gui!) && !InventoryPresentation.FinishedClosing(_gui!)){if(_group)_group!.blocksRaycasts=false;return;}
                    Hide();return;
                }
                if(!_wasOpen){_blocked=null;_wasOpen=true;}
                var gui=InventoryGui.instance;
                var container=gui?_bindings.Current.GetValue(gui) as Container:null;
                if(!gui || !container || Plugin.Instance==null || !Plugin.Instance.Enabled.Value){Hide();return;}
                var root=container!.m_rootObjectOverride?container.m_rootObjectOverride.gameObject:container.gameObject;
                if(!Supported.Contains(Utils.GetPrefabName(root)) || !gui.m_container.gameObject.activeInHierarchy){Hide();return;}
                if(_gui!=gui || !_button)Create(gui);
                if(!Layout()){Hide();return;}
                if(_shownContainer!=container)_status=Instruction;
                _shown=true;_shownContainer=container;_button!.gameObject.SetActive(true);_group!.blocksRaycasts=true;
                bool ready=TryContext(out _,out string reason,false);
                var view=_bindings.View.GetValue(container) as ZNetView;
                bool blocked=ready && view && _blocked==container && _blockedRevision==view!.GetZDO().DataRevision;
                if(ready && _blocked==container && !blocked){_blocked=null;_status=Instruction;}
                _button.interactable=ready && !blocked && !_busy;
                string text=!ready?reason:_status;
                if(_written!=text){_button.GetComponent<UITooltip>().Set("Sort & Stack",text==Instruction?text:text+"\n"+Instruction);_written=text;}
            }
            catch(Exception error){_disabled=true;Hide();Debug.LogWarning("CraftyQoL storage UI disabled safely: "+error.Message);}
        }
        internal void InventoryInput(InventoryGui gui)
        {
            if(_disabled || _bindings==null || !_shown || !_button || !InventoryPresentation.CanInteract)return;
            bool focused=Focus!=FocusMode.None;
            if(!focused && !TryContext(out _,out _,false))return;
            if(focused && (ZInput.GetButtonDown("JoyButtonB") || ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyTabLeft") || ZInput.GetButtonDown("JoyTabRight")))
            {ReturnFocus();_released=Time.frameCount;}
            else if(!focused && ZInput.IsGamepadActive() && gui.ActiveGroup==0 && _button!.interactable && _bindings.Selection(gui.ContainerGrid).y==0 && ZInput.GetButtonDown("JoyDPadUp"))
            {
                if(EventSystem.current){_previousFocus=EventSystem.current.currentSelectedGameObject;EventSystem.current.SetSelectedGameObject(_button.gameObject);_released=Time.frameCount;}
            }
        }
        private void ReturnFocus()
        {
            if(_button && EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_button!.transform))
                EventSystem.current.SetSelectedGameObject(_previousFocus && _previousFocus!.activeInHierarchy?_previousFocus:null);
            _previousFocus=null;
        }
        private void Hide(){ReturnFocus();_shown=false;_shownContainer=null;if(_button)_button!.gameObject.SetActive(false);}
        private void DisposeButton(){Hide();if(_button)UnityEngine.Object.Destroy(_button!.gameObject);_button=null;_group=null;_gui=null;_written="";}
        internal void Dispose(){DisposeButton();if(Current==this)Current=null;}
        private void OnDisable()=>Hide();
        private void OnDestroy()=>Dispose();
    }
}
