using CraftyQoL.Core;
namespace CraftyQoL
{
    internal static class InventoryCrafting
    {
        internal static CraftAccess Access
        {
            get
            {
                var player=Player.m_localPlayer;var zone=ZoneSystem.instance;
                return (player && player.NoCostCheat()?CraftAccess.NoCost:0) |
                    (zone && zone.GetGlobalKey(GlobalKeys.NoCraftCost)?CraftAccess.NoCraftCost:0) |
                    (zone && zone.GetGlobalKey(GlobalKeys.AllRecipesUnlocked)?CraftAccess.AllRecipesUnlocked:0);
            }
        }
    }
}
