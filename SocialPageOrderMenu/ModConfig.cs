using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace SocialPageOrderRedux
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public KeybindList prevButton { get; set; } = KeybindList.Parse($"{SButton.Down}, {SButton.DPadLeft}");
        public KeybindList nextButton { get; set; } = KeybindList.Parse($"{SButton.Up}, {SButton.DPadRight}");
        public bool UseButton { get; set; } = true;
        public bool UseDropdown { get; set; } = true;
        public bool UseFilter { get; set; } = true;
        public int ButtonOffsetX { get; set; } = 0;
        public int ButtonOffsetY { get; set; } = 0;
        public int DropdownOffsetX { get; set; } = 0;
        public int DropdownOffsetY { get; set; } = 0;
        public int FilterOffsetX { get; set; } = 0;
        public int FilterOffsetY { get; set; } = 0;
        public bool SearchBarAutoFocus { get; set; } = true;
    }
}
