using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CraftyQoL.Core;

// Offline diagnostics only. Never linked into the plugin or used to populate its UI.
internal static class BiomeAudit
{
    internal static void Export(string input, string output)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(input));
        var rows = doc.RootElement.GetProperty("recipes").EnumerateArray().Select(r =>
        {
            string item = r.GetProperty("output").GetString()!;
            string[] ingredients = r.GetProperty("ingredients").EnumerateArray()
                .Select(i => i.GetProperty("item").GetString() ?? "~unknown").ToArray();
            var biome = BiomeCatalogue.Classify(item, ingredients, (r.GetProperty("onlyOneIngredient").ValueKind == JsonValueKind.True || r.GetProperty("onlyOneIngredient").ToString() == "1"));
            return new { name = r.GetProperty("name").GetString(), output = item, mask = biome.Mask,
                source = biome.Source.ToString(), ambiguous = biome.Ambiguous,
                missingIngredients = ingredients.Where(i => BiomeCatalogue.Ingredient(i).Kind == IngredientKind.Unknown).Distinct().OrderBy(i => i, StringComparer.Ordinal).ToArray(),
                incompleteIngredients = ingredients.Where(i => !BiomeCatalogue.Ingredient(i).Complete).Distinct().OrderBy(i => i, StringComparer.Ordinal).ToArray(),
                ingredientProvenance = ingredients.Distinct().OrderBy(i => i, StringComparer.Ordinal).Select(i =>
                {
                    var origin = BiomeCatalogue.Ingredient(i);
                    return new { item = i, kind = origin.Kind.ToString(), complete = origin.Complete,
                        ambiguous = origin.Ambiguous, evidence = origin.Evidence,
                        biomes = Enum.GetValues<BiomeChoice>().Where(b => (origin.Mask & BiomeAssociation.Bit(b)) != 0).Select(b => b.ToString()).ToArray() };
                }).ToArray(),
                biomes = Enum.GetValues<BiomeChoice>()
                    .Where(b => b != BiomeChoice.All && b != BiomeChoice.Unknown && biome.Matches(b)).Select(b => b.ToString()).ToArray() };
        }).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Offline biome audit: {rows.Length} recipes; {rows.Count(r => r.mask != 0)} known; {rows.Count(r => r.mask == 0)} Unknown.");
    }
}
