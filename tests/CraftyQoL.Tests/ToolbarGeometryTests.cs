using System;
using System.Collections.Generic;
using CraftyQoL.Core;

internal static class ToolbarGeometryTests
{
    internal static void Run(Action<string, Action> test)
    {
        bool Near(float a, float b) => Math.Abs(a - b) < 0.001;
        test("top row controls align and fill available width", () =>
        {
            foreach (float width in new[] { 170f, 187f, 256f })
            {
                var g = new ToolbarGeometry(width);
                Program.Assert(Near(g.Search.Y, g.Clear.Y) && Near(g.Clear.Y, g.Favourites.Y));
                Program.Assert(Near(g.Search.Height, g.Clear.Height) && Near(g.Clear.Height, g.Favourites.Height));
                Program.Assert(Near(g.Clear.X - g.Search.Right, ToolbarGeometry.Gap) && Near(g.Favourites.X - g.Clear.Right, ToolbarGeometry.Gap));
                Program.Assert(Near(g.Favourites.Right, width - ToolbarGeometry.Outer));
            }
        });
        test("groups have equal options and consistent spacing", () =>
        {
            var g = new ToolbarGeometry(187);
            for (int i = 0; i < 4; i++)
            {
                Program.Assert(Near(g.Option(i, false).Width, g.Option(i, true).Width));
                Program.Assert(Near(g.Option(i, false).Height, g.SelectionSize));
                if (i > 0) Program.Assert(Near(g.Option(i, false).X - g.Option(i - 1, false).Right, ToolbarGeometry.OptionGap));
            }
        });
        test("palette has five aligned square columns and two rows", () =>
        {
            var g = new ToolbarGeometry(187);
            for (int i = 0; i < 5; i++)
            {
                Program.Assert(Near(g.Biome(i).X, g.Biome(i + 5).X));
                Program.Assert(Near(g.Biome(i).Width, g.Biome(i).Height));
                Program.Assert(Near(g.Biome(i + 5).Y - g.Biome(i).Bottom, ToolbarGeometry.Gap));
            }
            Program.Assert(Near(g.Biome(0).X, ToolbarGeometry.Outer) && Near(g.Biome(4).Right, 187 - ToolbarGeometry.Outer));
        });
        test("supported widths keep every control in bounds without overlap", () =>
        {
            for (int width = 170; width <= 384; width++)
            {
                var g = new ToolbarGeometry(width);
                for (int option = 0; option < 4; option++)
                    foreach (bool sort in new[] { false, true })
                    {
                        UiBox button = g.Option(option, sort);
                        Program.Assert(Near(button.Width, button.Height));
                        for (int biome = 0; biome < 10; biome++)
                            Program.Assert(Near(button.Width, g.Biome(biome).Width) && Near(button.Height, g.Biome(biome).Height), "Selection buttons differ in size");
                    }
                var boxes = new List<UiBox> { g.Search, g.Clear, g.Favourites, g.TypeCaption, g.SortCaption, g.BiomeCaption, g.Reset, g.Separator };
                for (int i = 0; i < 4; i++) { boxes.Add(g.Option(i, false)); boxes.Add(g.Option(i, true)); }
                for (int i = 0; i < 10; i++) boxes.Add(g.Biome(i));
                for (int i = 0; i < boxes.Count; i++)
                {
                    UiBox a = boxes[i];
                    Program.Assert(a.Width > 0 && a.Height > 0 && a.X >= 0 && a.Y >= 0 && a.Right <= width + 0.001 && a.Bottom <= ToolbarGeometry.Height);
                    for (int j = 0; j < i; j++)
                    {
                        UiBox b = boxes[j];
                        Program.Assert(a.Right <= b.X + 0.001 || b.Right <= a.X + 0.001 || a.Bottom <= b.Y + 0.001 || b.Bottom <= a.Y + 0.001, "Overlapping control rectangles");
                    }
                }
                Program.Assert(g.Separator.Y - g.Biome(9).Bottom >= ToolbarGeometry.Gap);
            }
        });
        test("food disclosure preserves square geometry and returns collapsed viewport space",()=>
        {
            foreach(float width in new[]{170f,187f,256f,384f})
            {
                var g=new ToolbarGeometry(width);
                Program.Assert(g.FoodDisclosure.Right+ToolbarGeometry.Gap<=g.Reset.X+0.001f);
                Program.Assert(g.FoodDisclosure.Y==g.Reset.Y && g.FoodDisclosure.Height==g.Reset.Height);
                Program.Assert(ToolbarGeometry.FoodHeight(false)<ToolbarGeometry.FoodHeight(true));
                Program.Assert(ToolbarGeometry.FoodHeight(true)==ToolbarGeometry.Height);
                Program.Assert(ToolbarGeometry.FoodSeparatorY(false)>=g.Reset.Bottom+ToolbarGeometry.Gap);
                Program.Assert(ToolbarGeometry.FoodSeparatorY(true)>=g.Biome(9).Bottom+ToolbarGeometry.Gap);
            }
            var state=new QueryState{Biome=BiomeChoice.Swamp};
            Program.Assert(!state.BiomesExpanded);state.BiomesExpanded=true;state.BiomesExpanded=false;
            Program.Assert(state.Biome==BiomeChoice.Swamp);
        });
        test("potion icon groups fit six categories without biome rows",()=>
        {
            foreach(float width in new[]{170f,187f,256f})
            {
                var g=new EquipmentToolbarGeometry(width);
                for(int i=0;i<6;i++)
                {
                    UiBox box=g.PotionCategory(i);Program.Assert(box.X>=0 && box.Right<=width && box.Bottom<g.Sort(0).Y);
                    if(i%3>0)Program.Assert(box.X>=g.PotionCategory(i-1).Right+ToolbarGeometry.Gap-0.001f);
                }
            }
        });
        test("invalid widths fail safely", () =>
        {
            foreach (float width in new[] { 0, 169, float.NaN, float.PositiveInfinity })
            {
                bool failed = false; try { _ = new ToolbarGeometry(width); } catch (ArgumentOutOfRangeException) { failed = true; }
                Program.Assert(failed);
            }
        });
    }
}
