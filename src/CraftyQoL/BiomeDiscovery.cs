using CraftyQoL.Core;
using UnityEngine;

namespace CraftyQoL
{
    internal static class BiomeDiscovery
    {
        private static BiomeSector[]? _probes;
        private static bool _warned;
        internal static Heightmap.Biome GameBiome(BiomeChoice choice)
        {
            switch (choice)
            {
                case BiomeChoice.Meadows: return Heightmap.Biome.Meadows;
                case BiomeChoice.BlackForest: return Heightmap.Biome.BlackForest;
                case BiomeChoice.Swamp: return Heightmap.Biome.Swamp;
                case BiomeChoice.Mountains: return Heightmap.Biome.Mountain;
                case BiomeChoice.Plains: return Heightmap.Biome.Plains;
                case BiomeChoice.Mistlands: return Heightmap.Biome.Mistlands;
                case BiomeChoice.Ashlands: return Heightmap.Biome.AshLands;
                case BiomeChoice.DeepNorth: return Heightmap.Biome.DeepNorth;
                case BiomeChoice.Ocean: return Heightmap.Biome.Ocean;
                default: return Heightmap.Biome.None;
            }
        }
        internal static int Read()
        {
            if (!Player.m_localPlayer || Localization.instance == null) return 0;
            try
            {
                if (_probes == null)
                {
                    var probes = new BiomeSector[9];
                    for (int i = 0; i < probes.Length; i++) probes[i] = new BiomeSector(null!, GameBiome((BiomeChoice)(i + 1)));
                    _probes = probes;
                }
                int mask = 0;
                for (int i = 0; i < _probes.Length; i++)
                    if (Player.m_localPlayer.IsBiomeKnown(_probes[i])) mask |= BiomeAssociation.Bit((BiomeChoice)(i + 1));
                return mask;
            }
            catch (System.Exception)
            {
                if (!_warned) { _warned = true; Plugin.Instance?.DiscoveryUnavailable(); }
                return 0; // No recipe/progression/name-guessing fallback.
            }
        }
        internal static string Name(BiomeChoice choice)
        {
            if (choice == BiomeChoice.All) return "All biomes";
            if (choice == BiomeChoice.Unknown) return "Misc";
            return Localization.instance != null ? Localization.instance.Localize(BiomeSector.GetBiomeName(GameBiome(choice))) : "Biome";
        }
        internal static Color Colour(BiomeChoice choice)
        {
            switch (choice)
            {
                case BiomeChoice.Meadows: return new Color(0.38f, 0.64f, 0.25f);
                case BiomeChoice.BlackForest: return new Color(0.12f, 0.32f, 0.27f);
                case BiomeChoice.Swamp: return new Color(0.44f, 0.36f, 0.15f);
                case BiomeChoice.Mountains: return new Color(0.72f, 0.83f, 0.9f);
                case BiomeChoice.Plains: return new Color(0.83f, 0.65f, 0.27f);
                case BiomeChoice.Mistlands: return new Color(0.51f, 0.43f, 0.71f);
                case BiomeChoice.Ashlands: return new Color(0.7f, 0.18f, 0.12f);
                case BiomeChoice.DeepNorth: return new Color(0.24f, 0.67f, 0.78f);
                case BiomeChoice.Ocean: return new Color(0.15f, 0.34f, 0.69f);
                default: return UiStyle.Neutral;
            }
        }
    }
}
