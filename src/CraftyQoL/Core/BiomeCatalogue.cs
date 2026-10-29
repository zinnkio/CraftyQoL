using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal enum IngredientKind { Unknown, BiomeSpecific, Common, MultiBiome, Special }

    internal readonly struct IngredientProvenance
    {
        internal readonly int Mask;
        internal readonly IngredientKind Kind;
        internal readonly bool Complete, Ambiguous;
        internal readonly string Evidence;
        internal IngredientProvenance(int mask, IngredientKind kind, string evidence, bool complete = true, bool ambiguous = false)
        { Mask = mask; Kind = kind; Evidence = evidence; Complete = complete; Ambiguous = ambiguous; }
    }

    // Reviewed normal-source associations for 1.0.20, not exclusive spawn locations.
    // Evidence groups and explicit exceptions: docs/BIOME_PROVENANCE.md.
    internal static class BiomeCatalogue
    {
        private static readonly Dictionary<string, IngredientProvenance> Ingredients = CreateIngredients();
        private static readonly Dictionary<string, IngredientProvenance> EquipmentIngredients = CreateEquipmentIngredients();
        internal static IngredientProvenance EquipmentIngredient(string prefab)
            => EquipmentIngredients.TryGetValue(prefab, out IngredientProvenance value) ? value : default;
        internal static BiomeAssociation ClassifyEquipment(IReadOnlyList<string> ingredients, bool alternatives, BiomeAssociation? explicitOverride = null)
            => explicitOverride ?? Infer(ingredients, alternatives, EquipmentIngredients);

        private static Dictionary<string, IngredientProvenance> CreateEquipmentIngredients()
        {
            var map = new Dictionary<string, IngredientProvenance>(Ingredients, StringComparer.Ordinal);
            int B(BiomeChoice biome) => BiomeAssociation.Bit(biome);
            void Add(int mask, string evidence, params string[] ids)
            {
                var kind = mask == 0 ? IngredientKind.Common : (mask & (mask - 1)) != 0 ? IngredientKind.MultiBiome : IngredientKind.BiomeSpecific;
                foreach (string id in ids) map[id] = new IngredientProvenance(mask, kind, evidence);
            }
            int m = B(BiomeChoice.Meadows), f = B(BiomeChoice.BlackForest), s = B(BiomeChoice.Swamp),
                mt = B(BiomeChoice.Mountains), p = B(BiomeChoice.Plains), mist = B(BiomeChoice.Mistlands),
                a = B(BiomeChoice.Ashlands), n = B(BiomeChoice.DeepNorth), o = B(BiomeChoice.Ocean);
            // E01: reviewed portable staples; no incidental biome membership from a common resource.
            Add(0, "E01: portable common staples / kiln conversion", "Wood", "Stone", "Coal", "Feathers", "Resin");
            Add(0, "E01: trader supplies, not trader location or boss key", "YmirRemains", "BlobVial");
            // E02: enabled vegetation -> harvest/log/fracture -> resource; processed forms follow exact conversion.
            Add(m, "E02: Meadows flint harvest", "Flint");
            Add(f, "E02: BlackForest pine logs and copper/tin veins; smelter and Bronze recipes", "RoundLog", "CopperOre", "TinOre", "Copper", "Tin", "Bronze", "BronzeNails");
            Add(m | p, "E02: Meadows birch/oak and Plains birch logs", "FineWood");
            Add(s, "E02: swamp trees and characteristic scrap/Wraith sources; smelter/recipe conversion", "ElderBark", "IronScrap", "Iron", "IronNails", "Chain", "Guck");
            Add(mt, "E02: Mountain silver veins and obsidian deposits", "SilverOre", "Silver", "Obsidian");
            Add(mist, "E02: Mistlands Yggdrasil shoots/logs", "YggdrasilWood");
            Add(p, "E02: Plains fuling scrap and Flax; furnace/spinning wheel conversions", "BlackMetalScrap", "BlackMetal", "Flax", "LinenThread");
            Add(o, "E02: enabled Ocean Leviathan barnacle harvest", "Chitin");
            // E03: normal habitat -> exact CharacterDrop; exclude roaming/raids and portable relocation.
            Add(m, "E03: Boar and Eikthyr characteristic sources", "LeatherScraps", "HardAntler", "TrophyBoar");
            Add(m | f, "E03: Deer normal habitats", "DeerHide", "TrophyDeer");
            Add(f, "E03: Troll and BlackForest shaman/elite", "TrollHide", "TrophyFrostTroll", "AncientSeed", "TrophyGreydwarfShaman");
            Add(f | p, "E03: Bjorn/Unbjorn share hide, but not paw", "BjornHide");
            Add(f, "E03: Bjorn only paw/trophy", "BjornPaw", "TrophyBjorn");
            Add(p, "E03: Unbjorn and Plains Lox/Deathsquito/Growth", "UndeadBjornRibcage", "TrophyBjornUndead", "LoxPelt", "TrophyLox", "Needle", "Tar", "TrophyGrowth", "TrophyGoblinBrute");
            Add(s, "E03: Abomination/Draugr/Leech/Blob/Surtling normal sources", "Root", "TrophyDraugrElite", "TrophyLeech", "TrophyBlob", "TrophySurtling");
            Add(mt, "E03: Wolf/Fenring/StoneGolem/Hatchling and FrostCave drops", "WolfFang", "WolfPelt", "WolfClaw", "WolfHairBundle", "TrophyWolf", "TrophyFenring", "TrophySGolem", "TrophyHatchling", "TrophyCultist", "Crystal");
            Add(o, "E03: Serpent normal source", "SerpentScale", "TrophySerpent");
            Add(a, "E03: Asksvin normal source", "AskHide", "AskBladder");
            Add(n, "E03: Seal/Writhan normal source", "SealHide", "WrithanRoots");
            // E04: Black Forge inputs, verified normal habitat -> CharacterDrop (same source policy).
            Add(mist, "E04: Seeker/Brute/Hare/Gjall normal habitats and drops", "Carapace", "Mandible", "ScaleHide", "Bilebag", "TrophySeeker", "TrophyGjall");
            Add(a, "E04: Morgen/Valkyrie/Bonemaw/Charred normal Ashlands sources", "MorgenHeart", "MorgenSinew", "TrophyMorgen", "TrophyFallenValkyrie", "BonemawSerpentTooth", "CharredBone");
            Add(n, "E04: Moose/Elaking/JotunWitch characteristic DeepNorth sources", "MooseHide", "MooseSinew", "TrophyMoose", "ElakingHairBundle", "NornThread", "TrophyJotunWitch", "MoldKeys", "MoldArmorMageChest", "MoldArmorMageHelmet", "MoldArmorMageLegs");
            // E06: exact registered deposit/tree/location -> resource, then verified furnace conversion.
            Add(a, "E06: LeviathanLava/CharredFortress ore, furnace; Ashlands tree logs and fortress stores", "FlametalOreNew", "FlametalNew", "Blackwood", "Grausten", "MoltenCore", "GemstoneBlue", "GemstoneGreen", "GemstoneRed", "BellFragment");
            Add(n, "E06: registered DN_gammeltrollFrac ore/furnace and DeepNorth pine logs", "GoldOre", "Gold", "Frostwood");
            // Explicit base-weapon recipe relationships used as upgrade-recipe ingredients.
            // Keep missing sources Partial rather than inventing a biome from the equipment name.
            void Composite(string output, params string[] inputs)
            {
                BiomeAssociation value = Infer(inputs, false, map);
                IngredientKind kind = value.Mask == 0 ? IngredientKind.Special : (value.Mask & (value.Mask - 1)) != 0 ? IngredientKind.MultiBiome : IngredientKind.BiomeSpecific;
                map[output] = new IngredientProvenance(value.Mask, kind, "E05: inspected Black Forge base recipe", value.Source == BiomeSource.Estimated, value.Ambiguous);
            }
            Composite("AtgeirGold", "Gold", "MoldAtgeir", "Frostwood");
            Composite("AxeBerzerkr", "CharredBone", "FlametalNew", "AskHide");
            Composite("AxeGold", "Gold", "MoldAxe", "Frostwood");
            Composite("BattleaxeGold", "Gold", "MoldAxe2H", "Frostwood");
            Composite("BowAshlands", "Blackwood", "CharredBone", "FlametalNew", "BonemawSerpentTooth");
            Composite("BowGold", "Gold", "MoldBow", "Frostwood");
            Composite("CrossbowGold", "Gold", "MoldCrossbow", "Frostwood");
            Composite("CrossbowRipper", "Blackwood", "FlametalNew", "MorgenSinew", "BonemawSerpentTooth");
            Composite("FistGold", "Gold", "MoldFistweapon", "Frostwood");
            Composite("KnifeGold", "Gold", "MoldKnife", "Frostwood");
            Composite("MaceEldner", "CharredBone", "FlametalNew", "SulfurStone", "AskHide");
            Composite("MaceGold", "Gold", "MoldMace", "Frostwood");
            Composite("SledgeGold", "Gold", "MoldMace2H", "Frostwood");
            Composite("SpearGold", "Gold", "MoldSpear", "Frostwood");
            Composite("SpearSplitner", "Blackwood", "FlametalNew", "AskHide", "BonemawSerpentTooth");
            Composite("SwordGold", "Gold", "MoldSword", "Frostwood");
            Composite("SwordNiedhogg", "CharredBone", "FlametalNew", "AskHide");
            Composite("THSwordSlayer", "FlametalNew", "AskHide", "MorgenSinew");
            Composite("THSwordGold", "Gold", "MoldSword2H", "BarkaBranch");
            // Wisp is deliberately neutral: portable lure, no invented Plains association from Yagluth progression.
            Add(0, "E01: portable Wisp lure; progression is not origin", "Wisp");
            return map;
        }
        private static readonly Dictionary<string, BiomeChoice> Meals = new Dictionary<string, BiomeChoice>(StringComparer.Ordinal)
        {
            ["FeastMeadows_Material"] = BiomeChoice.Meadows,
            ["FeastBlackforest_Material"] = BiomeChoice.BlackForest,
            ["FeastSwamps_Material"] = BiomeChoice.Swamp,
            ["FeastMountains_Material"] = BiomeChoice.Mountains,
            ["FeastPlains_Material"] = BiomeChoice.Plains,
            ["FeastMistlands_Material"] = BiomeChoice.Mistlands,
            ["FeastAshlands_Material"] = BiomeChoice.Ashlands,
            ["FeastDeepNorth_Material"] = BiomeChoice.DeepNorth,
            ["FeastOceans_Material"] = BiomeChoice.Ocean,
            ["SerpentStew"] = BiomeChoice.Ocean
        };

        internal static IngredientProvenance Ingredient(string? prefab)
            => prefab != null && Ingredients.TryGetValue(prefab, out IngredientProvenance origin) ? origin : default;

        internal static BiomeAssociation Classify(string output, IReadOnlyList<string> ingredients, bool alternatives,
            BiomeAssociation? explicitOverride = null)
        {
            // Explicit overrides replace, never supplement, existing associations (including Unknown).
            if (explicitOverride.HasValue) return explicitOverride.Value;
            if (Meals.TryGetValue(output, out BiomeChoice curated))
                return new BiomeAssociation(BiomeAssociation.Bit(curated), BiomeSource.Curated);
            return Infer(ingredients, alternatives, Ingredients);
        }

        private static BiomeAssociation Infer(IReadOnlyList<string> ingredients, bool alternatives,
            Dictionary<string, IngredientProvenance> catalogue)
        {
            if (ingredients.Count == 0) return default;
            int mask = 0, first = 0;
            bool complete = true, ambiguous = false;
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientProvenance origin = default;
                if (ingredients[i] != null) catalogue.TryGetValue(ingredients[i], out origin);
                complete &= origin.Complete;
                ambiguous |= origin.Ambiguous;
                if (i == 0) first = origin.Mask;
                if (alternatives)
                {
                    // One resource is consumed. Only shared memberships are safe for every branch.
                    mask = i == 0 ? origin.Mask : mask & origin.Mask;
                    ambiguous |= origin.Mask != first || !origin.Complete;
                }
                else mask |= origin.Mask;
            }
            BiomeSource source = mask == 0 ? BiomeSource.Unknown :
                complete && !ambiguous ? BiomeSource.Estimated : BiomeSource.Partial;
            return new BiomeAssociation(mask, source, ambiguous);
        }

        private static Dictionary<string, IngredientProvenance> CreateIngredients()
        {
            var map = new Dictionary<string, IngredientProvenance>(StringComparer.Ordinal);
            int B(BiomeChoice biome) => BiomeAssociation.Bit(biome);
            int meadows = B(BiomeChoice.Meadows), forest = B(BiomeChoice.BlackForest), swamp = B(BiomeChoice.Swamp),
                mountains = B(BiomeChoice.Mountains), plains = B(BiomeChoice.Plains), mist = B(BiomeChoice.Mistlands),
                ash = B(BiomeChoice.Ashlands), north = B(BiomeChoice.DeepNorth), ocean = B(BiomeChoice.Ocean);
            void Add(int mask, string evidence, params string[] ids)
            {
                var kind = (mask & (mask - 1)) != 0 ? IngredientKind.MultiBiome : IngredientKind.BiomeSpecific;
                foreach (string id in ids) map.Add(id, new IngredientProvenance(mask, kind, evidence));
            }
            void Special(string evidence, params string[] ids)
            { foreach (string id in ids) map.Add(id, new IngredientProvenance(0, IngredientKind.Special, evidence)); }
            void Alias(string output, string input, string evidence)
            {
                IngredientProvenance from = map[input];
                map.Add(output, new IngredientProvenance(from.Mask, from.Kind, evidence, from.Complete, from.Ambiguous));
            }
            void Meal(string output, string evidence, params string[] inputs)
            {
                BiomeAssociation value = Infer(inputs, false, map);
                var kind = value.Mask == 0 ? IngredientKind.Special : (value.Mask & (value.Mask - 1)) != 0 ? IngredientKind.MultiBiome : IngredientKind.BiomeSpecific;
                map.Add(output, new IngredientProvenance(value.Mask, kind, evidence, value.Source == BiomeSource.Estimated, value.Ambiguous));
            }

            // P01: normal creature spawns + exact CharacterDrop links; ignore roaming/raid expansion.
            Add(meadows, "P01: Boar/Neck normal spawns and drops", "RawMeat", "NeckTail");
            Add(meadows | forest, "P01: Deer normal habitats", "DeerMeat");
            Add(forest | plains, "P01: Bjorn and Unbjorn both drop BjornMeat", "BjornMeat");
            Add(forest | north, "P01: normal Greydwarf and frozen variants", "GreydwarfEye");
            Add(swamp, "P01: Leech/Draugr/Blob normal habitats", "Bloodbag", "Entrails", "Ooze");
            Add(mountains, "P01: Wolf/Hatchling normal habitats", "WolfMeat", "FreezeGland");
            Add(plains, "P01: Lox normal habitat", "LoxMeat");
            Add(mist, "P01: Seeker/Hare/Brood/Tick habitat; local Brood/Tick spawns may be gated", "BugMeat", "HareMeat", "RoyalJelly", "GiantBloodSack");
            Add(ash, "P01: Asksvin/Volture normal habitats (eggs also in nests)", "AsksvinMeat", "VoltureMeat", "VoltureEgg");
            Add(north, "P01: Moose/Seal normal habitats", "MooseMeat", "SealBlubber");
            Add(ocean, "P01: Serpent normal habitat", "SerpentMeat", "TrophySerpent");
            Add(forest, "P01: Troll drop, despite trophy identifier", "TrophyFrostTroll");
            Add(swamp, "P01: Abomination normal habitat", "TrophyAbomination");
            Add(mountains, "P01: Fenring/Hatchling normal habitats", "TrophyFenring", "TrophyHatchling");
            Add(plains, "P01: Goblin/Lox normal habitats", "TrophyGoblin", "TrophyLox");
            Add(ash, "P01: Charred_Melee normal habitat", "TrophyCharredMelee");

            // P02: enabled vegetation + Pickable; source crop seeds, not every transplant biome.
            Add(meadows, "P02: RaspberryBush", "Raspberry");
            Add(meadows | forest, "P02/P01: Dandelion pickable and Greydwarf_Elite drop", "Dandelion");
            Add(forest, "P02: BlueberryBush and wild CarrotSeeds", "Blueberries", "Carrot");
            Add(forest | swamp, "P02: Pickable_Thistle", "Thistle");
            Add(meadows | forest | swamp, "P02: current Pickable_Mushroom vegetation mask", "Mushroom");
            Add(swamp, "P02: wild TurnipSeeds; later incidental loot excluded", "Turnip");
            Add(mountains, "P03: qualified cabin chest OnionSeeds -> cultivation", "Onion");
            Add(plains, "P02: CloudberryBush", "Cloudberry");
            Add(plains, "P03: Plains-only barley cultivation; later incidental loot excluded", "Barley");
            Add(mist, "P02: qualified mushroom vegetation", "MushroomMagecap", "MushroomJotunPuffs");
            Add(mist, "P02: YggdrasilRoot -> SapCollector", "Sap");
            Add(ash, "P02: Pickable_SmokePuff", "MushroomSmokePuff");
            Add(ash, "P03: qualified ruins VineAsh harvest", "Vineberry");
            Add(ash, "P03: qualified ruins fiddlehead harvest", "Fiddleheadfern");
            Add(north, "P02: LingonberryBush and wild KaleSeeds", "Lingonberry", "Kale");
            Add(north, "P03: NorthVillage granary seed source -> cultivation", "OatSeeds", "Poteitr");
            Add(north, "P01/P02: frozen creature drops and ice1 vegetation; invasion spillover excluded", "Ice");

            // P04: deliberate neutral sources. A trader's location/boss key is not ingredient origin.
            map.Add("Honey", new IngredientProvenance(0, IngredientKind.Common, "P04: portable multi-biome beekeeping staple"));
            Special("P04: Haldor stock and portable domestic chicken line; not Plains merely due to boss key", "ChickenEgg", "ChickenMeat");
            Special("P04: Haldor stock", "FishingBait");
            Special("P04: BogWitch stock, not the spice's name or trader's biome", "SpiceForests", "SpiceOceans", "SpiceMountains", "SpicePlains", "SpiceMistlands", "SpiceAshlands", "SpiceDeepNorth");

            // P05: fish habitats are distinct; aquatic activities are not all Ocean.
            Add(meadows | forest | plains, "P05: normal fish spawn list", "Fish1", "Fish2");
            Add(ocean, "P05: normal fish spawn list", "Fish3", "Fish8");
            Add(mountains, "P05: cave lake rooms, theme4, qualified MountainCave02 generator", "Fish4_cave");
            Add(forest, "P05: normal fish spawn list", "Fish5");
            Add(swamp, "P05: normal fish spawn list", "Fish6");
            Add(plains, "P05: normal fish spawn list", "Fish7");
            Add(mist, "P05: normal fish spawn list", "Fish9");
            Add(north, "P05: normal fish spawn list", "Fish10");
            Add(ash, "P05: normal fish spawn list", "Fish11");
            Add(ocean | mist, "P05: normal fish spawn list", "Fish12");
            map.Add("FishRaw", new IngredientProvenance(0, IngredientKind.Special,
                "P05: Recipe_Fish1 alternatives have no common biome; input origin is lost", false, true));

            // P06: explicit transformations, separate from the nutrition graph. No runtime traversal.
            Alias("CookedMeat", "RawMeat", "P06: cooking station conversion");
            Alias("NeckTailGrilled", "NeckTail", "P06: cooking station conversion; prior compatible-mod ingredient");
            Alias("CookedWolfMeat", "WolfMeat", "P06: cooking station conversion; prior compatible-mod ingredient");
            Alias("CookedHareMeat", "HareMeat", "P06: cooking station conversion; prior compatible-mod ingredient");
            Alias("CookedLoxMeat", "LoxMeat", "P06: iron cooking conversion; prior compatible-mod ingredient");
            Alias("CookedVoltureMeat", "VoltureMeat", "P06: iron cooking conversion; prior compatible-mod ingredient");
            Alias("CookedDeerMeat", "DeerMeat", "P06: cooking station conversion");
            Alias("CookedBjornMeat", "BjornMeat", "P06: cooking station conversion");
            Alias("CookedBugMeat", "BugMeat", "P06: iron cooking station conversion");
            Alias("CookedAsksvinMeat", "AsksvinMeat", "P06: iron cooking station conversion");
            Alias("CookedMooseMeat", "MooseMeat", "P06: iron cooking station conversion");
            Alias("SerpentMeatCooked", "SerpentMeat", "P06: iron cooking station conversion");
            Alias("FishCooked", "FishRaw", "P06: cooking retains variable fish origin");
            Alias("BarleyFlour", "Barley", "P06: windmill conversion");
            Alias("Oat", "OatSeeds", "P06: windmill conversion");
            Alias("OatFlour", "Oat", "P06: windmill conversion");
            Meal("OatMilk", "P06: Recipe_OatMilk (Mead Ketill metadata only)", "Oat", "Ice");
            Meal("BreadDough", "P06: Recipe_Bread", "BarleyFlour");
            Alias("Bread", "BreadDough", "P06: oven conversion");
            Meal("CarrotSoup", "P06: Recipe_CarrotSoup; prior compatible-mod ingredient", "Mushroom", "Carrot");
            Meal("MinceMeatSauce", "P06: Recipe_MinceMeatSauce; prior compatible-mod ingredient", "RawMeat", "NeckTail", "Carrot");
            Meal("DeerStew", "P06: Recipe_DeerStew", "CookedDeerMeat", "Blueberries", "Carrot");
            Meal("QueensJam", "P06: Recipe_QueensJam", "Raspberry", "Blueberries");
            Meal("Sausages", "P06: Recipe_Sausages", "Entrails", "RawMeat", "Thistle");
            Meal("TurnipStew", "P06: Recipe_TurnipStew", "RawMeat", "Turnip");
            Meal("WolfMeatSkewer", "P06: Recipe_WolfSkewer", "WolfMeat", "Mushroom", "Onion");
            Meal("OnionSoup", "P06: Recipe_Onionsoup", "Onion");
            Meal("LoxPie", "P06: Recipe_LoxPie + oven conversion", "Cloudberry", "LoxMeat", "BarleyFlour");
            Meal("MisthareSupreme", "P06: Recipe_MisthareSupreme + oven conversion", "HareMeat", "MushroomJotunPuffs", "Carrot");
            Meal("YggdrasilPorridge", "P06: Recipe_YggdrasilPorridge", "Sap", "Barley", "RoyalJelly");
            Meal("ScorchingMedley", "P06: Recipe_ScorchingMedley", "MushroomJotunPuffs", "Onion", "Fiddleheadfern");
            return map;
        }
    }
}
