using System;
using System.Collections.Generic;
using System.Diagnostics;
using CraftyQoL.Core;

internal static class QueryBenchmark
{
    internal static void Run()
    {
        var input = new List<RecipeInfo>();
        for (int i = 0; i < 1000; i++)
            input.Add(new RecipeInfo("key" + i.ToString("D4"), "Food " + i, "output",
                new FoodStats(i % 137, i % 59, i % 17), NutritionSource.Direct,
                new BiomeAssociation(BiomeAssociation.Bit(BiomeChoice.Meadows), BiomeSource.Curated)));
        var query = new QueryState { Sort = FoodSort.Health };
        var favourites = new HashSet<string>(StringComparer.Ordinal);
        var ticks = new long[1000];
        long totalBytes = 0;
        for (int i = 0; i < 1100; i++)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            List<RecipeInfo> result = RecipeQuery.Filter(input, query, favourites);
            RecipeQuery.Sort(result, query.Sort);
            long elapsed = Stopwatch.GetTimestamp() - start;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            Program.Assert(result.Count == 1000);
            if (i >= 100) { ticks[i - 100] = elapsed; totalBytes += bytes; }
        }
        Array.Sort(ticks);
        double p95 = ticks[949] * 1000.0 / Stopwatch.Frequency;
        Console.WriteLine($"Pure query only: 1000 records, 100 warmups, 1000 samples; p95 {p95:F3} ms; mean allocation {totalBytes / 1000} bytes/query; .NET {Environment.Version}; {Environment.OSVersion}.");
        Program.Assert(p95 <= 2.0, "Pure query p95 exceeded research target");
        Program.Assert(totalBytes / 1000 <= 16384, "Pure query allocation exceeded research target");
    }
}
