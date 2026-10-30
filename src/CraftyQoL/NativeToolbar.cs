using System;
using CraftyQoL.Core;
using GUIFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CraftyQoL
{
    internal sealed class NativeToolbar : IDisposable
    {
        private readonly EquipmentToolbarGeometry _equipmentGeometry;
        private InventoryToolbarGeometry InventoryGeometry=>new InventoryToolbarGeometry(_viewport.rect.width,_state.Family,_state.Disclosure.Expanded,_state.BiomesExpanded);
        private float Height => _state.Inventory?InventoryGeometry.Height:_state.Mead ? _equipmentGeometry.Height(false) : _state.Equipment ? _equipmentGeometry.Height(_state.BiomesExpanded) : ToolbarGeometry.FoodHeight(_state.BiomesExpanded);
        private float _reservedHeight;
        private Button? _biomeToggle;
        private Button? _filters;
        private Button[] _families=Array.Empty<Button>();
        private readonly Dictionary<RecipeFamily,Button[]> _inventoryGroups=new Dictionary<RecipeFamily,Button[]>();
        private readonly RectTransform _separator;
        private readonly GameObject _root;
        private readonly RectTransform _viewport, _scrollbar;
        private readonly Vector2 _viewportTop, _scrollbarTop;
        private Button[] _types;
        private readonly Button[] _sorts;
        private readonly Button _faves;
        private readonly Button? _allBiomes;
        private readonly StarGraphic _filterStar;
        private readonly Button[] _palette = new Button[ToolbarGeometry.PaletteSlots];
        private readonly BiomeChoice[] _choices = new BiomeChoice[ToolbarGeometry.PaletteSlots];
        private int _discovered, _available, _visibleOptions;
        private bool _unknown, _paletteReady;
        private readonly Action<bool> _changed;
        private readonly Button _clear;
        internal readonly GuiInputField Search;
        internal GameObject FocusTarget => _state.Inventory?(_state.Disclosure.Expanded?_families[0].gameObject:_filters!.gameObject):_types[0].gameObject;
        private QueryState _state;
        private bool _visible, _disposed;
        private readonly InventoryGui _gui;
        private readonly float _baseHeight;
        private readonly GameObject _empty;

        internal NativeToolbar(InventoryGui gui, QueryState state, Action<bool> changed, Action released)
        {
            _state = state; _changed = changed;
            _types = new Button[state.Inventory?0:state.Equipment ? 8 : state.Mead ? 6 : 4]; _sorts = new Button[state.Inventory || state.Equipment || state.Mead ? 2 : 4];
            _gui = gui;
            _baseHeight = (float)GameContracts.BaseHeight.GetValue(gui);
            _viewport = gui.m_recipeListRoot.parent as RectTransform
                        ?? throw new NotSupportedException("Recipe viewport is not a RectTransform.");
            _scrollbar = gui.m_recipeListScroll.transform as RectTransform
                         ?? throw new NotSupportedException("Recipe scrollbar is not a RectTransform.");
            if (_viewport.rect.width < ToolbarGeometry.MinimumWidth || _viewport.rect.height < 320f)
                throw new NotSupportedException("Recipe viewport is too small for the qualified toolbar layout.");
            if (!TextInput.instance || !TextInput.instance.m_inputField)
                throw new NotSupportedException("Native text input template unavailable.");
            _equipmentGeometry = new EquipmentToolbarGeometry(_viewport.rect.width);
            _viewportTop = _viewport.offsetMax;
            _scrollbarTop = _scrollbar.offsetMax;
            _root = new GameObject("CraftyQoL.Toolbar", typeof(RectTransform));
            _root.SetActive(false);
            try
            {
                var rect = (RectTransform)_root.transform;
                rect.SetParent(_viewport.parent, false);
                float width = _viewport.rect.width;
                var geometry = new ToolbarGeometry(width);
                Place(rect, 0, 0, width, Height);
                Search = UnityEngine.Object.Instantiate(TextInput.instance.m_inputField, rect, false);
                Search.name = "CraftyQoL.Search";
                foreach (LayoutElement layout in Search.GetComponentsInChildren<LayoutElement>(true)) layout.ignoreLayout = true;
                foreach (UIGamePad shortcut in Search.GetComponentsInChildren<UIGamePad>(true))
                { shortcut.enabled = false; UnityEngine.Object.Destroy(shortcut); }
                Search.onValueChanged = new TMP_InputField.OnChangeEvent();
                Search.onEndEdit = new TMP_InputField.SubmitEvent();
                Search.onSubmit = new TMP_InputField.SubmitEvent();
                Search.onSelect = new TMP_InputField.SelectionEvent();
                Search.onDeselect = new TMP_InputField.SelectionEvent();
                Search.OnInputSubmit = new OnInputSubmitEvent();
                Search.characterLimit = 128;
                Search.contentType = TMP_InputField.ContentType.Standard;
                Search.lineType = TMP_InputField.LineType.SingleLine;
                Search.VirtualKeyboardTitle = state.Inventory?"Search crafting recipes":state.Equipment ? "Search equipment recipes" : state.Mead ? "Search mead recipes" : "Search food recipes";
                Search.VirtualKeyboardOnActivate = true;
                Search.shouldActivateOnSelect = false;
                Search.textComponent.enableAutoSizing = false;
                Search.textComponent.fontSize = UiStyle.SearchSize;
                Search.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                Search.textComponent.margin = Vector4.zero;
                RectTransform inputText = Search.textComponent.rectTransform;
                inputText.anchorMin = Vector2.zero; inputText.anchorMax = Vector2.one;
                inputText.offsetMin = inputText.offsetMax = Vector2.zero;
                RectTransform textViewport = Search.textViewport;
                textViewport.anchorMin = Vector2.zero; textViewport.anchorMax = Vector2.one;
                textViewport.offsetMin = new Vector2(UiStyle.SearchPaddingX, UiStyle.SearchPaddingY); textViewport.offsetMax = new Vector2(-UiStyle.SearchPaddingX, -UiStyle.SearchPaddingY);
                if (Search.placeholder is TMP_Text placeholder)
                {
                    placeholder.text = "Search"; placeholder.enableAutoSizing = false; placeholder.fontSize = UiStyle.PlaceholderSize;
                    placeholder.alignment = TextAlignmentOptions.MidlineLeft; placeholder.margin = Vector4.zero;
                    placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one;
                    placeholder.rectTransform.offsetMin = placeholder.rectTransform.offsetMax = Vector2.zero;
                }
                Place((RectTransform)Search.transform, geometry.Search);
                UiStyle.NativeBorder(Search.GetComponent<Image>());UiStyle.Round(Search.GetComponent<Image>());
                Search.colors = UiStyle.Colours(UiStyle.InputSurface);
                Search.onValueChanged.AddListener(value => { _state.Search = value; changed(false); });
                Search.onEndEdit.AddListener(_ => { released(); changed(true); });
                _clear = MakeButton(gui, rect, "X", "Clear search", () =>
                { if (_state.Search.Length == 0) return; Search.SetTextWithoutNotify(""); _state.Search = ""; changed(true); }, geometry.Clear);
                _faves = MakeButton(gui, rect, "", "Show favourites only", () =>
                { _state.FavouritesOnly = !_state.FavouritesOnly; Labels(); changed(true); }, geometry.Favourites);
                _filterStar = UiStyle.AddStar(_faves);
                var equipmentGeometry = _equipmentGeometry;
                if(state.Inventory)BuildInventoryControls(gui,rect);
                else if (state.Equipment || state.Mead)
                {
                    Caption(rect, "TYPE", new UiBox(ToolbarGeometry.Outer, ToolbarGeometry.CaptionY, geometry.ContentWidth, ToolbarGeometry.CaptionHeight));
                    string[] labels = state.Mead ? new[]{"All","Heal","Stam","Eitr","Res","Other"} : new[]{"All", "Wpn", "Arm", "Shld", "Tool", "Ammo", "Magic", "Other"};
                    for (int i = 0; i < _types.Length; i++)
                    {
                        int option = i;
                        _types[i] = MakeButton(gui, rect, labels[i], "Type: " + (state.Mead?(i==0?"All":UiStyle.MeadLabels((MeadFlags)(1<<(i-1)))):((EquipmentCategory)i).ToString()), () =>
                        { if(_state.Mead){if(_state.MeadCategory==(MeadCategory)option)return;_state.MeadCategory=(MeadCategory)option;}
                          else {if (_state.EquipmentCategory == (EquipmentCategory)option) return; _state.EquipmentCategory = (EquipmentCategory)option;} Labels(); changed(true); }, state.Mead?equipmentGeometry.PotionCategory(i):equipmentGeometry.Category(i));
                    }
                    if(state.Mead)MeadCategoryIcons.Attach(_types);else EquipmentCategoryIcons.Attach(_types);
                    for (int i = 0; i < _sorts.Length; i++)
                    {
                        int option = i;
                        _sorts[i] = MakeButton(gui, rect, i == 0 ? "=" : "A-Z", i == 0 ? "Sort: Game ordering" : "Sort: Localised name A-Z", () =>
                        { if (_state.EquipmentSort == (EquipmentSort)option) return; _state.EquipmentSort = (EquipmentSort)option; Labels(); changed(true); }, equipmentGeometry.Sort(i));
                    }
                    if(!state.Mead)_biomeToggle = MakeButton(gui, rect, "Biomes >", "Expand biome filters", TogglePalette, equipmentGeometry.Disclosure);
                }
                else
                {
                    Caption(rect, "TYPE", geometry.TypeCaption);
                    Caption(rect, "SORT", geometry.SortCaption);
                    string[] typeLabels = { "All", "H", "S", "E" };
                    string[] sortLabels = { "=", "H", "S", "E" };
                    for (int i = 0; i < 4; i++)
                    {
                        int option = i;
                        _types[i] = MakeButton(gui, rect, typeLabels[i], "Type: " + UiStyle.FoodLabel(i,((FoodCategory)i).ToString()), () =>
                        { if (_state.Category == (FoodCategory)option) return; _state.Category = (FoodCategory)option; Labels(); changed(true); }, geometry.Option(i, false));
                    }
                    for(int i=0;i<_sorts.Length;i++)
                    {
                        int option=i;
                        _sorts[i] = MakeButton(gui,rect,sortLabels[i],i==0?"Sort: Game ordering":"Sort: "+UiStyle.FoodLabel(i,((FoodSort)i).ToString()),()=>
                        { if(_state.Sort==(FoodSort)option)return;_state.Sort=(FoodSort)option;Labels();changed(true); },geometry.Option(i,true));
                    }
                    _biomeToggle=MakeButton(gui,rect,"Biomes >","Expand biome filters",TogglePalette,geometry.FoodDisclosure);
                }
                if(state.Inventory || !state.Mead)
                {
                _allBiomes = MakeButton(gui, rect, "All", "Reset to all biomes", () =>
                { if (_state.Biome == BiomeChoice.All) return; _state.Biome = BiomeChoice.All; Labels(); changed(true); }, state.Equipment ? equipmentGeometry.Reset : geometry.Reset);
                for (int i = 0; i < _palette.Length; i++)
                {
                    int slot = i;
                    _palette[i] = MakeButton(gui, rect, "", "", () => ChooseBiome(slot), state.Equipment ? equipmentGeometry.Biome(i) : geometry.Biome(i));
                    _palette[i].name = "CraftyQoL.BiomeSlot" + i;
                    _palette[i].interactable = false;
                    _palette[i].gameObject.SetActive(false);
                }
                }
                _separator = UiStyle.Separator(rect, geometry.Separator);
                ApplyPaletteVisibility();
                WireNavigation();
                Bind(state);
                _empty = new GameObject("CraftyQoL.Empty", typeof(RectTransform), typeof(TextMeshProUGUI));
                _empty.transform.SetParent(_viewport, false);
                var emptyText = _empty.GetComponent<TextMeshProUGUI>();
                emptyText.font = Search.textComponent.font;
                emptyText.fontSize = 14; emptyText.alignment = TextAlignmentOptions.Center;
                emptyText.text = "No matching recipes"; emptyText.raycastTarget = false;
                Place(emptyText.rectTransform, 0, 4, width, 40);
                _empty.SetActive(false);
            }
            catch
            {
                UnityEngine.Object.Destroy(_root);
                if (_empty) UnityEngine.Object.Destroy(_empty);
                throw;
            }
        }

        private void ToggleFilters()
        {
            _state.Disclosure.Toggle();
            if(!_state.Disclosure.Expanded)Search.DeactivateInputField();
            ApplyInventoryLayout();ApplyPaletteVisibility();Labels();WireNavigation();
            if(_visible){ApplyReservation(Height);EnsureSelectionVisible();}
        }
        internal void FocusSearch()
        {
            if(_state.Inventory && !_state.Disclosure.Expanded)ToggleFilters();
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(Search.gameObject);
            Search.ActivateInputField();
        }
        private void BuildInventoryControls(InventoryGui gui,RectTransform rect)
        {
            var geometry=InventoryGeometry;
            _filters=MakeButton(gui,rect,"Filters >","Show or hide inventory filters; * means filters are active",ToggleFilters,geometry.Toggle);
            _families=new Button[5];
            string[] names={"All","Equip","Food","Mead","Other"};
            for(int i=0;i<5;i++)
            {
                int option=i;
                _families[i]=MakeButton(gui,rect,names[i],"Recipe family: "+(RecipeFamily)i,()=>
                {
                    if(_state.Family==(RecipeFamily)option)return;
                    _state.Family=(RecipeFamily)option;_state.Biome=BiomeChoice.All;_state.BiomesExpanded=false;
                    _paletteReady=false;
                    ApplyInventoryLayout();ApplyPaletteVisibility();Labels();WireNavigation();
                    if(_visible)ApplyReservation(Height);
                    _changed(true);
                },geometry.Family(i));
            }
            foreach(var family in new[]{RecipeFamily.Equipment,RecipeFamily.Food,RecipeFamily.Mead})
            {
                int count=family==RecipeFamily.Equipment?8:family==RecipeFamily.Mead?6:4;
                var buttons=new Button[count];_inventoryGroups.Add(family,buttons);
                for(int i=0;i<count;i++)
                {
                    int option=i;var selectedFamily=family;
                    string label=family==RecipeFamily.Food?new[]{"All","H","S","E"}[i]:i==0?"All":i.ToString();
                    string tip=family==RecipeFamily.Equipment?((EquipmentCategory)i).ToString():family==RecipeFamily.Mead?(i==0?"All":UiStyle.MeadLabels((MeadFlags)(1<<(i-1)))):UiStyle.FoodLabel(i,((FoodCategory)i).ToString());
                    buttons[i]=MakeButton(gui,rect,label,"Type: "+tip,()=>
                    {
                        if(selectedFamily==RecipeFamily.Equipment)_state.EquipmentCategory=(EquipmentCategory)option;
                        else if(selectedFamily==RecipeFamily.Mead)_state.MeadCategory=(MeadCategory)option;
                        else _state.Category=(FoodCategory)option;
                        Labels();_changed(true);
                    },geometry.Category(i,family));
                }
                if(family==RecipeFamily.Equipment)EquipmentCategoryIcons.Attach(buttons);
                else if(family==RecipeFamily.Mead)MeadCategoryIcons.Attach(buttons);
            }
            for(int i=0;i<2;i++)
            {
                int option=i;
                _sorts[i]=MakeButton(gui,rect,i==0?"=":"A-Z",i==0?"Sort: Game ordering":"Sort: Localised name A-Z",()=>
                {_state.EquipmentSort=(EquipmentSort)option;Labels();_changed(true);},geometry.Sort(i));
            }
            _biomeToggle=MakeButton(gui,rect,"Biomes >","Expand biome filters",TogglePalette,geometry.Disclosure);
        }
        private void ApplyInventoryLayout()
        {
            using var profiling=InventoryOpeningProfiler.Measure(OpeningPhase.ToolbarLayout);
            var geometry=InventoryGeometry;bool expanded=_state.Disclosure.Expanded;
            _types=_inventoryGroups.TryGetValue(_state.Family,out var group)?group:Array.Empty<Button>();
            Place((RectTransform)_filters!.transform,geometry.Toggle);
            Place((RectTransform)Search.transform,geometry.Search);Search.gameObject.SetActive(expanded);
            Place((RectTransform)_clear.transform,geometry.Clear);_clear.gameObject.SetActive(expanded);
            Place((RectTransform)_faves.transform,geometry.Favourites);_faves.gameObject.SetActive(expanded);
            for(int i=0;i<_sorts.Length;i++){Place((RectTransform)_sorts[i].transform,geometry.Sort(i));_sorts[i].gameObject.SetActive(expanded);}
            for(int i=0;i<_families.Length;i++){Place((RectTransform)_families[i].transform,geometry.Family(i));_families[i].gameObject.SetActive(expanded);}
            foreach(var pair in _inventoryGroups)
                for(int i=0;i<pair.Value.Length;i++)
                {Place((RectTransform)pair.Value[i].transform,geometry.Category(i,pair.Key));pair.Value[i].gameObject.SetActive(expanded && pair.Key==_state.Family);}
            bool biomes=expanded && InventoryRules.HasBiomes(_state.Family);
            _biomeToggle!.gameObject.SetActive(biomes);Place((RectTransform)_biomeToggle!.transform,geometry.Disclosure);
            if(_allBiomes){_allBiomes!.gameObject.SetActive(biomes);Place((RectTransform)_allBiomes.transform,geometry.Reset);}
            for(int i=0;i<_palette.Length;i++)if(_palette[i])Place((RectTransform)_palette[i].transform,geometry.Biome(i));
            var events=UnityEngine.EventSystems.EventSystem.current;
            var selected=events?events.currentSelectedGameObject:null;
            if(events && selected && Contains(selected) && !selected!.activeInHierarchy)events.SetSelectedGameObject(FocusTarget);
        }
        private void WireInventoryNavigation()
        {
            var rows=new List<List<Selectable>>{new List<Selectable>{_filters!}};
            if(_state.Disclosure.Expanded)
            {
                rows[0].AddRange(_sorts);
                rows.Add(new List<Selectable>{Search,_clear,_faves});rows.Add(new List<Selectable>(_families));
                int columns=_state.Mead?3:4;
                for(int i=0;i<_types.Length;i+=columns)
                {var row=new List<Selectable>();for(int j=i;j<Math.Min(i+columns,_types.Length);j++)row.Add(_types[j]);rows.Add(row);}
                if(InventoryRules.HasBiomes(_state.Family))rows.Add(new List<Selectable>{_biomeToggle!,_allBiomes!});
                if(PaletteExpanded)
                    for(int i=0;i<_visibleOptions;i+=ToolbarGeometry.Columns)
                    {var row=new List<Selectable>();for(int j=i;j<Math.Min(i+ToolbarGeometry.Columns,_visibleOptions);j++)if(_palette[j].interactable)row.Add(_palette[j]);if(row.Count>0)rows.Add(row);}
            }
            WireRows(rows.ToArray());
        }
        internal void Bind(QueryState state)
        {
            using var profiling=InventoryOpeningProfiler.Measure(OpeningPhase.ToolbarBind);
            _state = state;
            if(state.Mead || state.Inventory && !InventoryRules.HasBiomes(state.Family))state.Biome=BiomeChoice.All;
            Search.SetTextWithoutNotify(state.Search);
            if(state.Inventory)ApplyInventoryLayout();
            if(_state.Inventory)ApplyInventoryLayout();
            ApplyPaletteVisibility(); Labels(); WireNavigation();
            if (_visible) ApplyReservation(Height);
        }
        internal bool Contains(GameObject? selected) => _root && selected && selected.transform.IsChildOf(_root.transform);
        internal bool Editing => Search && (Search.isFocused || (UnityEngine.EventSystems.EventSystem.current &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == Search.gameObject && GuiInputField.VirtualKeyboardOpen));
        internal void SetBiomes(IReadOnlyList<RecipeInfo> infos, int discovered)
        {
            if(_state.Mead || _state.Inventory && !InventoryRules.HasBiomes(_state.Family))return;
            int available = 0; bool unknown = false;
            foreach (RecipeInfo info in infos) { available |= info.Biome.Mask; unknown |= info.Biome.Mask == 0; }
            if (_paletteReady && available == _available && unknown == _unknown && discovered == _discovered &&
                BiomeAccess.ValidSelection(_state.Biome, discovered, available, unknown) == _state.Biome) return;
            _available = available; _unknown = unknown; _discovered = discovered;
            RebuildPalette();
        }
        internal void InvalidatePaletteLabels() => _paletteReady = false;
        internal void SetDiscovery(int discovered)
        {
            if(_state.Mead || _state.Inventory && !InventoryRules.HasBiomes(_state.Family))return;
            if (_paletteReady && _discovered == discovered) return;
            _discovered = discovered; RebuildPalette();
        }
        private void ChooseBiome(int slot)
        {
            if (slot >= _visibleOptions || !_palette[slot].interactable ||
                !BiomeAccess.CanSelect(_choices[slot], _discovered, _available, _unknown)) return;
            BiomeChoice choice = _choices[slot] == _state.Biome ? BiomeChoice.All : _choices[slot];
            if (choice == _state.Biome) return;
            _state.Biome = choice; Labels(); _changed(true);
        }
        private void RebuildPalette()
        {
            BiomeChoice? focusedChoice = null;
            if (UnityEngine.EventSystems.EventSystem.current)
                for (int i = 0; i < _visibleOptions; i++)
                    if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == _palette[i].gameObject)
                    { focusedChoice = _choices[i]; break; }
            _paletteReady = true;
            List<BiomeOption> options = BiomeAccess.Options(_discovered, _available, _unknown);
            // All/Misc are neutral controls. Only confirmed biome identities may reach name/tooltip lookup.
            options.RemoveAll(option => option.Choice == BiomeChoice.All ||
                option.Choice != BiomeChoice.Unknown && !BiomeAccess.IsDiscovered(option.Choice, _discovered));
            _visibleOptions = options.Count;
            for (int i = 0; i < _palette.Length; i++)
            {
                Button button = _palette[i];
                _choices[i] = BiomeChoice.All;
                var tip = button.GetComponent<UITooltip>();
                tip.enabled = false; tip.m_text = tip.m_topic = "";
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true); text.text = "";
                button.interactable = false;
                UiStyle.Selected(button, false, Color.white);
                if (i < options.Count)
                {
                    BiomeOption option = options[i]; _choices[i] = option.Choice;
                    button.interactable = option.Enabled;
                    // Only confirmed discoveries enter options. Inactive slots contain no future identity.
                    tip.m_text = BiomeDiscovery.Name(option.Choice) + (option.Enabled ? "" : " - no available recipes");
                    text.text = option.Choice == BiomeChoice.Unknown ? "?" : "";
                }
                button.gameObject.SetActive(PaletteExpanded && i < options.Count);
                tip.enabled = i < options.Count;
            }
            BiomeChoice valid = BiomeAccess.ValidSelection(_state.Biome, _discovered, _available, _unknown);
            bool reset = valid != _state.Biome; _state.Biome = valid;
            Labels(); WireNavigation();
            if (PaletteExpanded && focusedChoice.HasValue && UnityEngine.EventSystems.EventSystem.current)
            {
                GameObject target = FocusTarget;
                for (int i = 0; i < options.Count; i++)
                    if (_choices[i] == focusedChoice.Value && _palette[i].interactable) { target = _palette[i].gameObject; break; }
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(target);
            }
            if (reset) _changed(true);
        }
        private bool PaletteExpanded => !_state.Mead && _state.BiomesExpanded && (!_state.Inventory || _state.Disclosure.Expanded && InventoryRules.HasBiomes(_state.Family));
        private void TogglePalette()
        {
            if (_state.Mead || _state.Inventory && !InventoryRules.HasBiomes(_state.Family)) return;
            _state.BiomesExpanded = !_state.BiomesExpanded;
            ApplyPaletteVisibility(); Labels(); WireNavigation();
            if (_visible)
            {
                ApplyReservation(Height);
                EnsureSelectionVisible();
            }
            // Presentation only: no changed callback, filtering, allocation of controls or panel rebuild.
        }
        private void EnsureSelectionVisible()
        {
            object selected=GameContracts.SelectedPair.GetValue(_gui);
            var element=(GameObject)GameContracts.PairElement.GetValue(selected,null);
            if(element)EnsureVisible(element,false);
        }
        private void ApplyPaletteVisibility()
        {
            if (!PaletteExpanded && UnityEngine.EventSystems.EventSystem.current)
                for (int i = 0; i < _visibleOptions; i++)
                    if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == _palette[i].gameObject)
                        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_biomeToggle ? _biomeToggle!.gameObject : FocusTarget);
            for (int i = 0; i < _palette.Length; i++)
                if (_palette[i]) _palette[i].gameObject.SetActive(PaletteExpanded && i < _visibleOptions);
            if (_root) ((RectTransform)_root.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Height);
            if (_separator)
            {
                float y = _state.Inventory?InventoryGeometry.SeparatorY:_state.Mead ? _equipmentGeometry.SeparatorY(false) : _state.Equipment ? _equipmentGeometry.SeparatorY(PaletteExpanded) : ToolbarGeometry.FoodSeparatorY(PaletteExpanded);
                _separator.anchoredPosition = new Vector2(_separator.anchoredPosition.x, -y);
            }
        }
        private void ApplyReservation(float next)
        {
            Vector2 previous = new Vector2(0, _reservedHeight), desired = new Vector2(0, next);
            if (_viewport && (_reservedHeight == 0 || _viewport.offsetMax == _viewportTop - previous))
                _viewport.offsetMax = _viewportTop - desired;
            if (_scrollbar && (_reservedHeight == 0 || _scrollbar.offsetMax == _scrollbarTop - previous))
                _scrollbar.offsetMax = _scrollbarTop - desired;
            if (_gui && (_reservedHeight == 0 || (float)GameContracts.BaseHeight.GetValue(_gui) == Mathf.Max(0, _baseHeight - _reservedHeight)))
            {
                float baseHeight = Mathf.Max(0, _baseHeight - next);
                GameContracts.BaseHeight.SetValue(_gui, baseHeight);
                if (_gui.m_recipeListRoot)
                {
                    int count = ((System.Collections.IList)GameContracts.Pairs.GetValue(_gui)).Count;
                    _gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Max(baseHeight, count * _gui.m_recipeListSpace));
                }
            }
            _reservedHeight = next;
        }
        internal void Show(bool show)
        {
            if (_disposed || _visible == show) return;
            _visible = show;
            if(_state.Inventory && show)ApplyInventoryLayout();
            if (!show && Search) Search.DeactivateInputField();
            ApplyReservation(show ? Height : 0);
            if (_root) _root.SetActive(show);
            if (!show && _empty) _empty.SetActive(false);
        }
        internal void SetInteraction(bool active)
        {
            if (!_root) return;
            var group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = active;
            if (!active && Search) Search.DeactivateInputField();
        }
        internal void ShowEmpty(bool empty) { if (_empty) _empty.SetActive(_visible && empty); }
        internal void EnsureVisible(GameObject element, bool force)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, element.transform);
            if (force || bounds.min.y < _viewport.rect.yMin || bounds.max.y > _viewport.rect.yMax)
                _gui.m_recipeEnsureVisible.CenterOnItem((RectTransform)element.transform);
        }
        private void Labels()
        {
            for (int i = 0; i < _types.Length; i++)
                UiStyle.Selected(_types[i], (_state.Equipment ? (int)_state.EquipmentCategory : _state.Mead ? (int)_state.MeadCategory : (int)_state.Category) == i,
                    _state.Equipment || _state.Mead ? Color.white : UiStyle.FoodColour(i));
            for (int i = 0; i < _sorts.Length; i++)
                UiStyle.Selected(_sorts[i], (_state.Inventory || _state.Equipment || _state.Mead ? (int)_state.EquipmentSort : (int)_state.Sort) == i,
                    _state.Inventory || _state.Equipment || _state.Mead ? Color.white : UiStyle.FoodColour(i));
            if (_state.Inventory)
            {
                for(int i=0;i<_families.Length;i++)UiStyle.Selected(_families[i],(int)_state.Family==i,Color.white);
                _filters!.GetComponentInChildren<TMP_Text>(true).text="Filters"+(InventoryRules.Active(_state)?"*":"")+(_state.Disclosure.Expanded?" v":" >");
            }
            if (_biomeToggle)
            {
                _biomeToggle!.GetComponentInChildren<TMP_Text>(true).text = PaletteExpanded ? "Biomes v" : "Biomes >";
                _biomeToggle.GetComponent<UITooltip>().Set("", PaletteExpanded ? "Collapse biome filters" : "Expand biome filters");
                UiStyle.Selected(_biomeToggle, PaletteExpanded, Color.white);
            }
            for (int i = 0; i < _palette.Length; i++)
                if (_palette[i])
                {
                    Color swatch = i < _visibleOptions && _palette[i].interactable ? UiStyle.Swatch(BiomeDiscovery.Colour(_choices[i])) : UiStyle.Neutral;
                    UiStyle.Selected(_palette[i], i < _visibleOptions && _choices[i] == _state.Biome, Color.white, swatch);
                }
            UiStyle.Selected(_faves, _state.FavouritesOnly, Color.white);
            _filterStar.SetFilled(_state.FavouritesOnly);
            _filterStar.color = UiStyle.StarColour(_state.FavouritesOnly);
            _faves.GetComponent<UITooltip>().Set("", _state.FavouritesOnly ? "Include non-favourites" : "Show favourites only");
            if(_allBiomes)UiStyle.Selected(_allBiomes!, _state.Biome == BiomeChoice.All, Color.white);
        }
        private static void Place(RectTransform rect, UiBox box) => Place(rect, box.X, box.Y, box.Width, box.Height);
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        private static Button MakeButton(InventoryGui gui, Transform parent, string label, string tooltip, Action action, UiBox box)
            => MakeButton(gui, parent, label, tooltip, action, box.X, box.Y, box.Width, box.Height);
        internal static Button MakeButton(InventoryGui gui, Transform parent, string label, string tooltip, Action action,
            float x, float y, float width, float height = 28)
        {
            Button button = UnityEngine.Object.Instantiate(gui.m_tabCraft, parent, false);
            button.name = "CraftyQoL." + (label.Length == 0 ? tooltip : label);
            button.onClick = new Button.ButtonClickedEvent();
            ButtonTextColor inheritedTint = button.GetComponent<ButtonTextColor>();
            if (inheritedTint) { inheritedTint.enabled = false; UnityEngine.Object.Destroy(inheritedTint); }
            Transform inheritedSelected = button.transform.Find("Selected");
            if (inheritedSelected) inheritedSelected.gameObject.SetActive(false);
            Transform inheritedHint = button.transform.Find("gamepad_hint");
            if (inheritedHint) inheritedHint.gameObject.SetActive(false);
            foreach (UIGamePad shortcut in button.GetComponentsInChildren<UIGamePad>(true))
            { shortcut.enabled = false; UnityEngine.Object.Destroy(shortcut); }
            foreach (LayoutElement layout in button.GetComponents<LayoutElement>()) layout.ignoreLayout = true;
            button.onClick.AddListener(() => action());
            button.interactable = true;
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center; text.margin = Vector4.zero;
            UiStyle.ButtonText(gui, text, height);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(UiStyle.TextPaddingX, UiStyle.TextPaddingY); textRect.offsetMax = new Vector2(-UiStyle.TextPaddingX, -UiStyle.TextPaddingY);
            UITooltip tip = UiStyle.Tooltip(gui, button.gameObject);
            tip.m_topic = ""; tip.m_text = tooltip; tip.m_gamepadFocusObject = null;
            Place((RectTransform)button.transform, x, y, width, height);
            UiStyle.ButtonSkin(gui.m_stackAllButton, button, height);
            UiStyle.Round(button.GetComponent<Image>());
            UiStyle.AddSelectionMarker(button);
            return button;
        }
        private void Caption(Transform parent, string label, UiBox box)
        {
            var go = new GameObject("CraftyQoL.Caption", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = Search.textComponent.font; text.fontSize = UiStyle.CaptionSize; text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.MidlineLeft; text.margin = Vector4.zero; text.text = label;
            text.raycastTarget = false; text.color = new Color(0.85f, 0.85f, 0.8f);
            Place(text.rectTransform, box);
        }
        private void WireNavigation()
        {
            var rows = new List<List<Selectable>> { new List<Selectable> { Search, _clear, _faves } };
            if(_state.Inventory){WireInventoryNavigation();return;}
            if(_state.Mead)
            {
                rows.Add(new List<Selectable>{_types[0],_types[1],_types[2]});
                rows.Add(new List<Selectable>{_types[3],_types[4],_types[5]});
                rows.Add(new List<Selectable>{_sorts[0],_sorts[1]});
            }
            else if (_state.Equipment)
            {
                rows.Add(new List<Selectable> { _types[0], _types[1], _types[2], _types[3] });
                rows.Add(new List<Selectable> { _types[4], _types[5], _types[6], _types[7] });
                rows.Add(new List<Selectable> { _sorts[0], _sorts[1], _biomeToggle!, _allBiomes! });
            }
            else
            {
                var groups = new List<Selectable>(); groups.AddRange(_types); groups.AddRange(_sorts);
                rows.Add(groups); rows.Add(new List<Selectable> { _biomeToggle!, _allBiomes! });
            }
            var first = new List<Selectable>(); var second = new List<Selectable>();
            if (PaletteExpanded)
                for (int i = 0; i < _visibleOptions; i++)
                    if (_palette[i].interactable) (i < ToolbarGeometry.Columns ? first : second).Add(_palette[i]);
            if (first.Count > 0) rows.Add(first);
            if (second.Count > 0) rows.Add(second);
            WireRows(rows.ToArray());
        }
        private static void WireRows(params List<Selectable>[] rows)
        {
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Count; c++)
                {
                    List<Selectable> row = rows[r], above = rows[(r + rows.Length - 1) % rows.Length], below = rows[(r + 1) % rows.Length];
                    int Map(int count) => row.Count <= 1 ? 0 : Mathf.RoundToInt(c * (count - 1f) / (row.Count - 1));
                    row[c].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                        selectOnLeft = row[(c + row.Count - 1) % row.Count], selectOnRight = row[(c + 1) % row.Count],
                        selectOnUp = above[Map(above.Count)], selectOnDown = below[Map(below.Count)] };
                }
        }
        public void Dispose()
        {
            if (_disposed) return;
            try { Show(false); }
            finally
            {
                _disposed = true;
                if (_root) UnityEngine.Object.Destroy(_root);
                if (_empty) UnityEngine.Object.Destroy(_empty);
            }
        }
    }
}
