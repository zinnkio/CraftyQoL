using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using CraftyQoL.Core;
using System.IO;

namespace CraftyQoL
{
    [BepInPlugin(Id, Name, Version)]
    [BepInProcess("valheim.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "io.github.zinnkio.craftyqol";
        public const string Name = "CraftyQoL";
        public const string Version = "0.18.3";
        internal static Plugin? Instance { get; private set; }
        internal ConfigEntry<bool> Enabled = null!;
        internal ConfigEntry<bool> LogRefreshes = null!;
        private ConfigEntry<bool> _profileInventoryOpenings = null!;
        internal FavouriteStore Favourites = null!;
        private RecipeTracker? _tracker;
        private StorageSorting? _storage;
        private readonly RecipeMoveContext _recipeMoves=new RecipeMoveContext();
        internal Dictionary<string, BiomeAssociation> BiomeOverrides = null!;
        private float _saveAt;
        private Harmony? _harmony;
        private string? _lastStation;
        private bool _failed,_inventorySessionOpen;
        private readonly Dictionary<string, QueryState> _queries = new Dictionary<string, QueryState>(System.StringComparer.Ordinal);
        private string? _reportedConflict;

        private void Awake()
        {
            if (Application.isBatchMode) return;
            Enabled = Config.Bind("General", "Enabled", true, "Enable native inventory/station Craft navigation, recipe tracker and storage Sort & Stack.");
            LogRefreshes = Config.Bind("Diagnostics", "LogRefreshes", false, "Log list refresh counts/timings for manual performance validation.");
            _profileInventoryOpenings = Config.Bind("Diagnostics", "ProfileInventoryOpenings", false,
                "Temporary opt-in stationless inventory profiling. Requires restart. Writes bounded timing/allocation reports, not recipe/player identities. See docs/CREATIVE_INVENTORY_PROFILING.md.");
            try
            {
                GameContracts.Resolve();
                string preferences = Path.Combine(Paths.ConfigPath, Id);
                Favourites = new FavouriteStore(Path.Combine(preferences, "favourites.json"), message => Logger.LogWarning(message));
                BiomeOverrides = Core.BiomeOverrides.Load(Path.Combine(preferences, "biome-overrides.json"), message => Logger.LogWarning(message));
                Instance = this;
                _harmony = new Harmony(Id);
                _harmony.PatchAll(typeof(Plugin).Assembly);
                _tracker = gameObject.AddComponent<RecipeTracker>(); _tracker.Initialise(preferences);
                _storage=gameObject.AddComponent<StorageSorting>();_storage.Initialise();
                if(_profileInventoryOpenings.Value)gameObject.AddComponent<InventoryOpeningProfiler>().Initialise();
                Logger.LogInfo($"{Name} {Version}: native UI contracts resolved. Runtime validation is required.");
            }
            catch (System.Exception error)
            {
                _harmony?.UnpatchSelf();
                Instance = null;
                Logger.LogError($"CraftyQoL disabled: {error.Message}");
            }
        }

        internal void PanelChanged(InventoryGui gui)
        {
            if (_failed) return;
            try
            {
                if (!InventoryPresentation.Open) { FoodView.Current?.BeginClose(); return; }
                if(!_inventorySessionOpen){_inventorySessionOpen=true;_queries.Remove(InventoryRules.Origin);}
                string? station = Enabled.Value ? StationScope.Identify(gui) : null;
                string? conflict = station != null ? Compatibility.Conflict() : null;
                if (conflict != null)
                {
                    if (_reportedConflict != conflict) Logger.LogWarning("CraftyQoL inactive to avoid overlapping recipe interfaces. " + conflict);
                    _reportedConflict = conflict;
                    station = null;
                }
                if (_lastStation != station)
                {
                    _lastStation = station;
                    Logger.LogDebug(station == null ? "Food station scope inactive." : $"Food station scope: {station}");
                }
                if (station == null) FoodView.Current?.Suspend();
                else
                {
                    if (!_queries.TryGetValue(station, out QueryState state)) _queries.Add(station, state = new QueryState(StationRules.IsEquipment(station),StationRules.IsMead(station),station==InventoryRules.Origin));
                    FoodView.Present(gui, state);
                }
            }
            catch (System.Exception error)
            {
                Fail(error);
            }
        }

        private void OnDestroy()
        {
            try { _storage?.Dispose(); _tracker?.Dispose(); FoodView.Current?.CloseOwned(); FlushPreferences(); }
            finally { _harmony?.UnpatchSelf(); if (Instance == this) Instance = null; }
        }
        internal void MigrateLoadedRecipes()
        {
            var approved=_recipeMoves.Approved();if(approved.Count==0)return;
            if(Favourites.Migrate(approved))FavouriteChanged();
            RecipeMoves.AliasOverrides(BiomeOverrides,approved); // Read-only override file; explicit new key takes priority.
            _tracker?.Migrate(approved);
        }
        internal void FavouriteChanged() => _saveAt = Time.unscaledTime + 0.25f;
        internal void InventoryClosed()=>_inventorySessionOpen=false;
        private void Update()
        {
            if(!InventoryPresentation.Open)InventoryClosed();
            if (Favourites != null && Favourites.Dirty && Time.unscaledTime >= _saveAt) FlushPreferences();
        }
        internal void FlushPreferences()
        {
            if (Favourites == null || !Favourites.Dirty) return;
            Favourites.Save();
            FoodView.Current?.RefreshFavouriteVisuals();
        }
        internal void Fail(System.Exception error)
        {
            if (_failed) return;
            _failed = true;
            try { FoodView.Current?.CloseOwned(); }
            catch (System.Exception cleanup) { Logger.LogWarning($"CraftyQoL cleanup: {cleanup.Message}"); }
            Logger.LogError($"CraftyQoL disabled for this session: {error}");
        }
        internal void DiscoveryUnavailable() => Logger.LogWarning("Biome discovery unavailable; biome options remain hidden.");
        internal void DiagnosticMessage(string message)=>Logger.LogInfo(message);
        internal void ReportRefresh(long count, int input, int visible, double milliseconds)
            => Logger.LogInfo($"Food list refresh {count}: {input} available -> {visible} visible; total metadata/native UI/order {milliseconds:F2} ms.");
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel", new[] { typeof(bool) })]
    internal static class PanelPatch
    {
        private static void Prefix(InventoryGui __instance)
        {
            Plugin.Instance?.PanelChanged(__instance);
            try { if (FoodView.Current && FoodView.Current!.IsFor(__instance)) FoodView.Current.BeforePanel(); }
            catch (System.Exception error) { Plugin.Instance?.Fail(error); }
        }
        private static void Postfix(InventoryGui __instance)
        {
            if (!FoodView.Current || !FoodView.Current!.IsFor(__instance))
            {
                // Vanilla can force Craft after the prefix (e.g. losing no-cost while Upgrade was selected).
                if(InventoryPresentation.Open && StationScope.Identify(__instance)==InventoryRules.Origin)
                {
                    Plugin.Instance?.PanelChanged(__instance);
                    if(FoodView.Current && FoodView.Current!.IsFor(__instance))FoodView.Current.RefreshInventoryAvailability();
                }
                return;
            }
            try { FoodView.Current.AfterPanel(); }
            catch (System.Exception error) { Plugin.Instance?.Fail(error); }
        }
    }
}
