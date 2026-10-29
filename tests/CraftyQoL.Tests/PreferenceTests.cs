using System;
using System.IO;
using System.Linq;
using CraftyQoL.Core;

internal static class PreferenceTests
{
    internal static void Run(Action<string, Action> check)
    {
        void Case(string name, Action<string> body) => check(name, () =>
        {
            // A unique test-owned directory; delete only files created by this case.
            string directory = Path.Combine(Path.GetTempPath(), "CraftyQoL.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { body(Path.Combine(directory, "favourites.json")); }
            finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        });
        FavouriteStore Store(string path) => new FavouriteStore(path, _ => { });
        Case("favourites round trip and removal", path =>
        {
            FavouriteStore store = Store(path);
            string key = RecipeInfo.StableKey("piece_cauldron", "Recipe_A/B", "Mødded");
            Program.Assert(store.Toggle(key) && store.Save());
            Program.Assert(Store(path).Keys.SetEquals(new[] { key }));
            Program.Assert(store.Toggle(key) && store.Save() && Store(path).Keys.Count == 0);
        });
        Case("food and equipment favourites share unchanged v1 persistence", path =>
        {
            string food = RecipeInfo.StableKey("piece_cauldron", "Recipe_BoarJerky", "BoarJerky");
            string bench = RecipeInfo.StableKey("piece_workbench", "Recipe_AxeFlint", "AxeFlint");
            string forge = RecipeInfo.StableKey("forge", "Recipe_SwordIron", "SwordIron");
            string black = RecipeInfo.StableKey("blackforge", "Recipe_BoltCarapace", "BoltCarapace");
            string galdr = RecipeInfo.StableKey("piece_magetable", "Recipe_StaffFireball", "StaffFireball");
            FavouriteStore old = Store(path); old.Toggle(food); Program.Assert(old.Save());
            FavouriteStore updated = Store(path); updated.Toggle(bench); updated.Toggle(forge); updated.Toggle(black); updated.Toggle(galdr); Program.Assert(updated.Save());
            Program.Assert(Store(path).Keys.SetEquals(new[] { food, bench, forge, black, galdr }));
            updated.Toggle(bench); Program.Assert(updated.Save() && Store(path).Keys.SetEquals(new[] { food, forge, black, galdr }));
        });
        Case("unavailable favourites retained across restart", path =>
        {
            FavouriteStore store = Store(path);
            store.Toggle("v1/station/removedmod/item"); store.Toggle("v1/station/present/item"); store.Save();
            Program.Assert(Store(path).Keys.Count == 2); // No dependency on installed recipe presence.
        });
        Case("deterministic sorted persistence and backup", path =>
        {
            FavouriteStore store = Store(path);
            store.Toggle("z"); store.Toggle("a"); store.Save();
            Program.Assert(PreferenceFiles.Read<FavouriteDocument>(path).Recipes.SequenceEqual(new[] { "a", "z" }));
            store.Toggle("next"); store.Save();
            Program.Assert(PreferenceFiles.Read<FavouriteDocument>(path + ".bak").Recipes.SequenceEqual(new[] { "a", "z" }));
        });
        Case("damaged main recovers backup without destroying evidence", path =>
        {
            PreferenceFiles.Write(path, new FavouriteDocument { Recipes = new[] { "good" } }, false);
            PreferenceFiles.Write(path, new FavouriteDocument { Recipes = new[] { "new" } }, false);
            File.WriteAllText(path, "{damaged");
            FavouriteStore store = Store(path);
            Program.Assert(store.Keys.SetEquals(new[] { "good" }));
            store.Toggle("extra"); Program.Assert(store.Save());
            Program.Assert(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.corrupt.*").Length == 1);
            Program.Assert(PreferenceFiles.Read<FavouriteDocument>(path + ".bak").Recipes.Single() == "good");
        });
        Case("damaged file without backup is never overwritten", path =>
        {
            File.WriteAllText(path, "invalid original");
            FavouriteStore store = Store(path); store.Toggle("in-memory");
            Program.Assert(!store.Save() && store.SaveFailed && !store.Dirty);
            Program.Assert(store.Keys.Contains("in-memory") && File.ReadAllText(path) == "invalid original");
        });
        Case("unknown favourite schema preserved", path =>
        {
            PreferenceFiles.Write(path, new FavouriteDocument { Version = 999, Recipes = new[] { "future" } }, false);
            string original = File.ReadAllText(path);
            FavouriteStore store = Store(path); store.Toggle("local");
            Program.Assert(!store.Save() && File.ReadAllText(path) == original);
        });
        Case("backup survives missing main", path =>
        {
            PreferenceFiles.Write(path + ".bak", new FavouriteDocument { Recipes = new[] { "backup" } }, false);
            FavouriteStore store = Store(path);
            Program.Assert(store.Keys.Contains("backup")); store.Toggle("extra");
            Program.Assert(store.Save() && Store(path).Keys.Count == 2);
        });
        Case("invalid and duplicate favourite IDs bounded", path =>
        {
            PreferenceFiles.Write(path, new FavouriteDocument { Recipes = new[] { "same", "same", "", new string('x', 1025) } }, false);
            FavouriteStore store = Store(path);
            Program.Assert(store.Keys.SetEquals(new[] { "same" }));
            Program.Assert(!store.Toggle("") && !store.Toggle(new string('x', 1025)));
        });
        Case("favourite count bound does not prevent removal", path =>
        {
            FavouriteStore store = Store(path);
            for (int i = 0; i < 4096; i++) store.Toggle("recipe" + i);
            Program.Assert(!store.Toggle("overflow") && store.Keys.Count == 4096);
            Program.Assert(store.Toggle("recipe0") && store.Toggle("replacement"));
        });
        Case("write failure stays in memory and does not flood retries", path =>
        {
            File.WriteAllText(path, "blocks directory");
            int notices = 0;
            var store = new FavouriteStore(Path.Combine(path, "child.json"), _ => notices++);
            store.Toggle("retained");
            Program.Assert(!store.Save() && !store.Dirty && store.Keys.Contains("retained"));
            int first = notices; store.Save(); store.Save(); Program.Assert(notices == first);
        });
        Case("oversize favourites preserved", path =>
        {
            File.WriteAllText(path, new string('x', PreferenceFiles.MaxBytes + 1));
            FavouriteStore store = Store(path); store.Toggle("memory");
            Program.Assert(!store.Save() && new FileInfo(path).Length > PreferenceFiles.MaxBytes);
        });
        Case("mixed biome override is explicit", path =>
        {
            PreferenceFiles.Write(path, new BiomeOverrideDocument { Recipes = new[] { new BiomeOverrideEntry { Key = "key", Biomes = new[] { "Meadows", "Swamp" } } } }, false);
            BiomeAssociation value = BiomeOverrides.Load(path, _ => { })["key"];
            Program.Assert(value.Source == BiomeSource.Override && value.Matches(BiomeChoice.Meadows) && value.Matches(BiomeChoice.Swamp) && !value.Matches(BiomeChoice.Plains));
        });
        Case("explicit unknown biome override", path =>
        {
            PreferenceFiles.Write(path, new BiomeOverrideDocument { Recipes = new[] { new BiomeOverrideEntry { Key = "key", Biomes = new[] { "Unknown" } } } }, false);
            Program.Assert(BiomeOverrides.Load(path, _ => { })["key"].Matches(BiomeChoice.Unknown));
        });
        Case("invalid biome override rejected atomically", path =>
        {
            PreferenceFiles.Write(path, new BiomeOverrideDocument { Recipes = new[] {
                new BiomeOverrideEntry { Key = "valid", Biomes = new[] { "Meadows" } },
                new BiomeOverrideEntry { Key = "bad", Biomes = new[] { "InventedBiome" } } } }, false);
            int notices = 0; string original = File.ReadAllText(path);
            Program.Assert(BiomeOverrides.Load(path, _ => notices++).Count == 0 && notices == 1 && File.ReadAllText(path) == original);
        });
    }
}
