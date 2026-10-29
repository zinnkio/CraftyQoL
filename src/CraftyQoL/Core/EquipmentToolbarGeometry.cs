using System;

namespace CraftyQoL.Core
{
    internal readonly struct EquipmentToolbarGeometry
    {
        internal const float IconSize = 18;
        internal const float CategoryY = ToolbarGeometry.OptionsY;
        internal const float HeaderY = CategoryY + 2 * (ToolbarGeometry.ControlHeight + ToolbarGeometry.Gap);
        internal const float PaletteY = HeaderY + ToolbarGeometry.ControlHeight + ToolbarGeometry.Gap;
        private readonly ToolbarGeometry _shared;
        internal EquipmentToolbarGeometry(float width) { _shared = new ToolbarGeometry(width); }
        internal UiBox Category(int index)
        {
            if (index < 0 || index >= 8) throw new ArgumentOutOfRangeException(nameof(index));
            float width = (_shared.ContentWidth - 3 * ToolbarGeometry.Gap) / 4;
            return new UiBox(ToolbarGeometry.Outer + index % 4 * (width + ToolbarGeometry.Gap),
                CategoryY + index / 4 * (ToolbarGeometry.ControlHeight + ToolbarGeometry.Gap), width, ToolbarGeometry.ControlHeight);
        }
        internal UiBox PotionCategory(int index)
        {
            if(index<0 || index>=6)throw new ArgumentOutOfRangeException(nameof(index));
            float width=(_shared.ContentWidth-2*ToolbarGeometry.Gap)/3;
            return new UiBox(ToolbarGeometry.Outer+index%3*(width+ToolbarGeometry.Gap),CategoryY+index/3*(ToolbarGeometry.ControlHeight+ToolbarGeometry.Gap),width,ToolbarGeometry.ControlHeight);
        }
        internal UiBox Sort(int index) => new UiBox(ToolbarGeometry.Outer + index * (ToolbarGeometry.SmallButton + ToolbarGeometry.Gap), HeaderY,
            ToolbarGeometry.SmallButton, ToolbarGeometry.SmallButton);
        internal UiBox Disclosure => new UiBox(Sort(1).Right + ToolbarGeometry.Gap, HeaderY,
            _shared.Reset.X - Sort(1).Right - 2 * ToolbarGeometry.Gap, ToolbarGeometry.ControlHeight);
        internal UiBox Reset => new UiBox(_shared.Reset.X, HeaderY, _shared.Reset.Width, _shared.Reset.Height);
        internal UiBox Biome(int index)
        { UiBox box = _shared.Biome(index); return new UiBox(box.X, PaletteY + box.Y - ToolbarGeometry.PaletteY, box.Width, box.Height); }
        internal float SeparatorY(bool expanded) => expanded ? Biome(9).Bottom + ToolbarGeometry.Gap : PaletteY;
        internal float Height(bool expanded) => SeparatorY(expanded) + ToolbarGeometry.Border + ToolbarGeometry.Outer;
    }
}
