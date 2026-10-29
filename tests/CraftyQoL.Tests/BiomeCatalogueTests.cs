using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CraftyQoL.Core;

internal static class BiomeCatalogueTests
{
    private static int Bits(params BiomeChoice[] choices) => choices.Aggregate(0, (mask, b) => mask | BiomeAssociation.Bit(b));
    private static BiomeAssociation Infer(params string[] ingredients) => BiomeCatalogue.Classify("modded", ingredients, false);
    internal static void Run(Action<string, Action> test)
    {
        test("single source does not imply other biomes", () =>
            Program.Assert(Infer("Turnip").Mask == Bits(BiomeChoice.Swamp) && Infer("Turnip").Source == BiomeSource.Estimated));
        test("union includes every required specific source", () =>
            Program.Assert(Infer("Carrot", "Bloodbag", "Honey").Mask == Bits(BiomeChoice.BlackForest, BiomeChoice.Swamp)));
        test("honey is common and contributes no biome", () =>
        {
            Program.Assert(BiomeCatalogue.Ingredient("Honey").Kind == IngredientKind.Common);
            Program.Assert(Infer("Honey").Mask == 0 && Infer("Honey", "WolfMeat").Mask == Bits(BiomeChoice.Mountains));
        });
        test("unknown input preserves partial known union", () =>
        {
            var b = Infer("Bloodbag", "Carrot", "ModdedRoot");
            Program.Assert(b.Source == BiomeSource.Partial && b.Mask == Bits(BiomeChoice.Swamp, BiomeChoice.BlackForest));
            Program.Assert(!b.Matches(BiomeChoice.Unknown) && b.Explanation.Contains("partial"));
        });
        test("all unknown and absent input stay unknown", () =>
        {
            foreach (var b in new[] { Infer(), Infer("MeadowsInventedFood"), Infer(null!, ""), Infer("~unknown") })
                Program.Assert(b.Mask == 0 && b.Source == BiomeSource.Unknown && b.Matches(BiomeChoice.Unknown));
        });
        test("one ingredient can have multiple verified origins", () =>
            Program.Assert(Infer("Thistle").Mask == Bits(BiomeChoice.BlackForest, BiomeChoice.Swamp) &&
                BiomeCatalogue.Ingredient("Thistle").Kind == IngredientKind.MultiBiome));
        test("portable trader chicken is not inferred from boss key", () =>
            Program.Assert(Infer("ChickenEgg").Mask == 0 && Infer("ChickenEgg", "MushroomJotunPuffs").Mask == Bits(BiomeChoice.Mistlands)));
        test("fish provenance is not universally Ocean", () =>
        {
            Program.Assert(Infer("Fish9").Mask == Bits(BiomeChoice.Mistlands));
            Program.Assert(Infer("Fish12").Mask == Bits(BiomeChoice.Ocean, BiomeChoice.Mistlands));
            var b = Infer("FishCooked", "BarleyFlour");
            Program.Assert(b.Mask == Bits(BiomeChoice.Plains) && b.Source == BiomeSource.Partial && b.Ambiguous);
        });
        test("equivalent alternatives share a complete association", () =>
        {
            var b = BiomeCatalogue.Classify("x", new[] { "Fish3", "Fish8" }, true);
            Program.Assert(b.Mask == Bits(BiomeChoice.Ocean) && b.Source == BiomeSource.Estimated && !b.Ambiguous);
        });
        test("differing alternatives are never unioned", () =>
        {
            var b = BiomeCatalogue.Classify("x", new[] { "Carrot", "Bloodbag" }, true);
            Program.Assert(b.Mask == 0 && b.Source == BiomeSource.Unknown && b.Ambiguous);
        });
        test("overlapping alternatives keep only guaranteed overlap", () =>
        {
            var b = BiomeCatalogue.Classify("x", new[] { "Fish1", "Fish5" }, true);
            Program.Assert(b.Mask == Bits(BiomeChoice.BlackForest) && b.Source == BiomeSource.Partial && b.Ambiguous);
        });
        test("unknown alternative cannot imply a required known source", () =>
        {
            var b = BiomeCatalogue.Classify("x", new[] { "ModdedFish", "Fish8" }, true);
            Program.Assert(b.Mask == 0 && b.Ambiguous);
        });
        test("curated themes remain authoritative over ingredients", () =>
        {
            Program.Assert(BiomeCatalogue.Classify("SerpentStew", new[] { "Mushroom", "ModdedRoot" }, false).Mask == Bits(BiomeChoice.Ocean));
            var b = BiomeCatalogue.Classify("FeastMountains_Material", new[] { "Carrot" }, false);
            Program.Assert(b.Mask == Bits(BiomeChoice.Mountains) && b.Source == BiomeSource.Curated);
        });
        test("user override replaces curated and ingredient mappings", () =>
        {
            var chosen = new BiomeAssociation(Bits(BiomeChoice.Swamp, BiomeChoice.Plains), BiomeSource.Override);
            var b = BiomeCatalogue.Classify("SerpentStew", new[] { "Mushroom" }, false, chosen);
            Program.Assert(b.Mask == chosen.Mask && b.Source == BiomeSource.Override);
            b = BiomeCatalogue.Classify("SerpentStew", new[] { "Mushroom" }, false, new BiomeAssociation(0, BiomeSource.Override));
            Program.Assert(b.Mask == 0 && b.Source == BiomeSource.Override && b.Explanation == "explicit override");
        });
        test("composite and finishing ingredients preserve upstream sources", () =>
        {
            Program.Assert(Infer("MisthareSupreme").Mask == Bits(BiomeChoice.BlackForest, BiomeChoice.Mistlands));
            Program.Assert(Infer("ScorchingMedley").Mask == Bits(BiomeChoice.Mountains, BiomeChoice.Mistlands, BiomeChoice.Ashlands));
            Program.Assert(Infer("BreadDough").Mask == Bits(BiomeChoice.Plains));
            Program.Assert(Infer("CookedBjornMeat").Mask == Bits(BiomeChoice.BlackForest, BiomeChoice.Plains));
        });
        test("verified prior ingredients retained; guessed identifiers fail closed", () =>
        {
            Program.Assert(Infer("CookedWolfMeat").Mask == Bits(BiomeChoice.Mountains));
            Program.Assert(Infer("CookedLoxMeat").Mask == Bits(BiomeChoice.Plains));
            Program.Assert(Infer("MinceMeatSauce").Mask == Bits(BiomeChoice.Meadows, BiomeChoice.BlackForest));
            Program.Assert(Infer("Fiddlehead", "Lingonberries", "Sulfur").Mask == 0);
        });
        test("order and duplicate requirements do not alter provenance", () =>
        {
            var ingredients = new[] { "ModdedRoot", "Bloodbag", "Thistle", "Honey", "Carrot" };
            foreach (bool alternatives in new[] { false, true })
            {
                var b = BiomeCatalogue.Classify("x", ingredients, alternatives);
                foreach (var order in new[] { ingredients.Reverse().ToArray(), ingredients.Concat(ingredients).ToArray() })
                {
                    var other = BiomeCatalogue.Classify("x", order, alternatives);
                    Program.Assert(b.Mask == other.Mask && b.Source == other.Source && b.Ambiguous == other.Ambiguous);
                }
            }
        });
        test("partial biome composes with categories search favourites and sort", () =>
        {
            var info = new RecipeInfo("stable", "Modded Blood Soup", "out", new FoodStats(80, 40, 0), NutritionSource.Direct, Infer("Bloodbag", "Carrot", "ModdedRoot"));
            var other = new RecipeInfo("other", "Soup", "out2", new FoodStats(10, 1, 0), NutritionSource.Direct, Infer("Turnip"));
            var source = new List<RecipeInfo> { other, info };
            var favourites = new HashSet<string> { "stable", "locked" };
            foreach (var biome in new[] { BiomeChoice.BlackForest, BiomeChoice.Swamp })
            {
                var q = new QueryState { Biome = biome, Category = FoodCategory.Stamina, Search = "BLOOD", FavouritesOnly = true };
                var result = RecipeQuery.Filter(source, q, favourites); RecipeQuery.Sort(result, FoodSort.Health);
                Program.Assert(result.Count == 1 && result[0] == info && source[0] == other);
            }
            Program.Assert(RecipeQuery.Filter(source, new QueryState { Biome = BiomeChoice.Unknown }, favourites).Count == 0);
            Program.Assert(RecipeQuery.Filter(source, new QueryState(), favourites).SequenceEqual(source));
        });
        VanillaCatalogue(test);
    }

    private static void VanillaCatalogue(Action<string, Action> test)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "vanilla-biomes-1.0.20.json")));
        var rows = doc.RootElement.GetProperty("recipes").EnumerateArray().ToArray();
        var expected = Expected();
        test("complete installed catalogue baseline retained", () =>
        {
            Program.Assert(rows.Length == 69 && rows.Count(r => r.GetProperty("station").GetString() == "piece_cauldron") == 41);
            Program.Assert(rows.Count(r => r.GetProperty("before").GetProperty("mask").GetInt32() == 0) == 27);
            Program.Assert(rows.Count(r => r.GetProperty("onlyOneIngredient").GetBoolean()) == 1);
            Program.Assert(expected.Count == rows.Length);
        });
        var results = new List<BiomeAssociation>();
        foreach (var row in rows)
        {
            string output = row.GetProperty("output").GetString()!;
            string[] inputs = row.GetProperty("ingredients").EnumerateArray().Select(i => i.GetProperty("item").GetString()!).ToArray();
            bool alternatives = row.GetProperty("onlyOneIngredient").GetBoolean();
            var actual = BiomeCatalogue.Classify(output, inputs, alternatives); results.Add(actual);
            test("audited vanilla provenance: " + output, () =>
            {
                Program.Assert(actual.Mask == expected[output], "Unexpected biome union for " + output);
                foreach (string input in inputs)
                {
                    var origin = BiomeCatalogue.Ingredient(input);
                    Program.Assert(origin.Kind != IngredientKind.Unknown && !string.IsNullOrWhiteSpace(origin.Evidence), "Unreviewed input " + input);
                }
                var reverse = BiomeCatalogue.Classify(output, inputs.Reverse().ToArray(), alternatives);
                Program.Assert(reverse.Mask == actual.Mask && reverse.Source == actual.Source && reverse.Ambiguous == actual.Ambiguous);
                bool partial = output == "FishSoup" || output == "FishWraps" || output == "SmokedFish";
                bool curated = row.GetProperty("before").GetProperty("source").GetString() == "Curated";
                Program.Assert(actual.Source == (curated ? BiomeSource.Curated : expected[output] == 0 ? BiomeSource.Unknown : partial ? BiomeSource.Partial : BiomeSource.Estimated));
                Program.Assert(actual.Ambiguous == (partial || output == "FishRaw"));
            });
        }
        test("vanilla coverage preserves meaningful Unknown and partial results", () =>
        {
            Program.Assert(results.Count(r => r.Mask != 0) == 67 && results.Count(r => r.Source == BiomeSource.Partial) == 3);
            Program.Assert(results.Count(r => r.Ambiguous) == 4 && results.Count(r => (r.Mask & (r.Mask - 1)) != 0) == 24);
        });
        test("every audited union remains independently discovery gated for 512 masks", () =>
        {
            foreach (var info in results)
                for (int discovery = 0; discovery < 512; discovery++)
                {
                    int known = discovery << 1;
                    Program.Assert((BiomeAccess.Revealed(info.Mask, known) & ~known) == 0);
                    foreach (var option in BiomeAccess.Options(known, info.Mask, info.Mask == 0))
                        if (option.Choice != BiomeChoice.All && option.Choice != BiomeChoice.Unknown)
                        {
                            int bit = BiomeAssociation.Bit(option.Choice);
                            Program.Assert((known & bit) != 0 && option.Enabled == ((info.Mask & bit) != 0));
                        }
                }
        });
    }

    // Reviewed expectations from the asset/source audit, intentionally independent of runtime lookup construction.
    private static Dictionary<string, int> Expected()
    {
        var e = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(string choices, params string[] outputs)
        {
            int mask = choices.Length == 0 ? 0 : choices.Split(',').Aggregate(0, (m,b) => m | BiomeAssociation.Bit(Enum.Parse<BiomeChoice>(b)));
            foreach (string output in outputs) e.Add(output, mask);
        }
        Add("", "CookedEgg", "FishRaw");
        Add("Meadows", "BoarJerky", "FeastMeadows_Material");
        Add("BlackForest", "FeastBlackforest_Material", "FishingBaitForest");
        Add("Swamp", "BlackSoup", "FeastSwamps_Material", "FishingBaitSwamp");
        Add("Mountains", "FeastMountains_Material", "FishingBaitCave", "FishingBaitDeepNorth", "OnionSoup", "WolfJerky");
        Add("Plains", "BreadDough", "FeastPlains_Material", "FishWraps", "FishingBaitMistlands", "FishingBaitPlains", "LoxPieUncooked", "VikingCupcakeUncooked");
        Add("Mistlands", "FeastMistlands_Material", "HoneyGlazedChickenUncooked", "MushroomOmelette", "SeekerAspic");
        Add("Ashlands", "FeastAshlands_Material", "FierySvinstew", "FishingBaitAshlands", "MashedMeat", "SpicyMarmalade");
        Add("DeepNorth", "BakedPoteitrUncooked", "FeastDeepNorth_Material", "FishSoup", "KaleChipsUncooked", "Lingondricka", "MeatballsMashedPoteitr", "MooseKebab", "OatmealLingonberryJam", "OvenPancakeUncooked", "SealSoup", "SmokedFish", "SmokedMooseMeat");
        Add("Ocean", "FeastOceans_Material", "FishingBaitOcean", "SerpentStew");
        Add("Meadows,BlackForest", "DeerStew", "MinceMeatSauce", "QueensJam");
        Add("Meadows,BlackForest,Swamp", "CarrotSoup", "Sausages", "ShocklateSmoothie");
        Add("BlackForest,Swamp,Plains", "BloodPudding");
        Add("BlackForest,Mountains,DeepNorth", "Eyescream");
        Add("Plains,Mistlands", "FishAndBreadUncooked", "MeatPlatterUncooked", "YggdrasilPorridge");
        Add("Swamp,Mistlands", "MagicallyStuffedShroomUncooked");
        Add("Mistlands,Ashlands", "MarinatedGreens", "SizzlingBerryBroth", "SparklingShroomshake");
        Add("BlackForest,Mistlands", "MisthareSupremeUncooked");
        Add("BlackForest,DeepNorth", "Pancakes");
        Add("Plains,Ashlands", "PiquantPieUncooked", "RoastedCrustPieUncooked");
        Add("BlackForest,Plains", "PulledBear");
        Add("Mountains,Plains,Mistlands", "Salad");
        Add("Mountains,Mistlands,Ashlands", "ScorchingMedley");
        Add("Meadows,Swamp", "TurnipStew");
        Add("Meadows,BlackForest,Swamp,Mountains", "WolfMeatSkewer");
        return e;
    }
}
