namespace CraftyQoL.Core
{
    internal enum FoodCategory { All, Health, Stamina, Eitr }
    internal enum FoodSort { Vanilla, Health, Stamina, Eitr }
    internal enum BiomeChoice { All, Meadows, BlackForest, Swamp, Mountains, Plains, Mistlands, Ashlands, DeepNorth, Ocean, Unknown }
    internal sealed class QueryState
    {
        private readonly bool _equipment,_mead;
        internal readonly bool Inventory;
        internal bool Equipment=>Inventory?Family==RecipeFamily.Equipment:_equipment;
        internal bool Mead=>Inventory?Family==RecipeFamily.Mead:_mead;
        internal readonly InventoryDisclosure Disclosure=new InventoryDisclosure();
        internal RecipeFamily Family { get; set; }
        internal QueryState(bool equipment = false, bool mead = false,bool inventory=false) { _equipment=equipment;_mead=mead;Inventory=inventory; }
        internal EquipmentCategory EquipmentCategory { get; set; }
        internal EquipmentSort EquipmentSort { get; set; }
        internal bool BiomesExpanded { get; set; }
        internal MeadCategory MeadCategory { get; set; }
        internal FoodCategory Category { get; set; }
        internal FoodSort Sort { get; set; }
        internal BiomeChoice Biome { get; set; }
        internal bool FavouritesOnly { get; set; }
        internal string Search { get; set; } = "";
        // Query equality excludes presentation-only disclosure state.
        internal bool SameQuery(QueryState other) => Inventory==other.Inventory && Family==other.Family && Equipment==other.Equipment && Mead==other.Mead &&
            Category==other.Category && MeadCategory==other.MeadCategory && Sort==other.Sort && Biome==other.Biome &&
            EquipmentCategory==other.EquipmentCategory && EquipmentSort==other.EquipmentSort &&
            FavouritesOnly==other.FavouritesOnly && Search==other.Search;
    }

    [System.Flags]
    internal enum FocusMode { None = 0, Toolbar = 1, Text = 2, Released = 4, Tracker = 8, Storage = 16 }

    internal static class InputPolicy
    {
        internal static bool BlockAction(string action, FocusMode mode)
        {
            if (mode == FocusMode.None) return false;
            switch (action)
            {
                case "JoyLStickDown": case "JoyLStickUp": case "JoyDPadDown": case "JoyDPadUp":
                case "JoyButtonB": return true;
                case "JoyTabLeft": case "JoyTabRight":
                    return (mode & (FocusMode.Text | FocusMode.Released | FocusMode.Tracker | FocusMode.Storage)) != 0;
                case "Inventory": case "Use": case "JoyButtonY":
                    return (mode & (FocusMode.Text | FocusMode.Released)) != 0;
                default: return false;
            }
        }
    }
}
