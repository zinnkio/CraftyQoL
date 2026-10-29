using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CraftyQoL.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftyQoL
{
    public sealed class RecipeTracker : MonoBehaviour
    {
        internal static RecipeTracker? Current;
        private string _directory = "", _scope = "", _snapshot = "";
        private Player? _player;
        private long _characterId, _worldId;
        private Inventory? _inventory;
        private TrackerStore? _store;
        private TrackerHud? _view;
        private readonly TrackerCatalogue _catalogue = new TrackerCatalogue();
        private readonly Dictionary<string, long> _counts = new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly List<TrackerNote> _notes = new List<TrackerNote>();
        private bool _editing, _failed, _wasVisible, _dirty = true;
        private int _worldLevel = -1;
        private CraftAccess _access=(CraftAccess)(-1);
        private float _nextContext, _nextKnowledge, _saveAt;
        private int _released = -1;
        private GameObject? _previousFocus;
        internal int Revision { get; private set; }
        internal bool Ready => !_failed && _store != null && _player && _player == Player.m_localPlayer && _scope.Length > 0 &&
            Game.instance && Game.instance.GetPlayerProfile() != null && Game.instance.GetPlayerProfile().GetPlayerID() == _characterId &&
            ZNet.instance && ZNet.instance.GetWorld() != null && ZNet.instance.GetWorldUID() == _worldId;
        internal bool Contains(string key) => _store?.Contains(key) == true;
        internal static bool Editing => Current && Current!.isActiveAndEnabled && Current._editing && Current._view != null && Current._view.CanFit && InventoryPresentation.CanInteract && Current.CanShow();
        internal static FocusMode Focus => Editing && Current!._view!.Contains(EventSystem.current ? EventSystem.current.currentSelectedGameObject : null) ? FocusMode.Tracker : FocusMode.None;
        internal static bool BlocksGameInput => Focus != FocusMode.None || (Editing && Current!._view!.Dragging) || (Current && Current!._released == Time.frameCount);
        internal void Initialise(string preferences)
        { if(Current && Current != this)Current!.Dispose(); _directory = Path.Combine(preferences, "tracker"); Current = this; }
        private static void Warn(string text) => Debug.LogWarning("CraftyQoL tracker: " + text);
        private void InventoryChanged() { _counts.Clear(); _dirty = true; Revision++; }
        private void Context()
        {
            Player player = Player.m_localPlayer;
            if (!player || !Game.instance || !ZNet.instance || ZNet.instance.GetWorld() == null) { ClearScope(); return; }
            PlayerProfile profile=Game.instance.GetPlayerProfile();
            if(profile==null){ClearScope();return;}
            long character = profile.GetPlayerID(), world = ZNet.instance.GetWorldUID();
            if (character == 0 || world == 0) { ClearScope(); return; }
            string scope = TrackerMath.Scope(character, world);
            Inventory inventory = player.GetInventory();
            if(inventory==null){ClearScope();return;}
            if (_scope == scope && _player == player && ReferenceEquals(_inventory, inventory)) return;
            ClearScope(); _scope = scope; _player = player; _inventory = inventory; _characterId = character; _worldId = world;
            _store = new TrackerStore(Path.Combine(_directory, scope), Warn);
            _inventory.m_onChanged += InventoryChanged; _dirty = true; Revision++;
        }
        private void ClearScope()
        {
            ExitEdit();
            if (_inventory != null) _inventory.m_onChanged -= InventoryChanged;
            _store?.Save(); _store = null; _inventory = null; _player = null; _scope = ""; _characterId = _worldId = 0;
            _notes.Clear(); _counts.Clear(); _catalogue.Invalidate(); _snapshot = ""; _wasVisible = false;
            _view?.Dispose(); _view = null;
        }
        private bool CanShow()
        {
            if (_failed || !Ready || Plugin.Instance == null || !Plugin.Instance.Enabled.Value || !Hud.instance || !Hud.instance.m_rootObject ||
                !Hud.instance.IsVisible() || Hud.instance.m_userHidden || !_player || _player!.IsDead() || _player.IsTeleporting() || _player.InCutscene() || _player.IsSleeping()) return false;
            if (Hud.instance.m_loadingScreen && Hud.instance.m_loadingScreen.gameObject.activeSelf) return false;
            return !Minimap.IsOpen() && !Menu.IsActive() && !Console.IsVisible() && !TextInput.IsVisible() &&
                !StoreGui.IsVisible() && !UnifiedPopup.IsVisible() && !Hud.IsPieceSelectionVisible() && !Hud.InRadial() &&
                (!Chat.instance || !Chat.instance.HasFocus()) && (!TextViewer.instance || !TextViewer.instance.IsVisible()) &&
                !PlayerCustomizaton.IsBarberGuiVisible() && !ZNet.IsPasswordDialogShowing() && !GameCamera.InFreeFly() && !Game.instance.IsShuttingDown();
        }
        private void Update()
        {
            if (_failed || _directory.Length == 0) return;
            try
            {
                if (_player != Player.m_localPlayer || Time.unscaledTime >= _nextContext)
                { _nextContext = Time.unscaledTime + 0.5f; Context(); }
                if (!Ready) { ExitEdit(); _view?.Show(false,false); _wasVisible=false; return; }
                if (Plugin.Instance == null || !Plugin.Instance.Enabled.Value)
                { ExitEdit(); _view?.Show(false,false); _wasVisible=false; _store?.Save(); return; }
                if (!ObjectDB.instance || !ZNetScene.instance) { ExitEdit(); _view?.Show(false,false); _wasVisible=false; return; }
                if (_worldLevel != Game.m_worldLevel) { _worldLevel = Game.m_worldLevel; _counts.Clear(); _dirty=true; }
                var access=InventoryCrafting.Access;
                if(_access!=access){_access=access;_dirty=true;_counts.Clear();Revision++;}
                bool show=Visible(CanShow());
                if (show && (!_wasVisible || _dirty || Time.unscaledTime >= _nextKnowledge))
                { _nextKnowledge = Time.unscaledTime + 1f; RefreshModel(); }
                _wasVisible=show;
                if (!show || !TrackerUiPolicy.CanEdit(InventoryPresentation.CanInteract)) ExitEdit();
                if (_view != null && !_view.Alive) { ExitEdit(); _view.Dispose(); _view = null; _snapshot = ""; }
                if (show && _view == null && InventoryGui.instance)
                { _view = new TrackerHud(InventoryGui.instance, Hud.instance, _store!, this); _snapshot = ""; RefreshModel(); }
                _editing=show && TrackerUiPolicy.CanEdit(InventoryPresentation.CanInteract);
                _view?.Show(show,_editing);
                if (_store!.Dirty && Time.unscaledTime >= _saveAt) { bool failed=_store.SaveFailed; _store.Save(); if(failed!=_store.SaveFailed)_dirty=true; }
            }
            catch (Exception error) { _failed = true; ClearScope(); Warn("disabled for this session; crafting UI remains active. " + error); }
        }
        private void LateUpdate()
        {
            if (_view == null) return;
            if(_access!=InventoryCrafting.Access){_dirty=true;_wasVisible=false;ExitEdit();_view.Show(false,false);return;}
            bool show = Visible(CanShow());
            if (!show || !TrackerUiPolicy.CanEdit(InventoryPresentation.CanInteract)) ExitEdit();
            _view.Show(show,_editing);
            _view.StyleTooltip(show && _editing);
        }
        private void RefreshModel()
        {
            Plugin.Instance?.MigrateLoadedRecipes();
            _catalogue.Prepare(_player!,_store!.State.Pins.Exists(pin=>pin.Station==InventoryRules.Origin));
            _dirty = false; _notes.Clear();
            var plans = new List<TrackerPlan>(); var stamp = new StringBuilder();
            foreach (TrackerPin pin in _store!.State.Pins)
            {
                TrackerNote note;
                try { note = _catalogue.Resolve(_player!, pin); }
                catch (Exception error) when (error is InvalidDataException || error is NullReferenceException || error is ArgumentException)
                { note = new TrackerNote(pin, null, null); }
                _notes.Add(note); stamp.Append(pin.Key).Append(pin.Expanded);
                if (note.Plan == null) { stamp.Append('!'); continue; }
                plans.Add(note.Plan); stamp.Append(note.Plan.Name).Append(note.Plan.OutputAmount);
                foreach (var need in note.Plan.Needs) stamp.Append(need.Id).Append(need.Name).Append(need.Amount).Append(need.Quality);
                stamp.Append(note.Options.Count);
            }
            List<TrackerMaterial> materials = TrackerMath.Aggregate(plans, Owned);
            foreach (var m in materials) stamp.Append(m.Id).Append(m.Owned).Append(m.Required).Append(m.Deficit);
            stamp.Append(_store.State.Minimised).Append(_store.State.RecipesTab).Append(_store.State.HideCompleted).Append(_store.SaveFailed);
            string snapshot = stamp.ToString();
            if (snapshot != _snapshot) Revision++;
            if (_view != null && snapshot != _snapshot) _view.Render(_notes, materials, _store.SaveFailed);
            _snapshot = snapshot;
        }
        internal long Owned(string id, int quality)
        {
            string key = id + "/" + quality;
            if (_counts.TryGetValue(key, out long value)) return value;
            value = _inventory != null && _catalogue.Items.TryGetValue(id, out ItemDrop item) && item
                ? _inventory.CountItems(item.m_itemData.m_shared.m_name, quality, true) : 0;
            _counts[key] = value; return value;
        }
        internal Sprite? Icon(string id) => _catalogue.Items.TryGetValue(id, out ItemDrop item) && item ? item.m_itemData.GetIcon() : null;
        internal TrackerPin? Candidate(Recipe recipe, string origin)
        {
            if (!Ready || !ObjectDB.instance) return null;
            try { return _catalogue.Create(_player!, recipe, origin,origin!=InventoryRules.Origin || FoodView.Current && FoodView.Current!.CanPinInventory(recipe)); }
            catch (Exception error) when (error is InvalidDataException || error is NullReferenceException || error is ArgumentException) { return null; }
        }
        internal bool Toggle(Recipe recipe, string origin)
        {
            var pin = Candidate(recipe, origin); if (pin == null || !_store!.Toggle(pin)) return false;
            Changed(); return true;
        }
        internal void Migrate(IReadOnlyList<RecipeMove> approved)
        {if(_store?.Migrate(approved)==true)Changed();}
        internal void Changed() { _store?.Touch(); _saveAt = Time.unscaledTime + 0.25f; _counts.Clear(); _dirty = true; Revision++; }
        internal void Remove(string key) { if(!Editing)return; _released=Time.frameCount; _store?.Remove(key); Changed(); }
        private bool Visible(bool allowed) => TrackerUiPolicy.Visible(allowed,_store!=null && _store.State.Pins.Count>0,
            InventoryPresentation.Open,_store?.State.KeepVisible==true);
        internal void ToggleKeepVisible()
        {
            if (!Editing || _store == null) return;
            _store.State.KeepVisible=!_store.State.KeepVisible;
            _view?.RefreshPin(); _store.Touch(); _saveAt=Time.unscaledTime+0.25f;
        }
        // Share the inventory's cursor; only take controller focus on an explicit shoulder action.
        internal void InventoryInput(InventoryGui gui)
        {
            if (!Editing || !ZInput.IsGamepadActive() && Focus==FocusMode.None) return;
            bool focused=Focus!=FocusMode.None;
            if (focused && (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB") ||
                ZInput.GetButtonDown("JoyTabLeft") || ZInput.GetButtonDown("JoyTabRight")))
            {
                if (!_view!.CloseSettings()) ReturnFocus();
                _released=Time.frameCount;
            }
            else if (!focused && gui.ActiveGroup==gui.m_uiGroups.Length-1 && ZInput.GetButtonDown("JoyTabRight"))
            {
                _previousFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
                _view!.BeginEdit(); _released=Time.frameCount;
            }
        }
        internal static bool Released => Current && Current!._released==Time.frameCount;
        private void ReturnFocus()
        {
            if (EventSystem.current && _view != null && _view.Contains(EventSystem.current.currentSelectedGameObject))
                EventSystem.current.SetSelectedGameObject(_previousFocus && _previousFocus!.activeInHierarchy ? _previousFocus : null);
            _previousFocus=null;
        }
        internal void Alternative(string key)
        {
            if(!Editing)return;
            TrackerNote? note = _notes.Find(n => n.Pin.Key == key);
            if (note == null || note.Options.Count < 2) return;
            int at = note.Options.FindIndex(o => o.Item.gameObject.name == note.Pin.Alternative && o.Quality == note.Pin.Quality);
            var next = note.Options[(at + 1) % note.Options.Count]; note.Pin.Alternative = next.Item.gameObject.name; note.Pin.Quality = next.Quality; Changed();
        }
        internal void ExitEdit()
        {
            if (!_editing) return;
            _view?.EndDrag();
            _editing = false;
            ReturnFocus();
            _view?.Show(Visible(CanShow()),false);
        }
        internal void Dispose() { ClearScope(); if (Current == this) Current = null; }
        private void OnDisable() { ExitEdit(); _store?.Save(); _view?.Show(false,false); _wasVisible=false; }
        private void OnDestroy() => Dispose();
    }
}
