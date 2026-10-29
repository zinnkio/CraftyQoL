using System;
using System.Collections.Generic;
using System.Linq;
using CraftyQoL.Core;

internal static class QueryTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Category(string name, FoodStats stats, FoodFlags expected)
            => test(name, () => Program.Assert(stats.Categories == expected));
        Category("current Boar Jerky", new FoodStats(23, 23, 0), FoodFlags.Health | FoodFlags.Stamina);
        Category("current Serpent Stew", new FoodStats(80, 26, 0), FoodFlags.Health);
        Category("current Bread", new FoodStats(23, 70, 0), FoodFlags.Stamina);
        Category("current Seeker Aspic", new FoodStats(28, 14, 85), FoodFlags.Eitr);
        Category("current Deep North feast", new FoodStats(85, 85, 43), FoodFlags.Health | FoodFlags.Stamina | FoodFlags.Eitr);
        Category("inclusive 50 percent boundary", new FoodStats(100, 50, 0), FoodFlags.Health | FoodFlags.Stamina);
        Category("below threshold", new FoodStats(100, 49.99, 0), FoodFlags.Health);
        Category("zero vector", new FoodStats(0, 0, 0), FoodFlags.None);
        Category("missing vector", default, FoodFlags.None);
        Category("invalid fields", new FoodStats(double.NaN, -5, double.PositiveInfinity), FoodFlags.None);
        Category("partially invalid", new FoodStats(null, 80, -1), FoodFlags.Stamina);
        Category("scaled modded vector", new FoodStats(1e20, 1e20, 6e19), FoodFlags.Health | FoodFlags.Stamina | FoodFlags.Eitr);

        var a = Info("b", "Boar Jerky", new FoodStats(23, 23, 0), BiomeChoice.BlackForest);
        var b = Info("a", "Seeker Aspic", new FoodStats(28, 14, 85), BiomeChoice.Mistlands);
        var c = Info("c", "Crème brûlée", default, BiomeChoice.Unknown);
        var d = Info("d", "北方の料理", new FoodStats(0, 0, 0), BiomeChoice.DeepNorth);
        var input = new List<RecipeInfo> { a, b, c, d };
        var favourites = new HashSet<string>(StringComparer.Ordinal) { "b", "missing-mod-recipe" };
        test("empty query preserves available source order", () =>
            Program.Assert(RecipeQuery.Filter(input, new QueryState(), favourites).SequenceEqual(input)));
        test("case-insensitive localised name", () =>
            Program.Assert(RecipeQuery.Filter(input, new QueryState { Search = " JERKY " }, favourites).Single() == a));
        test("normalised Unicode and non-Latin search", () =>
        {
            Program.Assert(RecipeQuery.Filter(input, new QueryState { Search = "Cre\u0300me" }, favourites).Single() == c);
            Program.Assert(RecipeQuery.Filter(input, new QueryState { Search = "北方" }, favourites).Single() == d);
        });
        test("empty search result", () => Program.Assert(RecipeQuery.Filter(input, new QueryState { Search = "unobtainium" }, favourites).Count == 0));
        test("all predicates combine", () =>
        {
            var q = new QueryState { Category = FoodCategory.Stamina, Biome = BiomeChoice.BlackForest, FavouritesOnly = true, Search = "boar" };
            Program.Assert(RecipeQuery.Filter(input, q, favourites).Single() == a);
            q.Search = "missing"; Program.Assert(RecipeQuery.Filter(input, q, favourites).Count == 0);
        });
        test("no locked input expansion from missing favourites", () =>
            Program.Assert(RecipeQuery.Filter(input, new QueryState { FavouritesOnly = true }, favourites).Single() == a));
        test("stat sort independent of category and missing after zero", () =>
        {
            var rows = new List<RecipeInfo>(input);
            RecipeQuery.Sort(rows, FoodSort.Health);
            Program.Assert(rows.SequenceEqual(new[] { b, a, d, c }));
            Program.Assert(input[0] == a && input[1] == b, "Source list mutated");
        });
        test("vanilla order preserved", () =>
        {
            var rows = new List<RecipeInfo>(input); RecipeQuery.Sort(rows, FoodSort.Vanilla);
            Program.Assert(rows.SequenceEqual(input));
        });
        test("deterministic stat ties and stable colliding keys", () =>
        {
            var first = Info("same", "first", new FoodStats(10, 0, 0), BiomeChoice.Unknown);
            var second = Info("same", "second", new FoodStats(10, 0, 0), BiomeChoice.Unknown);
            var before = Info("aaa", "before", new FoodStats(10, 0, 0), BiomeChoice.Unknown);
            var rows = new List<RecipeInfo> { first, second, before }; RecipeQuery.Sort(rows, FoodSort.Health);
            Program.Assert(rows.SequenceEqual(new[] { before, first, second }));
        });
        test("empty sort", () => RecipeQuery.Sort(new List<RecipeInfo>(), FoodSort.Eitr));
        test("unknown biome remains available under All and Unknown", () =>
        {
            Program.Assert(c.Biome.Matches(BiomeChoice.All) && c.Biome.Matches(BiomeChoice.Unknown));
            Program.Assert(!c.Biome.Matches(BiomeChoice.Meadows));
        });
        test("mixed biome memberships", () =>
        {
            var association = new BiomeAssociation(BiomeAssociation.Bit(BiomeChoice.Swamp) | BiomeAssociation.Bit(BiomeChoice.Plains), BiomeSource.Override);
            Program.Assert(association.Matches(BiomeChoice.Swamp) && association.Matches(BiomeChoice.Plains) && !association.Matches(BiomeChoice.Unknown));
        });
        test("complete ingredient union estimate", () =>
        {
            var association = BiomeCatalogue.Classify("modded", new[] { "Honey", "RoyalJelly" }, false);
            Program.Assert(association.Source == BiomeSource.Estimated && association.Matches(BiomeChoice.Mistlands));
        });
        test("unknown ingredients and ambiguous alternatives", () =>
        {
            Program.Assert(BiomeCatalogue.Classify("modded", new[] { "BlackForestHotDog" }, false).Mask == 0);
            Program.Assert(BiomeCatalogue.Classify("modded", new[] { "Honey", "RoyalJelly" }, true).Mask == 0);
        });
        test("explicit feast association remains authoritative", () =>
            Program.Assert(BiomeCatalogue.Classify("FeastDeepNorth_Material", new[] { "unknown" }, false).Matches(BiomeChoice.DeepNorth)));
        test("stable IDs escape separators and ignore display names", () =>
        {
            string id = RecipeInfo.StableKey("station", "Recipe/a", "item|b");
            Program.Assert(id == "v1/station/Recipe%2Fa/item%7Cb");
            Program.Assert(id != RecipeInfo.StableKey("station", "Recipe", "a/item|b"));
        });
        test("star geometry has five tips and five valleys", () =>
        {
            Program.Assert(StarGeometry.Points.Length == 10);
            for (int i = 0; i < 10; i++)
            {
                StarPoint point = StarGeometry.Points[i];
                double radius = Math.Sqrt(point.X * point.X + point.Y * point.Y);
                Program.Assert(Math.Abs(radius - (i % 2 == 0 ? 1 : 0.45)) < 0.0001);
            }
        });
        GraphTests(test);
    }
    private static RecipeInfo Info(string key, string name, FoodStats food, BiomeChoice biome)
        => new RecipeInfo(key, name, "out", food, NutritionSource.Direct,
            new BiomeAssociation(BiomeAssociation.Bit(biome), biome == BiomeChoice.Unknown ? BiomeSource.Unknown : BiomeSource.Curated));
    private static void GraphTests(Action<string, Action> test)
    {
        test("prepared material uses finished nutrition", () =>
        {
            var graph = new FoodGraph(); graph.AddEdible("Bread", new FoodStats(23, 70, 0)); graph.AddFinish("Dough", "Bread");
            FoodResolution result = graph.Resolve("Dough");
            Program.Assert(result.Source == NutritionSource.Cooked && result.Stats.Stamina == 70);
        });
        test("feast per-serving vector not divided by servings", () =>
        {
            var graph = new FoodGraph(); graph.AddEdible("Feast", new FoodStats(85, 85, 43)); graph.AddFinish("Material", "Feast", true);
            Program.Assert(graph.Resolve("Material").Stats.Eitr == 43 && graph.Resolve("Material").Source == NutritionSource.Feast);
        });
        test("conflicting finishing paths remain ambiguous", () =>
        {
            var graph = new FoodGraph(); graph.AddEdible("A", new FoodStats(80, 26, 0)); graph.AddEdible("B", new FoodStats(26, 80, 0));
            graph.AddFinish("Input", "A"); graph.AddFinish("Input", "B");
            Program.Assert(graph.Resolve("Input").Source == NutritionSource.Ambiguous && graph.Resolve("Input").Stats.Categories == FoodFlags.None);
        });
        test("equivalent finishing paths accepted", () =>
        {
            var graph = new FoodGraph(); graph.AddEdible("A", new FoodStats(50, 50, 0)); graph.AddEdible("B", new FoodStats(50, 50, 0));
            graph.AddFinish("Input", "A"); graph.AddFinish("Input", "B");
            Program.Assert(graph.Resolve("Input").Stats.Health == 50);
        });
        test("missing finishing branch is not ignored", () =>
        {
            var graph = new FoodGraph(); graph.AddEdible("A", new FoodStats(50, 50, 0)); graph.AddFinish("Input", "A"); graph.AddFinish("Input", "missing");
            Program.Assert(graph.Resolve("Input").Stats.Categories == FoodFlags.None);
        });
        test("cycles and depth bound terminate", () =>
        {
            var graph = new FoodGraph(); graph.AddFinish("A", "B"); graph.AddFinish("B", "A");
            Program.Assert(graph.Resolve("A").Stats.Categories == FoodFlags.None);
            for (int i = 0; i < 12; i++) graph.AddFinish("depth" + i, "depth" + (i + 1));
            graph.AddEdible("depth12", new FoodStats(100, 1, 0));
            Program.Assert(graph.Resolve("depth0").Stats.Categories == FoodFlags.None);
        });
        test("cache invalidation after graph data change", () =>
        {
            var graph = new FoodGraph(); graph.AddFinish("input", "food"); graph.Resolve("input");
            graph.AddEdible("food", new FoodStats(10, 20, 0)); Program.Assert(graph.Resolve("input").Stats.Stamina == 20);
        });
        test("finishing graph node budget rejects excessive branching", () =>
        {
            var graph = new FoodGraph();
            for (int i = 0; i < 600; i++)
            { graph.AddEdible("food" + i, new FoodStats(50, 50, 0)); graph.AddFinish("input", "food" + i); }
            Program.Assert(graph.Resolve("input").Source == NutritionSource.Ambiguous && graph.Resolve("input").Stats.Categories == FoodFlags.None);
        });
    }
}
