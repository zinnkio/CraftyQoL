using System;
using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal readonly struct FoodResolution
    {
        internal readonly FoodStats Stats;
        internal readonly NutritionSource Source;
        internal FoodResolution(FoodStats stats, NutritionSource source) { Stats = stats; Source = source; }
    }
    internal sealed class FoodGraph
    {
        private readonly Dictionary<string, FoodStats> _edible = new Dictionary<string, FoodStats>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _finishes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly HashSet<string> _feasts = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, FoodResolution> _cache = new Dictionary<string, FoodResolution>(StringComparer.Ordinal);
        internal void AddEdible(string name, FoodStats stats) { _edible[name] = stats; _cache.Clear(); }
        internal void AddFinish(string input, string output, bool feast = false)
        {
            if (!_finishes.TryGetValue(input, out HashSet<string>? destinations) || destinations == null)
                _finishes.Add(input, destinations = new HashSet<string>(StringComparer.Ordinal));
            destinations.Add(output);
            if (feast) _feasts.Add(input);
            _cache.Clear();
        }
        internal FoodResolution Resolve(string name)
        {
            if (_cache.TryGetValue(name, out FoodResolution cached)) return cached;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var terminals = new List<FoodStats>();
            bool feast = false;
            int budget = 512;
            bool valid = Walk(name, 0, visited, terminals, ref feast, ref budget);
            NutritionSource source = !valid ? (terminals.Count > 0 || visited.Count > 0 ? NutritionSource.Ambiguous : NutritionSource.Unknown) :
                _edible.ContainsKey(name) ? NutritionSource.Direct : feast ? NutritionSource.Feast : NutritionSource.Cooked;
            FoodStats stats = valid && terminals.Count > 0 ? terminals[0] : default;
            if (valid)
            {
                for (int i = 1; i < terminals.Count; i++)
                    if (!stats.Equals(terminals[i])) { source = NutritionSource.Ambiguous; stats = default; break; }
            }
            var result = new FoodResolution(stats, source);
            _cache[name] = result;
            return result;
        }
        private bool Walk(string name, int depth, HashSet<string> visiting, List<FoodStats> terminals, ref bool feast, ref int budget)
        {
            if (--budget < 0) return false;
            if (_edible.TryGetValue(name, out FoodStats food)) { terminals.Add(food); return true; }
            if (depth >= 8 || !visiting.Add(name)) return false;
            if (!_finishes.TryGetValue(name, out HashSet<string>? next) || next == null || next.Count == 0)
            { visiting.Remove(name); return false; }
            if (_feasts.Contains(name)) feast = true;
            bool valid = true;
            foreach (string target in next)
            {
                valid &= Walk(target, depth + 1, visiting, terminals, ref feast, ref budget);
                if (budget < 0) break;
            }
            visiting.Remove(name);
            return valid;
        }
    }
}
