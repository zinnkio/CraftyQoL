using System;

namespace CraftyQoL.Core
{
    internal static class StationRules
    {
        // Input must already be the exact registered prefab identifier.
        internal static bool IsSupported(string? prefab)
            => string.Equals(prefab, "piece_cauldron", StringComparison.Ordinal)
               || string.Equals(prefab, "piece_preptable", StringComparison.Ordinal)
               || IsEquipment(prefab) || IsMead(prefab);
        internal static bool IsEquipment(string? prefab)
            => string.Equals(prefab, "piece_workbench", StringComparison.Ordinal) || string.Equals(prefab, "forge", StringComparison.Ordinal)
               || string.Equals(prefab, "blackforge", StringComparison.Ordinal)
               || string.Equals(prefab, "piece_magetable", StringComparison.Ordinal);
        internal static bool IsMead(string? prefab) => string.Equals(prefab,"piece_MeadCauldron",StringComparison.Ordinal);
        internal static bool IsActive(string? prefab, bool craftTab) => craftTab && IsSupported(prefab);
    }
}
