using System;
using CraftyQoL.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace CraftyQoL
{
    public sealed class FoodView : MonoBehaviour
    {
        internal static FoodView? Current;
        private InventoryGui _gui = null!;
        private NativeToolbar? _toolbar;
        private TrackerPinControl? _pin;
        private CraftButtonStyle? _buttonStyle;
        private bool _pinUnavailable;
        private bool _shown, _closing, _dirty;
        private float _refreshAt;
        private int _releasedFrame = -10;
        private CraftingStation? _station;
        internal QueryState State = null!;
        private readonly RecipeMetadata _metadata = new RecipeMetadata();
        private readonly TooltipBackground _tooltipBackground = new TooltipBackground();
        private readonly Dictionary<RecipeInfo, Recipe> _sources = new Dictionary<RecipeInfo, Recipe>();
        private readonly List<RecipeRow> _rows = new List<RecipeRow>();
        private readonly List<RowDecoration> _decorations = new List<RowDecoration>();
        private readonly HashSet<GameObject> _rowActions = new HashSet<GameObject>();
        private float _nextContextCheck;
        private string _language = "";
        private int _discovered;
        private float _savedScroll;
        private Recipe? _previousSelection;
        private bool _userRefresh, _applyingUserRefresh;
        private bool _pendingSearch;
        private float _searchDue;
        private string _committedSearch = "";
        private QueryState _applied = new QueryState();
        private bool _hasApplied,_availabilityDirty;
        private CraftAccess _access,_sourceAccess;
        private readonly HashSet<Recipe> _lastFiltered = new HashSet<Recipe>();

        internal static void Present(InventoryGui gui, QueryState state)
        {
            if (Current && Current!._gui != gui) Current!.CloseOwned();
            FoodView view = gui.GetComponent<FoodView>() ?? gui.gameObject.AddComponent<FoodView>();
            Current = view;
            view._gui = gui;
            bool changedState = !ReferenceEquals(view.State, state);
            bool reopening = !view._shown || view._closing;
            if (changedState && view._shown) view.Suspend();
            if (view._toolbar != null && view.State != null && (view.State.Inventory != state.Inventory || !state.Inventory && (view.State.Equipment != state.Equipment || view.State.Mead != state.Mead)))
            { view._toolbar.Dispose(); view._toolbar = null; }
            view.State = state;
            view._access=InventoryCrafting.Access;
            if(state.Inventory)state.Disclosure.Observe(view._access);
            if (view._toolbar == null)
                view._toolbar = new NativeToolbar(gui, state, view.Changed, view.ReleaseLatch);
            else if (changedState || state.Inventory) view._toolbar.Bind(state);
            if (view._pin == null && !view._pinUnavailable)
            {
                try { view._pin = new TrackerPinControl(gui); }
                catch(Exception error) { view._pinUnavailable=true;Debug.LogWarning("CraftyQoL detail pin unavailable: "+error.Message); }
            }
            if(view._buttonStyle==null)view._buttonStyle=new CraftButtonStyle(gui);
            view._closing = false;
            view._toolbar.SetInteraction(true);
            view._pin?.SetInteraction(true);
            view._toolbar.Show(true);
            view._shown = true;
            if (changedState || reopening)
            {
                view._committedSearch = state.Search;
                view._pendingSearch = false;
                view._language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            }
            view._station = Player.m_localPlayer.GetCurrentCraftingStation();
        }
        internal bool IsFor(InventoryGui gui) => _shown && !_closing && _gui == gui;
        internal FocusMode Focus
        {
            get
            {
                if (!_shown || _closing || _toolbar == null || !_gui || !_gui.InCraftTab()) return FocusMode.None;
                if (_releasedFrame == Time.frameCount) return FocusMode.Released;
                if (_toolbar.Editing) return FocusMode.Text;
                return EventSystem.current && (_toolbar.Contains(EventSystem.current.currentSelectedGameObject) ||
                    _rowActions.Contains(EventSystem.current.currentSelectedGameObject) || (_pin?.Contains(EventSystem.current.currentSelectedGameObject) == true))
                    ? FocusMode.Toolbar : FocusMode.None;
            }
        }
        internal void ReleaseLatch() => _releasedFrame = Time.frameCount;
        private void Changed(bool immediate)
        {
            if (_closing) return;
            if (immediate && _hasApplied && !_pendingSearch && State.Search == _committedSearch &&
                State.SameQuery(_applied)) return;
            _userRefresh = true;
            if (!immediate)
            {
                _pendingSearch = true;
                _searchDue = Time.unscaledTime + 0.15f;
            }
            else if (_toolbar == null || !_toolbar.Search.IsCompositionActive())
            { _committedSearch = State.Search; _pendingSearch = false; }
            Schedule(immediate);
        }
        private void Schedule(bool immediate)
        {
            _dirty = true;
            _refreshAt = immediate ? 0f : Time.unscaledTime + 0.15f;
        }
        private void ToggleFavourite(RecipeInfo info)
        {
            if (_closing || !InventoryPresentation.Open) return;
            Plugin? plugin = Plugin.Instance;
            if (plugin == null || !info.Persistable || !plugin.Favourites.Toggle(info.Key)) return;
            plugin.FavouriteChanged();
            ReleaseLatch();
            RefreshFavouriteVisuals();
            if (State.FavouritesOnly) { _userRefresh = true; Schedule(true); }
        }
        internal void RefreshFavouriteVisuals()
        {
            if (!_shown || _closing || Plugin.Instance == null) return;
            foreach (RowDecoration row in _decorations)
                row.SetFavourite(Plugin.Instance.Favourites.Keys.Contains(row.Key), Plugin.Instance.Favourites.SaveFailed);
        }
        private void FocusSelectedStar()
        {
            var selected = (Recipe)GameContracts.PairRecipe.GetValue(GameContracts.SelectedPair.GetValue(_gui), null);
            int index = _rows.FindIndex(row => row.Recipe == selected);
            if (index >= 0 && index < _decorations.Count && _decorations[index].Favourite.interactable && EventSystem.current)
            {
                _toolbar?.EnsureVisible(_rows[index].Element, false);
                _decorations[index].Favourite.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnRight = _pin != null && _pin.Button.interactable && _pin.Button.gameObject.activeSelf ? _pin.Button : _gui.m_craftButton, selectOnLeft = _gui.m_craftButton, selectOnDown = _gui.m_craftButton, selectOnUp = _gui.m_craftButton };
                EventSystem.current.SetSelectedGameObject(_decorations[index].Favourite.gameObject);
            }
        }
        internal void RefreshInventoryAvailability(){_availabilityDirty=true;Schedule(true);}
        internal bool CanPinInventory(Recipe recipe)=>_shown && !_closing && State.Inventory && InventoryPresentation.Open &&
            StationScope.Identify(_gui)==InventoryRules.Origin && _sourceAccess==InventoryCrafting.Access && _rows.Exists(row=>row.Recipe==recipe);
        internal List<Recipe> Filter(List<Recipe> source)
        {
            _sourceAccess=InventoryCrafting.Access;
            _metadata.Ensure(State.Mead || State.Inventory,State.Inventory);
            _sources.Clear();
            var infos = new List<RecipeInfo>(source.Count);
            foreach (Recipe recipe in source)
            {
                if (recipe.m_noCraftOnlyUpgrade) continue; // Same exclusion as native InCraftTab population.
                RecipeInfo info = _metadata.Get(recipe);
                infos.Add(info); _sources[info] = recipe;
            }
            UpdateDiscovery(false);
            if(State.Inventory)
            {
                if(InventoryRules.HasBiomes(State.Family))_toolbar?.SetBiomes(infos.FindAll(info=>info.Family==State.Family),_discovered);
            }
            else if(!State.Mead)_toolbar?.SetBiomes(infos, _discovered);
            if (_hasApplied && (float)GameContracts.CraftTimer.GetValue(_gui) >= 0)
            {
                var held = new List<Recipe>(source.Count);
                foreach (Recipe recipe in source) if (_lastFiltered.Contains(recipe)) held.Add(recipe);
                return held;
            }
            // A native refresh can occur during a craft too. Hold every query field, not just our scheduled refresh.
            if (!_hasApplied || (float)GameContracts.CraftTimer.GetValue(_gui) < 0)
            {
                _applied = new QueryState(State.Equipment,State.Mead,State.Inventory) { Family=State.Family, Category = State.Category, MeadCategory=State.MeadCategory, Sort = State.Sort, Biome = State.Biome,
                    EquipmentCategory = State.EquipmentCategory, EquipmentSort = State.EquipmentSort,
                    FavouritesOnly = State.FavouritesOnly, Search = _committedSearch };
                _hasApplied = true;
            }
            List<RecipeInfo> selected = _applied.Inventory?InventoryRules.Filter(infos,_applied,Plugin.Instance!.Favourites.Keys):_applied.Equipment
                ? EquipmentQuery.Filter(infos, _applied, Plugin.Instance!.Favourites.Keys)
                : _applied.Mead ? MeadQuery.Filter(infos,_applied,Plugin.Instance!.Favourites.Keys)
                : RecipeQuery.Filter(infos, _applied, Plugin.Instance!.Favourites.Keys);
            var result = new List<Recipe>(selected.Count);
            _lastFiltered.Clear();
            foreach (RecipeInfo info in selected) { Recipe recipe = _sources[info]; result.Add(recipe); _lastFiltered.Add(recipe); }
            return result;
        }
        internal void BeforePanel()
        {
            if (!_shown) return;
            _savedScroll = _gui.m_recipeListScroll.value;
            _previousSelection = (Recipe)GameContracts.PairRecipe.GetValue(GameContracts.SelectedPair.GetValue(_gui), null);
            if (_pendingSearch && Time.unscaledTime >= _searchDue && _toolbar != null && !_toolbar.Search.IsCompositionActive())
            { _committedSearch = State.Search; _pendingSearch = false; }
        }
        internal void RowsPopulated()
        {
            ClearDecorations();
            var pairs = (IList)GameContracts.Pairs.GetValue(_gui);
            _toolbar?.ShowEmpty(pairs.Count == 0);
            _rows.Clear();
            for (int i = 0; i < pairs.Count; i++)
            {
                object pair = pairs[i];
                var recipe = (Recipe)GameContracts.PairRecipe.GetValue(pair, null);
                var element = (GameObject)GameContracts.PairElement.GetValue(pair, null);
                if (!element || !(element.transform is RectTransform) || !element.transform.Find("name") ||
                    !element.transform.Find("name").GetComponent<TMP_Text>())
                    throw new NotSupportedException("Unsupported recipe-row hierarchy.");
                _rows.Add(new RecipeRow(pair, recipe, _metadata.Get(recipe), element));
            }
            var original = new List<RecipeRow>(_rows);
            try
            {
                if (_applied.Inventory || _applied.Equipment || _applied.Mead)
                {
                    if (_applied.EquipmentSort == EquipmentSort.Alphabetical)
                        StableOrder.Sort(_rows, (a, b) => EquipmentQuery.Compare(a.Info, b.Info));
                }
                else if (_applied.Sort != FoodSort.Vanilla)
                    StableOrder.Sort(_rows, (a, b) => RecipeQuery.Compare(a.Info, b.Info, _applied.Sort));
                for (int i = 0; i < _rows.Count; i++)
                {
                    RecipeRow row = _rows[i];
                    pairs[i] = row.Pair;
                    ((RectTransform)row.Element.transform).anchoredPosition = new Vector2(0, -i * _gui.m_recipeListSpace);
                    var decoration = new RowDecoration(_gui, row, _applied.Biome, _discovered, Plugin.Instance!.Favourites.Keys.Contains(row.Info.Key), () => ToggleFavourite(row.Info),
                        _applied.Inventory?row.Info.Family!=RecipeFamily.Food && row.Info.Family!=RecipeFamily.Mead:_applied.Equipment,
                        _applied.Inventory?row.Info.Family==RecipeFamily.Mead:_applied.Mead,
                        _applied.Inventory && row.Info.Family==RecipeFamily.Other);
                    _decorations.Add(decoration);
                    _rowActions.Add(decoration.Favourite.gameObject);
                }
            }
            catch
            {
                ClearDecorations();
                if (pairs.Count == original.Count)
                    for (int i = 0; i < original.Count; i++)
                    {
                        pairs[i] = original[i].Pair;
                        if (original[i].Element)
                            ((RectTransform)original[i].Element.transform).anchoredPosition = new Vector2(0, -i * _gui.m_recipeListSpace);
                    }
                throw;
            }
        }
        internal void AfterPanel()
        {
            if (!_shown) return;
            _gui.m_recipeListScroll.value = _rows.Count == 0 ? 1 : _savedScroll;
            object selected = GameContracts.SelectedPair.GetValue(_gui);
            var recipe = (Recipe)GameContracts.PairRecipe.GetValue(selected, null);
            int index = _rows.FindIndex(row => row.Recipe == recipe);
            if (_rows.Count > 0 && index < 0)
                GameContracts.SelectRecipe.Invoke(_gui, new object[] { 0, true });
            else if (index >= 0 && (_previousSelection != recipe || _applyingUserRefresh || (_userRefresh && !_pendingSearch)))
                _toolbar?.EnsureVisible(_rows[index].Element, _previousSelection != recipe);
            // Native refresh also services already-dirty inventory/query state.
            bool deferred = _userRefresh && (float)GameContracts.CraftTimer.GetValue(_gui) >= 0;
            _dirty = _pendingSearch || deferred;
            if (_pendingSearch) _refreshAt = _searchDue;
            else if (!deferred) _userRefresh = false;
        }
        private void ClearDecorations()
        {
            if (EventSystem.current && _rowActions.Contains(EventSystem.current.currentSelectedGameObject))
                EventSystem.current.SetSelectedGameObject(_toolbar != null ? _toolbar.FocusTarget : null);
            foreach (RowDecoration decoration in _decorations) decoration.Dispose();
            _decorations.Clear(); _rowActions.Clear();
        }
        private void UpdateDiscovery(bool updatePalette = true)
        {
            if(State.Mead && !State.Inventory)return;
            int discovered = BiomeDiscovery.Read();
            if (discovered == _discovered) return;
            _discovered = discovered;
            if (updatePalette) _toolbar?.SetDiscovery(discovered);
            foreach (RowDecoration row in _decorations) row.RefreshDiscovery(discovered);
        }
        private void Update()
        {
            if (!_shown || !_gui || _toolbar == null) return;
            try
            {
                if (Plugin.Instance == null || !Plugin.Instance.Enabled.Value || !Player.m_localPlayer || !_gui.InCraftTab())
                { Suspend(); return; }
                if (_closing)
                {
                    if (InventoryPresentation.Open) Plugin.Instance.PanelChanged(_gui);
                    else if (InventoryPresentation.FinishedClosing(_gui)) Suspend();
                    return;
                }
                if (!InventoryPresentation.Open) { BeginClose(); return; }
                if (_station != Player.m_localPlayer.GetCurrentCraftingStation())
                { Plugin.Instance.PanelChanged(_gui); return; }
                if(State.Inventory && _access!=InventoryCrafting.Access)
                {
                    _access=InventoryCrafting.Access;State.Disclosure.Observe(_access);_toolbar.Bind(State);
                    _availabilityDirty=true;Schedule(true);
                }
                if (Time.unscaledTime >= _nextContextCheck)
                {
                    _nextContextCheck = Time.unscaledTime + 1f;
                    string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
                    if (_language != language) { _language = language; _toolbar.InvalidatePaletteLabels(); Schedule(true); }
                    UpdateDiscovery();
                }
                _pin?.Refresh();
                FocusMode focus = Focus;
                if (focus != FocusMode.Text && (ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) && ZInput.GetKeyDown(KeyCode.P))
                { _pin?.Toggle(); ReleaseLatch(); }
                if (focus != FocusMode.None && focus != FocusMode.Released &&
                    (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB")))
                {
                    _toolbar.Search.DeactivateInputField();
                    _toolbar.Search.ToggleEventSystemNavigation(true);
                    if (EventSystem.current) EventSystem.current.SetSelectedGameObject(_gui.m_craftButton.gameObject);
                    ReleaseLatch();
                }
                else if (focus == FocusMode.None && RecipeTracker.Focus == FocusMode.None && _gui.ActiveGroup == 3 &&
                         ZInput.IsGamepadActive() && ZInput.GetButtonDown("JoyDPadLeft"))
                {
                    if (EventSystem.current) EventSystem.current.SetSelectedGameObject(_toolbar.FocusTarget);
                }
                if (focus == FocusMode.None && RecipeTracker.Focus == FocusMode.None && _gui.ActiveGroup == 3 && ZInput.IsGamepadActive() && ZInput.GetButtonDown("JoyDPadRight"))
                    FocusSelectedStar();
                if ((ZInput.GetKey(KeyCode.LeftControl) || ZInput.GetKey(KeyCode.RightControl)) && ZInput.GetKeyDown(KeyCode.F))
                {
                    _toolbar.FocusSearch();
                }
                if (_dirty && Time.unscaledTime >= _refreshAt && !_toolbar.Search.IsCompositionActive() &&
                    (_availabilityDirty || (float)GameContracts.CraftTimer.GetValue(_gui) < 0))
                {
                    _availabilityDirty=false;
                    _dirty = false;
                    _applyingUserRefresh = _userRefresh;
                    _userRefresh = false;
                    try { GameContracts.RefreshPanel.Invoke(_gui, new object[] { false }); }
                    finally { _applyingUserRefresh = false; }
                }
            }
            catch (Exception error) { Plugin.Instance?.Fail(error); }
        }
        private void LateUpdate()
        {
            if (_shown && !_closing && _gui && _toolbar != null) _tooltipBackground.Update(_toolbar, _rows, _rowActions, _gui.m_crafting.gameObject);
        }
        internal void BeginClose()
        {
            if (!_shown || _closing) return;
            if (!InventoryPresentation.CanAnimateClose(_gui)) { Suspend(); return; }
            _closing = true; _dirty = false;
            _tooltipBackground.Restore();
            _toolbar?.SetInteraction(false);
            _pin?.SetInteraction(false);
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject &&
                (_toolbar?.Contains(EventSystem.current.currentSelectedGameObject) == true ||
                 _rowActions.Contains(EventSystem.current.currentSelectedGameObject) ||
                 _pin?.Contains(EventSystem.current.currentSelectedGameObject) == true))
                EventSystem.current.SetSelectedGameObject(null);
        }
        internal void Suspend()
        {
            _closing = false;
            _tooltipBackground.Restore();
            _buttonStyle?.Dispose();_buttonStyle=null;
            if (!_shown) return;
            _shown = false;
            _pin?.Hide();
            _dirty = false;_availabilityDirty=false;
            _hasApplied = false;
            _lastFiltered.Clear();
            _userRefresh = _applyingUserRefresh = false;
            _releasedFrame = -10;
            ClearDecorations();
            _rows.Clear(); _sources.Clear();
            _metadata.Invalidate();
            if (_toolbar != null)
            {
                if (EventSystem.current && _toolbar.Contains(EventSystem.current.currentSelectedGameObject))
                    EventSystem.current.SetSelectedGameObject(null);
                _toolbar.Show(false);
            }
            Plugin.Instance?.FlushPreferences();
        }
        internal void CloseOwned()
        {
            Suspend();
            _pin?.Dispose(); _pin = null;
            _toolbar?.Dispose();
            _toolbar = null;
            if (Current == this) Current = null;
        }
        private void OnDisable() => Suspend();
        private void OnDestroy() => CloseOwned();
    }
}
