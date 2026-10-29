using System.Collections.Generic;

namespace CraftyQoL.Core
{
    internal readonly struct BiomeOption
    {
        internal readonly BiomeChoice Choice;
        internal readonly bool Enabled;
        internal BiomeOption(BiomeChoice choice, bool enabled) { Choice = choice; Enabled = enabled; }
    }
    internal static class BiomeAccess
    {
        internal const int KnownBits = (1 << 10) - 2;
        internal static int Revealed(int association, int discovered) => association & discovered & KnownBits;
        internal static bool IsDiscovered(BiomeChoice choice, int discovered)
        {
            int index = (int)choice;
            return index >= 1 && index <= 9 && Revealed(BiomeAssociation.Bit(choice), discovered) != 0;
        }
        internal static bool CanSelect(BiomeChoice choice, int discovered, int available, bool unknown)
        {
            if (choice == BiomeChoice.All) return true;
            if (choice == BiomeChoice.Unknown) return unknown;
            int index = (int)choice;
            return index >= 1 && index <= 9 && (Revealed(available, discovered) & BiomeAssociation.Bit(choice)) != 0;
        }
        internal static BiomeChoice ValidSelection(BiomeChoice choice, int discovered, int available, bool unknown)
            => CanSelect(choice, discovered, available, unknown) ? choice : BiomeChoice.All;
        internal static List<BiomeOption> Options(int discovered, int available, bool unknown)
        {
            var options = new List<BiomeOption>(11) { new BiomeOption(BiomeChoice.All, true) };
            for (int i = 1; i <= 9; i++)
            {
                var choice = (BiomeChoice)i;
                if (IsDiscovered(choice, discovered))
                    options.Add(new BiomeOption(choice, CanSelect(choice, discovered, available, unknown)));
            }
            options.Add(new BiomeOption(BiomeChoice.Unknown, unknown));
            return options;
        }
    }
}
