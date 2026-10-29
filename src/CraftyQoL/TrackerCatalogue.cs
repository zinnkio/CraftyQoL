using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal sealed class TrackerOption
    {
        internal readonly ItemDrop Item;
        internal readonly int Amount, Quality, Output;
        internal TrackerOption(ItemDrop item, int amount, int quality, int output) { Item = item; Amount = amount; Quality = quality; Output = output; }
    }
    internal sealed class TrackerNote
    {
        internal readonly TrackerPin Pin;
        internal readonly TrackerPlan? Plan;
        internal readonly Sprite? Icon;
        internal readonly List<TrackerOption> Options;
        internal TrackerNote(TrackerPin pin, TrackerPlan? plan, Sprite? icon, List<TrackerOption>? options = null)
        { Pin = pin; Plan = plan; Icon = icon; Options = options ?? new List<TrackerOption>(); }
    }
    internal sealed class TrackerCatalogue
    {
        private static readonly Func<Player,string,int,bool>? StationKnown = BindStationKnown();
        private static Func<Player,string,int,bool>? BindStationKnown()
        {
            try
            {
                MethodInfo? method=typeof(Player).GetMethod("KnowStationLevel",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(int)},null);
                if(method!=null && method.ReturnType==typeof(bool))
                    return (Func<Player,string,int,bool>)Delegate.CreateDelegate(typeof(Func<Player,string,int,bool>),method);
            }
            catch(Exception error) when (error is ArgumentException || error is MemberAccessException || error is NotSupportedException || error is System.Security.SecurityException) { }
            Debug.LogWarning("CraftyQoL tracker: station-knowledge API unavailable; recipe pins fail closed.");return null;
        }
        private ObjectDB? _db;
        private int _count,_preparedFrame=-1;
        private Player? _preparedPlayer;
        private CraftAccess _preparedAccess;
        private bool _preparedInventory;
        private List<Recipe> _available=new List<Recipe>();
        private readonly HashSet<Recipe> _creativeAvailable=new HashSet<Recipe>();
        private readonly Dictionary<string, Recipe?> _recipes = new Dictionary<string, Recipe?>(StringComparer.Ordinal);
        internal readonly Dictionary<string, ItemDrop> Items = new Dictionary<string, ItemDrop>(StringComparer.Ordinal);
        internal void Invalidate() { _db = null; _recipes.Clear(); Items.Clear();_creativeAvailable.Clear();_preparedPlayer=null;_preparedFrame=-1; }
        private void Ensure()
        {
            if (!ObjectDB.instance) throw new InvalidOperationException("Tracker recipe database unavailable.");
            if (_db == ObjectDB.instance && _count == _db.m_recipes.Count) return;
            _db = ObjectDB.instance; _count = _db.m_recipes.Count; _recipes.Clear(); Items.Clear();_creativeAvailable.Clear();_preparedPlayer=null;_preparedFrame=-1;
            var seen = new HashSet<Recipe>();
            foreach (Recipe recipe in _db.m_recipes)
            {
                if (!recipe || !recipe.m_item || !seen.Add(recipe)) continue;
                string key = RecipeMetadata.Key(recipe);
                if (_recipes.ContainsKey(key)) _recipes[key] = null; else _recipes.Add(key, recipe);
            }
        }
        internal void Prepare(Player player,bool inventoryPins=true)
        {
            Ensure();var access=InventoryCrafting.Access;
            if(_preparedFrame==Time.frameCount && _preparedPlayer==player && _preparedAccess==access && (!inventoryPins || _preparedInventory))return;
            _preparedFrame=Time.frameCount;_preparedPlayer=player;_preparedAccess=access;_preparedInventory=inventoryPins;
            _creativeAvailable.Clear();
            if(!inventoryPins || !InventoryRules.CreativeKnowledge(access))return;
            // One native snapshot per model refresh/frame, never a query for every pin.
            player.GetAvailableRecipes(ref _available);
            foreach(var recipe in _available)if(recipe && !recipe.m_noCraftOnlyUpgrade)_creativeAvailable.Add(recipe);
        }
        private bool Creative(Recipe recipe,string origin)=>InventoryRules.CreativePin(origin==InventoryRules.Origin,_preparedAccess,_creativeAvailable.Contains(recipe));
        internal static string Fingerprint(Recipe recipe)
        {
            if (recipe.m_resources == null || recipe.m_resources.Length > TrackerMath.MaxNeeds) throw new InvalidDataException("Unsupported resource list.");
            var text = new StringBuilder(RecipeMetadata.Key(recipe));
            text.Append('|').Append(recipe.m_amount).Append('|').Append(recipe.m_requireOnlyOneIngredient).Append('|')
                .Append(recipe.m_qualityResultAmountMultiplier.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(recipe.m_minStationLevel);
            foreach (var r in recipe.m_resources)
            {
                if (r == null || !r.m_resItem) throw new InvalidDataException("Missing resource.");
                text.Append('|').Append(Uri.EscapeDataString(r.m_resItem.gameObject.name)).Append(':').Append(r.GetAmount(1))
                    .Append(':').Append(r.m_upgraderResource).Append(':').Append(r.m_extraAmountOnlyOneIngredient);
            }
            return TrackerMath.Signature(text.ToString());
        }
        private bool Known(Player player,Recipe recipe,string origin)
            => recipe && recipe.m_item && !recipe.m_noCraftOnlyUpgrade && (Creative(recipe,origin) ||
               recipe.m_enabled && (!recipe.m_craftingStation || origin==InventoryRules.Origin || StationRules.IsSupported(recipe.m_craftingStation.gameObject.name)) &&
               player.IsRecipeKnown(recipe.m_item.m_itemData.m_shared.m_name) &&
               (!recipe.m_craftingStation || (StationKnown != null && StationKnown(player,recipe.m_craftingStation.m_name,recipe.m_minStationLevel))) &&
               (recipe.m_item.m_itemData.m_shared.m_dlc.Length == 0 || DLCMan.instance.IsDLCInstalled(recipe.m_item.m_itemData.m_shared.m_dlc)));
        private static bool ValidOrigin(Recipe recipe,string origin)
            => origin==InventoryRules.Origin || StationRules.IsSupported(origin) && (recipe.m_craftingStation ? recipe.m_craftingStation.gameObject.name==origin : origin=="piece_workbench");
        private static bool KnownItem(Player player, ItemDrop item) => item && player.IsKnownMaterial(item.m_itemData.m_shared.m_name);
        private static string Name(ItemDrop item) => Localization.instance.Localize(item.m_itemData.m_shared.m_name);
        private List<TrackerOption> Options(Player player, Recipe recipe,bool creative)
        {
            var result = new List<TrackerOption>();
            foreach (var r in recipe.m_resources)
            {
                if (r.m_upgraderResource || r.GetAmount(1) <= 0 || !creative && !KnownItem(player, r.m_resItem)) continue;
                for (int q = 1; q <= Math.Min(1000, r.m_resItem.m_itemData.m_shared.m_maxQuality); q++)
                {
                    if (result.Count >= 256) throw new InvalidDataException("Too many alternative options.");
                    result.Add(new TrackerOption(r.m_resItem, r.GetAmount(1), q,
                        TrackerMath.Output(recipe.m_amount, q, recipe.m_qualityResultAmountMultiplier, r.m_extraAmountOnlyOneIngredient)));
                }
            }
            return result;
        }
        internal TrackerPin? Create(Player player, Recipe recipe, string origin,bool inventoryAvailable=false)
        {
            if(origin==InventoryRules.Origin)Prepare(player);else Ensure();
            string key = RecipeMetadata.Key(recipe);
            if(origin==InventoryRules.Origin && !inventoryAvailable)return null;
            if (!_recipes.TryGetValue(key, out Recipe? unique) || unique != recipe || !Known(player, recipe,origin) || !ValidOrigin(recipe, origin)) return null;
            var pin = new TrackerPin { Key = key, Signature = Fingerprint(recipe), Station = origin };
            if (recipe.m_requireOnlyOneIngredient)
            {
                var options = Options(player, recipe,Creative(recipe,origin)); if (options.Count == 0) return null;
                TrackerOption choice = options[0];
                foreach (var candidate in options)
                    if (player.GetInventory().CountItems(candidate.Item.m_itemData.m_shared.m_name, candidate.Quality) >= candidate.Amount) { choice = candidate; break; }
                pin.Alternative = choice.Item.gameObject.name; pin.Quality = choice.Quality;
            }
            return Resolve(player, pin).Plan != null ? pin : null;
        }
        internal TrackerNote Resolve(Player player, TrackerPin pin)
        {
            Ensure();
            if (!_recipes.TryGetValue(pin.Key, out Recipe? recipe) || !recipe || !Known(player, recipe!,pin.Station) || !ValidOrigin(recipe!, pin.Station) || Fingerprint(recipe!) != pin.Signature)
                return new TrackerNote(pin, null, null);
            var needs = new List<TrackerNeed>(); List<TrackerOption>? options = null; int output = recipe!.m_amount;
            if (recipe.m_requireOnlyOneIngredient)
            {
                options = Options(player, recipe,Creative(recipe,pin.Station));
                TrackerOption? selected = options.Find(o => o.Item.gameObject.name == pin.Alternative && o.Quality == pin.Quality);
                if (selected == null) return new TrackerNote(pin, null, null);
                Add(selected.Item, selected.Amount, selected.Quality); output = selected.Output;
            }
            else
                foreach (var r in recipe.m_resources)
                {
                    int amount = r.GetAmount(1); if (r.m_upgraderResource || amount <= 0) continue;
                    if (!Creative(recipe,pin.Station) && !KnownItem(player, r.m_resItem)) return new TrackerNote(pin, null, null);
                    Add(r.m_resItem, amount, -1);
                }
            if (output <= 0 || output > TrackerMath.MaxAmount) return new TrackerNote(pin, null, null);
            return new TrackerNote(pin, new TrackerPlan(pin.Key, Name(recipe.m_item), output, needs.ToArray()), recipe.m_item.m_itemData.GetIcon(), options);
            void Add(ItemDrop item, int amount, int quality)
            {
                if (amount > TrackerMath.MaxAmount) throw new InvalidDataException("Unsupported ingredient amount.");
                string id = item.gameObject.name; Items[id] = item;
                needs.Add(new TrackerNeed(id, Name(item), amount, quality));
            }
        }
    }
}
