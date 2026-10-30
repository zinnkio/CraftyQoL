using System;
using System.Collections.Generic;
using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal sealed class RecipeMetadata
    {
        private ObjectDB? _db;
        private ZNetScene? _scene;
        private string _language = "";
        private int _recipeCount, _prefabCount;
        private FoodGraph _graph = new FoodGraph();
        private MeadGraph _meads = new MeadGraph();
        private bool _meadReady,_inventory;
        private readonly Dictionary<Recipe, RecipeInfo> _recipes = new Dictionary<Recipe, RecipeInfo>();
        private readonly Dictionary<string, int> _keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal void Invalidate()
        {
            InventoryOpeningProfiler.Note(OpeningCapture.Event.MetadataInvalidated);
            _db = null; _scene = null; _recipes.Clear(); _keyCounts.Clear(); _graph = new FoodGraph(); _meads = new MeadGraph();
        }
        internal void Ensure(bool includeMeads=false,bool inventory=false)
        {
            using var profiling=InventoryOpeningProfiler.Measure(OpeningPhase.MetadataEnsure);
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            if (!db || !scene) throw new InvalidOperationException("Recipe metadata context is unavailable.");
            string language = Localization.instance != null ? Localization.instance.GetSelectedLanguage() : "";
            if (_language != language) { _recipes.Clear(); _language = language; }
            if (_db == db && _scene == scene && _recipeCount == db.m_recipes.Count && _prefabCount == scene.m_prefabs.Count && (!includeMeads || _meadReady) && _inventory==inventory) return;
            InventoryOpeningProfiler.Note(OpeningCapture.Event.MetadataRebuild);
            _db = db; _scene = scene; _meadReady=includeMeads; _inventory=inventory;
            _recipeCount = db.m_recipes.Count; _prefabCount = scene.m_prefabs.Count;
            _recipes.Clear(); _keyCounts.Clear(); _graph = new FoodGraph(); _meads = new MeadGraph();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (!prefab) continue;
                ItemDrop item = prefab.GetComponent<ItemDrop>();
                if (item && item.m_itemData?.m_shared != null && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable)
                {
                    var shared = item.m_itemData.m_shared;
                    _graph.AddEdible(prefab.name, new FoodStats(shared.m_food, shared.m_foodStamina, shared.m_foodEitr));
                    if(includeMeads && (shared.m_consumeStatusEffect || shared.m_food>0 || shared.m_foodStamina>0 || shared.m_foodEitr>0))
                        _meads.Add(prefab.name, MeadMetadata.Effects(shared));
                }
                Fermenter fermenter=includeMeads?prefab.GetComponent<Fermenter>():null!;
                if(fermenter && fermenter.m_conversion!=null)
                    foreach(var conversion in fermenter.m_conversion)
                        if(conversion!=null && conversion.m_from && conversion.m_to)
                            _meads.Convert(conversion.m_from.gameObject.name,conversion.m_to.gameObject.name);
                CookingStation cooking = prefab.GetComponent<CookingStation>();
                if (cooking && cooking.m_conversion != null)
                    foreach (CookingStation.ItemConversion conversion in cooking.m_conversion)
                        if (conversion != null && conversion.m_from && conversion.m_to)
                            _graph.AddFinish(conversion.m_from.gameObject.name, conversion.m_to.gameObject.name);
                Feast feast = prefab.GetComponent<Feast>();
                Piece piece = prefab.GetComponent<Piece>();
                if (!feast || !piece || piece.m_resources == null || piece.m_resources.Length != 1) continue;
                Piece.Requirement requirement = piece.m_resources[0];
                ItemDrop? food = feast.m_foodItem ? feast.m_foodItem : item;
                if (food != null && food && requirement != null && requirement.m_resItem && requirement.m_amount == 1)
                    _graph.AddFinish(requirement.m_resItem.gameObject.name, food.gameObject.name, feast: true);
            }
            var seen = new HashSet<Recipe>();
            foreach (Recipe recipe in db.m_recipes)
            {
                if (!recipe || !recipe.m_item || !seen.Add(recipe)) continue;
                string key = Key(recipe);
                _keyCounts.TryGetValue(key, out int count); _keyCounts[key] = count + 1;
            }
            Plugin.Instance?.MigrateLoadedRecipes();
        }
        internal RecipeInfo Get(Recipe recipe)
        {
            if (_recipes.TryGetValue(recipe, out RecipeInfo cached)) return cached;
            InventoryOpeningProfiler.Note(OpeningCapture.Event.MetadataMiss);
            if (!recipe || !recipe.m_item || recipe.m_item.m_itemData?.m_shared == null)
                throw new InvalidOperationException("Recipe output metadata is invalid.");
            string output = recipe.m_item.gameObject.name;
            string token = recipe.m_item.m_itemData.m_shared.m_name;
            string name = Localization.instance != null ? Localization.instance.Localize(token) : token;
            var ingredients = new List<string>();
            if (recipe.m_resources != null)
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    // Supported benches craft base items, not upgrades. Quantities do not weight provenance.
                    if (requirement != null && (requirement.m_amount <= 0 || requirement.m_upgraderResource)) continue;
                    ingredients.Add(requirement != null && requirement.m_resItem ? requirement.m_resItem.gameObject.name : "~unknown");
                }
            FoodResolution food = _graph.Resolve(output);
            string key = Key(recipe);
            BiomeAssociation? explicitOverride = Plugin.Instance != null && Plugin.Instance.BiomeOverrides.TryGetValue(key, out BiomeAssociation chosen)
                ? chosen : (BiomeAssociation?)null;
            bool equipmentRecipe = !recipe.m_craftingStation || StationRules.IsEquipment(recipe.m_craftingStation.gameObject.name);
            bool meadRecipe=recipe.m_craftingStation && StationRules.IsMead(recipe.m_craftingStation.gameObject.name);
            var shared = recipe.m_item.m_itemData.m_shared;
            Attack attack = shared.m_attack;
            EquipmentFlags equipment = EquipmentClassifier.Classify(output, (int)shared.m_itemType, (int)shared.m_skillType,
                shared.m_damages.m_chop, shared.m_damages.m_pickaxe,
                attack != null && attack.m_attackType == Attack.AttackType.Projectile && attack.m_consumeItem, shared.m_eitrRegenModifier);
            bool foodStation=recipe.m_craftingStation && StationRules.IsSupported(recipe.m_craftingStation.gameObject.name) && !equipmentRecipe && !meadRecipe;
            RecipeFamily family=InventoryRules.Family(foodStation || food.Source==NutritionSource.Cooked || food.Source==NutritionSource.Feast,
                meadRecipe,_meads.Ferments(output),shared.m_itemType==ItemDrop.ItemData.ItemType.Consumable && shared.m_consumeStatusEffect && food.Stats.Categories==FoodFlags.None,food.Stats,equipment,InventoryRules.EquipmentType((int)shared.m_itemType));
            bool equipmentBiome=_inventory?family!=RecipeFamily.Food:equipmentRecipe || meadRecipe;
            BiomeAssociation biome=equipmentBiome?BiomeCatalogue.ClassifyEquipment(ingredients,recipe.m_requireOnlyOneIngredient,explicitOverride):
                BiomeCatalogue.Classify(output,ingredients,recipe.m_requireOnlyOneIngredient,explicitOverride);
            var info = new RecipeInfo(key, name ?? token, output, food.Stats, food.Source,
                biome,
                _keyCounts.TryGetValue(key, out int count) && count == 1, equipment, (meadRecipe || _inventory && family==RecipeFamily.Mead)?_meads.Resolve(output):MeadFlags.Other,family);
            _recipes.Add(recipe, info);
            return info;
        }
        internal static string Key(Recipe recipe) => RecipeInfo.StableKey(
            recipe.m_craftingStation ? recipe.m_craftingStation.gameObject.name : "", recipe.name ?? "", recipe.m_item.gameObject.name);

    }
}
