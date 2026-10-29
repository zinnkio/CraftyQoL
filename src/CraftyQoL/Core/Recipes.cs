using System;
using System.Collections.Generic;
using System.Text;

namespace CraftyQoL.Core
{
    [Flags] internal enum FoodFlags { None = 0, Health = 1, Stamina = 2, Eitr = 4 }
    internal enum NutritionSource { Unknown, Direct, Cooked, Feast, Ambiguous }
    internal enum BiomeSource { Unknown, Curated, Estimated, Override, Partial }

    internal readonly struct FoodStats : IEquatable<FoodStats>
    {
        internal readonly double? Health, Stamina, Eitr;
        internal FoodStats(double? health, double? stamina, double? eitr)
        { Health = Valid(health); Stamina = Valid(stamina); Eitr = Valid(eitr); }
        private static double? Valid(double? value) => value.HasValue && value.Value >= 0 &&
            !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) ? value : null;
        internal FoodFlags Categories
        {
            get
            {
                double max = Math.Max(Health ?? 0, Math.Max(Stamina ?? 0, Eitr ?? 0));
                if (max <= 0) return FoodFlags.None;
                double threshold = max * 0.5;
                return ((Health ?? 0) > 0 && Health >= threshold ? FoodFlags.Health : 0)
                     | ((Stamina ?? 0) > 0 && Stamina >= threshold ? FoodFlags.Stamina : 0)
                     | ((Eitr ?? 0) > 0 && Eitr >= threshold ? FoodFlags.Eitr : 0);
            }
        }
        internal double? Value(FoodSort sort) => sort == FoodSort.Health ? Health : sort == FoodSort.Stamina ? Stamina : Eitr;
        public bool Equals(FoodStats other) => Health == other.Health && Stamina == other.Stamina && Eitr == other.Eitr;
        public override bool Equals(object? obj) => obj is FoodStats other && Equals(other);
        public override int GetHashCode() => Health.GetHashCode() ^ Stamina.GetHashCode() ^ Eitr.GetHashCode();
    }

    internal readonly struct BiomeAssociation
    {
        internal readonly int Mask;
        internal readonly BiomeSource Source;
        internal readonly bool Ambiguous;
        internal BiomeAssociation(int mask, BiomeSource source, bool ambiguous = false)
        { Mask = mask; Source = source; Ambiguous = ambiguous; }
        internal bool IsIngredientDerived => Source == BiomeSource.Estimated || Source == BiomeSource.Partial;
        internal string Explanation => Source == BiomeSource.Override ? "explicit override" :
            Source == BiomeSource.Curated ? "reviewed mapping" :
            Source == BiomeSource.Estimated ? "complete ingredient mapping" :
            Source == BiomeSource.Partial ? (Ambiguous ? "partial mapping; source alternatives vary" : "partial ingredient mapping") :
            Ambiguous ? "unknown; source alternatives vary" : "no reliable biome association";
        internal static int Bit(BiomeChoice biome) => biome == BiomeChoice.All || biome == BiomeChoice.Unknown ? 0 : 1 << (int)biome;
        internal bool Matches(BiomeChoice biome) => biome == BiomeChoice.All ||
            (biome == BiomeChoice.Unknown ? Mask == 0 : (Mask & Bit(biome)) != 0);
    }

    internal sealed class RecipeInfo
    {
        internal readonly string Key, Name, SearchName, Output;
        internal readonly FoodStats Food;
        internal readonly NutritionSource Nutrition;
        internal readonly BiomeAssociation Biome;
        internal readonly bool Persistable;
        internal readonly EquipmentFlags Equipment;
        internal readonly MeadFlags Mead;
        internal readonly RecipeFamily Family;
        internal RecipeInfo(string key, string name, string output, FoodStats food, NutritionSource nutrition,
            BiomeAssociation biome, bool persistable = true, EquipmentFlags equipment = EquipmentFlags.None, MeadFlags mead = MeadFlags.Other, RecipeFamily family=RecipeFamily.Other)
        {
            Key = key; Name = name; SearchName = Normalize(name); Output = output;
            Food = food; Nutrition = nutrition; Biome = biome; Persistable = persistable; Equipment = equipment; Mead = mead; Family=family;
        }
        internal static string Normalize(string value)
        { try { return value.Normalize(NormalizationForm.FormC); } catch (ArgumentException) { return value; } }
        internal static string StableKey(string station, string recipe, string output)
            => "v1/" + Uri.EscapeDataString(station) + "/" + Uri.EscapeDataString(recipe.Length == 0 ? "~output" : recipe)
               + "/" + Uri.EscapeDataString(output);
    }

    internal static class RecipeQuery
    {
        internal static List<RecipeInfo> Filter(IReadOnlyList<RecipeInfo> source, QueryState query,
            ISet<string> favourites)
        {
            string search = RecipeInfo.Normalize(query.Search.Trim());
            var result = new List<RecipeInfo>(source.Count);
            FoodFlags category = query.Category == FoodCategory.Health ? FoodFlags.Health :
                query.Category == FoodCategory.Stamina ? FoodFlags.Stamina :
                query.Category == FoodCategory.Eitr ? FoodFlags.Eitr : FoodFlags.None;
            for (int i = 0; i < source.Count; i++)
            {
                RecipeInfo info = source[i];
                if (category != FoodFlags.None && (info.Food.Categories & category) == 0) continue;
                if (!MatchesCommon(info, query, favourites, search)) continue;
                result.Add(info);
            }
            return result;
        }
        internal static bool MatchesCommon(RecipeInfo info, QueryState query, ISet<string> favourites, string normalizedSearch, bool ignoreBiome = false)
            => (ignoreBiome || info.Biome.Matches(query.Biome)) &&
               (!query.FavouritesOnly || (info.Persistable && favourites.Contains(info.Key))) &&
               (normalizedSearch.Length == 0 || info.SearchName.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0);
        internal static int Compare(RecipeInfo left, RecipeInfo right, FoodSort sort)
        {
            double? a = left.Food.Value(sort), b = right.Food.Value(sort);
            if (a.HasValue != b.HasValue) return a.HasValue ? -1 : 1;
            int stat = b.GetValueOrDefault().CompareTo(a.GetValueOrDefault());
            return stat != 0 ? stat : StringComparer.Ordinal.Compare(left.Key, right.Key);
        }
        internal static void Sort(List<RecipeInfo> recipes, FoodSort sort)
        {
            if (sort != FoodSort.Vanilla) StableOrder.Sort(recipes, (a, b) => Compare(a, b, sort));
        }
    }

    internal static class StableOrder
    {
        internal static void Sort<T>(List<T> items, Comparison<T> compare)
        {
            if (items.Count < 2) return;
            Merge(items, new T[items.Count], 0, items.Count, compare);
        }
        private static void Merge<T>(List<T> items, T[] scratch, int start, int end, Comparison<T> compare)
        {
            if (end - start < 2) return;
            int middle = start + (end - start) / 2;
            Merge(items, scratch, start, middle, compare);
            Merge(items, scratch, middle, end, compare);
            int a = start, b = middle, at = start;
            while (a < middle || b < end)
                scratch[at++] = b == end || (a < middle && compare(items[a], items[b]) <= 0) ? items[a++] : items[b++];
            for (int i = start; i < end; i++) items[i] = scratch[i];
        }
    }
}
