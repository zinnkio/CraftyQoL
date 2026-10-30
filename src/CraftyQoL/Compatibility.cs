using BepInEx.Bootstrap;
using HarmonyLib;

namespace CraftyQoL
{
    internal static class Compatibility
    {
        internal static string? Conflict()
        {
            // Identifiers are public upstream metadata, not implementation dependencies.
            foreach (string id in new[] { "com.inventoryux.valheim", "dev.craftsort" })
                if (Chainloader.PluginInfos.ContainsKey(id)) return "Loaded recipe organiser: " + id;
            Patches? patches = Harmony.GetPatchInfo(GameContracts.RebuildList);
            if (patches == null) return null;
            foreach (Patch prefix in patches.Prefixes)
                if (prefix.owner != Plugin.Id && !InventoryOpeningProfiler.IsOwnListPrefix(prefix)) return "Untested recipe-list prefix: " + prefix.owner;
            foreach (Patch transpiler in patches.Transpilers)
                if (transpiler.owner != Plugin.Id) return "Untested recipe-list transpiler: " + transpiler.owner;
            return null;
        }
    }
}
