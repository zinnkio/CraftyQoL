using System;

namespace CraftyQoL.Core
{
    internal readonly struct UiBox
    {
        internal readonly float X, Y, Width, Height;
        internal UiBox(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        internal float Right => X + Width;
        internal float Bottom => Y + Height;
    }
    internal readonly struct ToolbarGeometry
    {
        internal const float MinimumWidth = 170, Outer = 2, Gap = 3, OptionGap = 2, GroupGap = 6, Border = 1;
        internal const float ControlHeight = 22, SmallButton = 22, CaptionHeight = 10, ResetWidth = 30, BiomeSquare = 24;
        internal const int Columns = 5, PaletteSlots = 10;
        internal const float SearchY = Outer, CaptionY = SearchY + ControlHeight + Gap;
        internal const float OptionsY = CaptionY + CaptionHeight + 1, HeaderY = OptionsY + ControlHeight + Gap;
        internal const float PaletteY = HeaderY + ControlHeight + Gap;
        internal const float SeparatorY = PaletteY + BiomeSquare * 2 + Gap * 2;
        internal const float Height = SeparatorY + Border + Outer;
        internal readonly float Width;
        internal ToolbarGeometry(float width)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width < MinimumWidth) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
        }
        internal float ContentWidth => Width - Outer * 2;
        internal float GroupWidth => (ContentWidth - GroupGap) / 2;
        internal float OptionWidth => (GroupWidth - OptionGap * 3) / 4;
        internal float SelectionSize => Math.Min(ControlHeight, OptionWidth);
        internal UiBox Search => new UiBox(Outer, SearchY, ContentWidth - SmallButton * 2 - Gap * 2, ControlHeight);
        internal UiBox Clear => new UiBox(Favourites.X - Gap - SmallButton, SearchY, SmallButton, ControlHeight);
        internal UiBox Favourites => new UiBox(Width - Outer - SmallButton, SearchY, SmallButton, ControlHeight);
        internal UiBox TypeCaption => new UiBox(Outer, CaptionY, GroupWidth, CaptionHeight);
        internal UiBox SortCaption => new UiBox(Outer + GroupWidth + GroupGap, CaptionY, GroupWidth, CaptionHeight);
        internal UiBox BiomeCaption => new UiBox(Outer, HeaderY + (ControlHeight - CaptionHeight) / 2, ContentWidth - ResetWidth - Gap, CaptionHeight);
        internal UiBox Reset => new UiBox(Width - Outer - ResetWidth, HeaderY, ResetWidth, ControlHeight);
        internal UiBox Separator => new UiBox(Outer, SeparatorY, ContentWidth, Border);
        internal UiBox Option(int index, bool sort)
        {
            if (index < 0 || index >= 4) throw new ArgumentOutOfRangeException(nameof(index));
            float inset = (GroupWidth - (SelectionSize * 4 + OptionGap * 3)) / 2;
            return new UiBox(Outer + (sort ? GroupWidth + GroupGap : 0) + inset + index * (SelectionSize + OptionGap),
                OptionsY + (ControlHeight - SelectionSize) / 2, SelectionSize, SelectionSize);
        }
        internal UiBox AlphabeticalOption(int index)
        {
            if(index<0 || index>1)throw new ArgumentOutOfRangeException(nameof(index));
            return new UiBox(SortCaption.X+(GroupWidth-2*SelectionSize-OptionGap)/2+index*(SelectionSize+OptionGap),OptionsY+(ControlHeight-SelectionSize)/2,SelectionSize,SelectionSize);
        }
        internal UiBox FoodDisclosure => new UiBox(Outer,HeaderY,ContentWidth-ResetWidth-Gap,ControlHeight);
        internal static float FoodSeparatorY(bool expanded)=>expanded?SeparatorY:HeaderY+ControlHeight+Gap;
        internal static float FoodHeight(bool expanded)=>FoodSeparatorY(expanded)+Border+Outer;
        internal UiBox Biome(int index)
        {
            if (index < 0 || index >= PaletteSlots) throw new ArgumentOutOfRangeException(nameof(index));
            float pitch = (ContentWidth - SelectionSize) / (Columns - 1);
            return new UiBox(Outer + index % Columns * pitch, PaletteY + index / Columns * (SelectionSize + Gap), SelectionSize, SelectionSize);
        }
    }
}
