using System;
using System.Linq;
using CraftyQoL.Core;

internal static class BiomeAccessTests
{
    internal static void Run(Action<string, Action> test)
    {
        int Bit(BiomeChoice choice) => BiomeAssociation.Bit(choice);
        int meadows = Bit(BiomeChoice.Meadows), mistlands = Bit(BiomeChoice.Mistlands), all = BiomeAccess.KnownBits;
        test("available recipes never establish discovery", () =>
        {
            var options = BiomeAccess.Options(0, all, true);
            Program.Assert(options.Select(x => x.Choice).SequenceEqual(new[] { BiomeChoice.All, BiomeChoice.Unknown }));
            Program.Assert(!BiomeAccess.CanSelect(BiomeChoice.DeepNorth, 0, all, true));
        });
        test("compact palette has no future biome positions", () =>
        {
            var options = BiomeAccess.Options(mistlands, all, false);
            Program.Assert(options.Select(x => x.Choice).SequenceEqual(new[] { BiomeChoice.All, BiomeChoice.Mistlands, BiomeChoice.Unknown }));
        });
        test("discovered biome without available recipes is disabled", () =>
        {
            var option = BiomeAccess.Options(meadows, 0, false).Single(x => x.Choice == BiomeChoice.Meadows);
            Program.Assert(!option.Enabled && !BiomeAccess.CanSelect(option.Choice, meadows, 0, false));
        });
        test("all reset always accessible", () => Program.Assert(BiomeAccess.CanSelect(BiomeChoice.All, 0, 0, false)));
        test("unknown association is neutral metadata category", () =>
        {
            Program.Assert(BiomeAccess.CanSelect(BiomeChoice.Unknown, 0, 0, true));
            Program.Assert(!BiomeAccess.CanSelect(BiomeChoice.Unknown, all, all, false));
        });
        test("lost discovery or recipes resets selected biome", () =>
        {
            Program.Assert(BiomeAccess.ValidSelection(BiomeChoice.Meadows, 0, meadows, true) == BiomeChoice.All);
            Program.Assert(BiomeAccess.ValidSelection(BiomeChoice.Meadows, meadows, 0, true) == BiomeChoice.All);
            Program.Assert(BiomeAccess.ValidSelection(BiomeChoice.Meadows, meadows, meadows, false) == BiomeChoice.Meadows);
        });
        test("mixed association reveal mask excludes undiscovered bits", () =>
            Program.Assert(BiomeAccess.Revealed(meadows | mistlands, meadows) == meadows));
        test("biome tooltip identity requires actual discovery, not available recipes", () =>
        {
            for(int choice=1;choice<=9;choice++)
            {
                var biome=(BiomeChoice)choice;
                Program.Assert(!BiomeAccess.IsDiscovered(biome,0));
                Program.Assert(BiomeAccess.IsDiscovered(biome,Bit(biome)));
                Program.Assert(!BiomeAccess.Options(0,all,true).Any(o=>o.Choice==biome));
                Program.Assert(BiomeAccess.Options(Bit(biome),0,false).Any(o=>o.Choice==biome && !o.Enabled));
            }
            Program.Assert(!BiomeAccess.IsDiscovered(BiomeChoice.All,all) && !BiomeAccess.IsDiscovered(BiomeChoice.Unknown,all));
            Program.Assert(!BiomeAccess.IsDiscovered((BiomeChoice)99,-1));
        });
        test("tooltip disclosure across every discovery mask contains no future biome", () =>
        {
            for(int flags=0;flags<512;flags++)
            {
                int discovered=flags<<1;
                for(int choice=1;choice<=9;choice++)
                    Program.Assert(BiomeAccess.IsDiscovered((BiomeChoice)choice,discovered)==((discovered & Bit((BiomeChoice)choice))!=0));
                foreach(var option in BiomeAccess.Options(discovered,all,true))
                    if(option.Choice!=BiomeChoice.All && option.Choice!=BiomeChoice.Unknown)
                        Program.Assert(BiomeAccess.IsDiscovered(option.Choice,discovered));
            }
        });
        test("new enum bits fail closed", () =>
        {
            Program.Assert(!BiomeAccess.CanSelect((BiomeChoice)99, -1, -1, true));
            Program.Assert(BiomeAccess.Revealed(1 << 25, -1) == 0);
        });
        test("all discovery masks retain exactly confirmed biome identities", () =>
        {
            for (int flags = 0; flags < 512; flags++)
            {
                int discovered = flags << 1;
                var options = BiomeAccess.Options(discovered, all, false);
                int actual = 0;
                foreach (var option in options)
                    if (option.Choice != BiomeChoice.All && option.Choice != BiomeChoice.Unknown)
                    { actual |= Bit(option.Choice); Program.Assert(option.Enabled); }
                Program.Assert(actual == discovered);
            }
        });
        test("independent discovery and availability truth table", () =>
        {
            for (int choice = 1; choice <= 9; choice++)
                foreach (bool known in new[] { false, true })
                    foreach (bool available in new[] { false, true })
                        Program.Assert(BiomeAccess.CanSelect((BiomeChoice)choice, known ? all : 0, available ? all : 0, false) == (known && available));
        });
    }
}
