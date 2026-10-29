using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal static class StationScope
    {
        internal static string? Identify(InventoryGui gui)
        {
            if (!gui || !Player.m_localPlayer || !ZNetScene.instance) return null;
            CraftingStation station = Player.m_localPlayer.GetCurrentCraftingStation();
            if (!gui.InCraftTab() || !gui.m_recipeListRoot || !gui.m_recipeElementPrefab || !gui.m_crafting) return null;
            if (!station) return InventoryRules.Origin;
            string id = Utils.GetPrefabName(station.gameObject);
            if (!StationRules.IsActive(id, gui.InCraftTab())) return null;
            GameObject prefab = ZNetScene.instance.GetPrefab(id);
            CraftingStation registered = prefab ? prefab.GetComponent<CraftingStation>() : null!;
            if (!registered || registered.m_name != station.m_name) return null;
            if (!gui.m_recipeListRoot || !gui.m_recipeElementPrefab || !gui.m_crafting) return null;
            return id;
        }
    }
}
