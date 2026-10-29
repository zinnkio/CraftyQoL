using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using CraftyQoL.Core;

internal static class EquipmentTests
{
    private static EquipmentFlags Classify(string id, int type, int skill = 1, double chop = 0, double pick = 0, bool projectile = false, double regen = 0)
        => EquipmentClassifier.Classify(id, type, skill, chop, pick, projectile, regen);
    internal static void Run(Action<string, Action> test)
    {
        test("equipment exact station and tab isolation", () =>
        {
            foreach (string id in new[] { "piece_workbench", "forge", "blackforge", "piece_magetable" })
                Program.Assert(StationRules.IsEquipment(id) && StationRules.IsActive(id, true) && !StationRules.IsActive(id, false));
            foreach (string id in new[] { "blackforge(Clone)", "BlackForge", "blackforge_extra", "piece_magetable(Clone)", "Galdr Table", "PIECE_MAGETABLE", "forge(Clone)", "Forge", "piece_workbench_alt", "", "$piece_workbench" })
                Program.Assert(!StationRules.IsActive(id, true));
            Program.Assert(!StationRules.IsEquipment("piece_cauldron"));
        });
        test("axes overlap weapons/tools and pickaxes exclude weapons", () =>
        {
            Program.Assert(Classify("custom", 3, 7, 30) == (EquipmentFlags.Weapons | EquipmentFlags.Tools));
            Program.Assert(Classify("custom", 14, 12, pick: 30) == EquipmentFlags.Tools);
            Program.Assert(Classify("custom", 14, 106) == EquipmentFlags.Tools);
            Program.Assert(Classify("custom", 19) == EquipmentFlags.Tools);
        });
        test("shield/clothing and magic overlap metadata", () =>
        {
            Program.Assert(Classify("custom", 5, 6) == EquipmentFlags.Shields);
            foreach (int type in new[] { 6, 7, 11, 12, 17 }) Program.Assert(Classify("custom", type) == EquipmentFlags.Armour);
            Program.Assert(Classify("custom", 3, 9) == (EquipmentFlags.Weapons | EquipmentFlags.Magic));
            Program.Assert(Classify("custom", 22, 10) == (EquipmentFlags.Weapons | EquipmentFlags.Magic));
            Program.Assert(Classify("custom", 7, regen: 0.2) == (EquipmentFlags.Armour | EquipmentFlags.Magic));
            Program.Assert(Classify("custom", 18) == EquipmentFlags.Other && Classify("custom", 24) == EquipmentFlags.Other);
        });
        test("ammunition covers both ammo types and consumed projectiles", () =>
        {
            Program.Assert(Classify("custom", 9) == EquipmentFlags.Ammunition && Classify("custom", 23) == EquipmentFlags.Ammunition);
            Program.Assert(Classify("custom", 3, projectile: true) == (EquipmentFlags.Ammunition | EquipmentFlags.Weapons));
            Program.Assert(Classify("Catapult_ammo", 1) == EquipmentFlags.Ammunition);
            Program.Assert(Classify("BombSiege", 1) == EquipmentFlags.Ammunition);
        });
        test("reviewed exceptions and unknown custom items", () =>
        {
            Program.Assert(Classify("KnifeButcher", 3, 2) == EquipmentFlags.Tools);
            Program.Assert(Classify("Tankard", 3) == EquipmentFlags.Other);
            Program.Assert(Classify("TankardOdin", 3) == EquipmentFlags.Other);
            Program.Assert(Classify("TankardAnniversary", 3) == EquipmentFlags.Other);
            Program.Assert(Classify("Magic Sword Pickaxe", 999, 9) == EquipmentFlags.Other);
        });
        test("equipment filters combine independently without mutating input", () =>
        {
            var biome = new BiomeAssociation(BiomeAssociation.Bit(BiomeChoice.BlackForest), BiomeSource.Estimated);
            var a = new RecipeInfo("a", "Hache", "x", default, NutritionSource.Unknown, biome, equipment: EquipmentFlags.Tools | EquipmentFlags.Weapons);
            var b = new RecipeInfo("b", "Hache custom", "y", default, NutritionSource.Unknown, default, equipment: EquipmentFlags.Other);
            var source = new List<RecipeInfo> { b, a }; var fav = new HashSet<string> { "a" };
            var q = new QueryState(true) { EquipmentCategory = EquipmentCategory.Tools, EquipmentSort = EquipmentSort.Alphabetical,
                Search = "HACHE", Biome = BiomeChoice.BlackForest, FavouritesOnly = true, Category = FoodCategory.Eitr, Sort = FoodSort.Eitr };
            Program.Assert(EquipmentQuery.Filter(source, q, fav).SequenceEqual(new[] { a }));
            q.Search = "none"; Program.Assert(EquipmentQuery.Filter(source, q, fav).Count == 0);
            q = new QueryState(true); Program.Assert(EquipmentQuery.Filter(source, q, fav).SequenceEqual(source));
            Program.Assert(source.SequenceEqual(new[] { b, a }));
        });
        test("alphabetic localized names and deterministic equal-name ties", () =>
        {
            var a = new RecipeInfo("a", "Alpha", "x", default, NutritionSource.Unknown, default);
            var z = new RecipeInfo("z", "alpha", "x", default, NutritionSource.Unknown, default);
            var b = new RecipeInfo("b", "Zebra", "x", default, NutritionSource.Unknown, default);
            var rows = new List<RecipeInfo> { b, z, a }; StableOrder.Sort(rows, EquipmentQuery.Compare);
            Program.Assert(rows.SequenceEqual(new[] { a, z, b }));
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sv-SE");
                var last = new RecipeInfo("c", "\u00c5ng", "x", default, NutritionSource.Unknown, default);
                Program.Assert(EquipmentQuery.Compare(b, last) < 0);
            }
            finally { CultureInfo.CurrentCulture = before; }
        });
        test("equipment layout fits widths and safely reserves expanded palette", () =>
        {
            foreach (float width in new[] { 170f, 187f, 256f, 384f })
            {
                var g = new EquipmentToolbarGeometry(width);
                Program.Assert(g.Height(false) < ToolbarGeometry.Height && g.Height(true) > g.Height(false));
                Program.Assert(g.Category(0).Bottom < g.Category(4).Y && g.Category(4).Bottom < g.Sort(0).Y);
                Program.Assert(g.Disclosure.X > g.Sort(1).Right && g.Disclosure.Right < g.Reset.X && g.Disclosure.Width >= 50);
                for (int i = 0; i < 8; i++) Program.Assert(g.Category(i).Right <= width - ToolbarGeometry.Outer);
                for (int i = 0; i < 10; i++) Program.Assert(g.Biome(i).Bottom < g.Height(true) && g.Biome(i).Right <= width - ToolbarGeometry.Outer);
            }
        });
        test("station session state and palette expansion are independent of query", () =>
        {
            var workbench = new QueryState(true) { EquipmentCategory = EquipmentCategory.Tools, Biome = BiomeChoice.BlackForest };
            var forge = new QueryState(true); var food = new QueryState();
            workbench.BiomesExpanded = true; workbench.BiomesExpanded = false;
            Program.Assert(workbench.Biome == BiomeChoice.BlackForest && workbench.EquipmentCategory == EquipmentCategory.Tools);
            Program.Assert(!forge.BiomesExpanded && forge.EquipmentCategory == EquipmentCategory.All && !food.Equipment);
            Program.Assert(RecipeInfo.StableKey("forge", "Recipe_SwordIron", "SwordIron") == "v1/forge/Recipe_SwordIron/SwordIron");
        });
        test("equipment materials preserve sources, common neutrality and override priority", () =>
        {
            int forest = BiomeAssociation.Bit(BiomeChoice.BlackForest), swamp = BiomeAssociation.Bit(BiomeChoice.Swamp), mountain = BiomeAssociation.Bit(BiomeChoice.Mountains);
            var b = BiomeCatalogue.ClassifyEquipment(new[] { "Bronze", "Wood", "LeatherScraps" }, false);
            Program.Assert(b.Mask == (forest | BiomeAssociation.Bit(BiomeChoice.Meadows)) && b.Source == BiomeSource.Estimated);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Wood", "Coal", "Stone", "Feathers" }, false).Mask == 0);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Iron", "Silver", "ModMaterial" }, false).Mask == (swamp | mountain));
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Iron", "ModMaterial" }, false).Source == BiomeSource.Partial);
            var chosen = new BiomeAssociation(0, BiomeSource.Override);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Iron" }, false, chosen).Source == BiomeSource.Override);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Iron", "Silver" }, true).Mask == 0);
            Program.Assert(!BiomeCatalogue.Ingredient("Iron").Complete, "Equipment map must not change food provenance");
        });
        test("equipment mixed associations never make undiscovered options eligible", () =>
        {
            int forest = BiomeAssociation.Bit(BiomeChoice.BlackForest), mountain = BiomeAssociation.Bit(BiomeChoice.Mountains);
            var b = BiomeCatalogue.ClassifyEquipment(new[] { "Bronze", "Silver" }, false);
            Program.Assert(b.Mask == (forest | mountain));
            foreach (var option in BiomeAccess.Options(forest, b.Mask, false))
                Program.Assert(option.Choice == BiomeChoice.All || option.Choice == BiomeChoice.BlackForest || (option.Choice == BiomeChoice.Unknown && !option.Enabled));
            Program.Assert(BiomeAccess.ValidSelection(BiomeChoice.Mountains, forest, b.Mask, false) == BiomeChoice.All);
        });
        test("Black Forge material additions retain known partial sources and overrides", () =>
        {
            int mist = BiomeAssociation.Bit(BiomeChoice.Mistlands), ash = BiomeAssociation.Bit(BiomeChoice.Ashlands), north = BiomeAssociation.Bit(BiomeChoice.DeepNorth);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Carapace", "MorgenSinew", "MooseHide" }, false).Mask == (mist | ash | north));
            var unknown = BiomeCatalogue.ClassifyEquipment(new[] { "MooseHide", "NewModdedOre" }, false);
            Program.Assert(unknown.Mask == north && unknown.Source == BiomeSource.Partial);
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Carapace" }, false, new BiomeAssociation(0, BiomeSource.Override)).Source == BiomeSource.Override);
            Program.Assert(!BiomeCatalogue.Ingredient("Carapace").Complete, "Do not extend food provenance");
            Program.Assert(BiomeCatalogue.ClassifyEquipment(new[] { "Gold", "Frostwood", "MooseHide" }, false).Mask == north);
            var upgraded = BiomeCatalogue.ClassifyEquipment(new[] { "BowAshlands", "GemstoneRed" }, false);
            Program.Assert(upgraded.Mask == ash && upgraded.Source == BiomeSource.Estimated);
            var mixed = BiomeCatalogue.ClassifyEquipment(new[] { "FlametalNew", "Carapace" }, false);
            Program.Assert(mixed.Mask == (ash | mist) && mixed.Source == BiomeSource.Estimated);
            for (int flags = 0; flags < 512; flags++)
            {
                int discovered = flags << 1;
                Program.Assert((BiomeAccess.Revealed(mist | ash | north, discovered) & ~discovered) == 0);
                Program.Assert(BiomeAccess.Options(discovered, mist | ash | north, false).All(o => o.Choice == BiomeChoice.All || o.Choice == BiomeChoice.Unknown || (BiomeAssociation.Bit(o.Choice) & discovered) != 0));
            }
        });
        test("category native sprite metadata and icon bounds", () =>
        {
            using var icons = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "native-equipment-icons-1.0.20.json")));
            foreach (string prefab in new[] { "SwordBronze", "ArmorBronzeChest", "ShieldWood", "Hammer", "ArrowWood", "StaffFireball", "Coins", "Resin" })
            {
                var icon = icons.RootElement.GetProperty(prefab)[0];
                Program.Assert(icon.GetProperty("spriteName").GetString()!.Length > 0);
                Program.Assert(icon.GetProperty("rect").GetProperty("width").GetDouble() == 64);
            }
            foreach (float width in new[] { 170f, 187f, 384f })
                for (int i = 0; i < 8; i++)
                {
                    var box = new EquipmentToolbarGeometry(width).Category(i);
                    Program.Assert(box.Width >= EquipmentToolbarGeometry.IconSize + 4 && box.Height >= EquipmentToolbarGeometry.IconSize + 4);
                }
        });
        Audit(test);
    }
    internal static void Report(string output)
    {
        var recipes = Catalogue();
        var results = recipes.Select(r =>
        {
            string[] ingredients = r.GetProperty("ingredients").EnumerateArray()
                .Where(i => i.GetProperty("m_amount").GetInt32() > 0 && i.GetProperty("m_upgraderResource").GetInt32() == 0)
                .Select(i => i.GetProperty("item").GetString() ?? "~unknown").ToArray();
            var biome = BiomeCatalogue.ClassifyEquipment(ingredients, r.GetProperty("onlyOneIngredient").GetInt32() != 0);
            return new { recipe = r.GetProperty("name").GetString(), output = r.GetProperty("output").GetString(),
                station = r.GetProperty("station").GetString(), enabled = r.GetProperty("enabled").GetInt32() != 0,
                categories = ((EquipmentFlags)r.GetProperty("expectedFlags").GetInt32()).ToString(),
                mask = biome.Mask, source = biome.Source.ToString(), ambiguous = biome.Ambiguous,
                unknownIngredients = ingredients.Where(i => !BiomeCatalogue.EquipmentIngredient(i).Complete).ToArray() };
        }).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Equipment audit: {results.Length} assets; {results.Count(r => r.enabled)} enabled; {results.Count(r => r.mask == 0)} Misc; {results.Count(r => r.source == "Partial")} Partial; {results.Count(r => r.ambiguous)} ambiguous biome results.");
        Console.WriteLine("Unresolved or partial ingredient entries: " + string.Join(", ", results.SelectMany(r => r.unknownIngredients).Distinct().OrderBy(x => x)));
    }
    private static JsonElement[] Catalogue()
    {
        var rows = new List<JsonElement>();
        foreach (string file in new[] { "vanilla-equipment-1.0.20.json", "vanilla-blackforge-1.0.20.json", "vanilla-galdr-1.0.20.json" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", file)));
            rows.AddRange(doc.RootElement.GetProperty("recipes").EnumerateArray().Select(r => r.Clone()));
        }
        return rows.ToArray();
    }
    private static void Audit(Action<string, Action> test)
    {
        var recipes = Catalogue();
        test("equipment asset fixture coverage", () =>
        {
            Program.Assert(recipes.Length == 340 && recipes.Count(r => r.GetProperty("enabled").GetInt32() != 0) == 326);
            Program.Assert(recipes.Count(r => r.GetProperty("station").GetString() == "forge") == 74);
            Program.Assert(recipes.Count(r => r.GetProperty("station").GetString() == "blackforge") == 144);
            var galdr=recipes.Where(r=>r.GetProperty("station").GetString()=="piece_magetable").ToArray();
            Program.Assert(galdr.Length==34 && galdr.Count(r=>r.GetProperty("noCraftOnlyUpgrade").GetInt32()!=0)==7);
            Program.Assert(galdr.Count(r=>r.GetProperty("enabled").GetInt32()!=0 && r.GetProperty("noCraftOnlyUpgrade").GetInt32()==0)==25);
        });
        foreach (var item in recipes)
        {
            JsonElement r = item;
            test("equipment recipe " + r.GetProperty("name").GetString(), () =>
            {
                string id = r.GetProperty("output").GetString()!;
                int type = r.GetProperty("itemType").GetInt32(), skill = r.GetProperty("skillType").GetInt32();
                var a = r.GetProperty("attack"); var d = r.GetProperty("damages");
                var flags = Classify(id, type, skill, d.GetProperty("m_chop").GetDouble(), d.GetProperty("m_pickaxe").GetDouble(),
                    a.GetProperty("m_consumeItem").GetInt32() != 0 && a.GetProperty("m_attackType").GetInt32() == 2, r.GetProperty("eitrRegen").GetDouble());
                // Independently reviewed expected membership stored alongside the observed metadata.
                Program.Assert((int)flags == r.GetProperty("expectedFlags").GetInt32(), id + ": " + flags);
                Program.Assert(flags != EquipmentFlags.None);
                if (r.TryGetProperty("expectedBiomeMask", out JsonElement expectedMask))
                {
                    string[] ingredients = r.GetProperty("ingredients").EnumerateArray()
                        .Where(i => i.GetProperty("m_amount").GetInt32() > 0 && i.GetProperty("m_upgraderResource").GetInt32() == 0)
                        .Select(i => i.GetProperty("item").GetString() ?? "~unknown").ToArray();
                    var biome = BiomeCatalogue.ClassifyEquipment(ingredients, r.GetProperty("onlyOneIngredient").GetInt32() != 0);
                    Program.Assert(biome.Mask == expectedMask.GetInt32() && biome.Source.ToString() == r.GetProperty("expectedBiomeSource").GetString(), id + ": biome expectation mismatch");
                }
            });
        }
    }
}
