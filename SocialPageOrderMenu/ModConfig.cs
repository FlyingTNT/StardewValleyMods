using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace SocialPageOrderRedux
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public KeybindList prevButton { get; set; } = KeybindList.Parse($"{SButton.Down}, {SButton.DPadLeft}");
        public KeybindList nextButton { get; set; } = KeybindList.Parse($"{SButton.Up}, {SButton.DPadRight}");
        public KeybindList toggleShowGifted { get; set; } = KeybindList.Parse($"{SButton.G}");
        public KeybindList toggleShowTalked { get; set; } = KeybindList.Parse($"{SButton.H}");
        public KeybindList toggleShowMaxFriendship { get; set; } = KeybindList.Parse($"");
        public KeybindList toggleShowUnmet { get; set; } = KeybindList.Parse($"");
        public bool UseButton { get; set; } = true;
        public bool UseDropdown { get; set; } = true;
        public bool UseFilter { get; set; } = true;
        public bool UseShowTalked { get; set; } = true;
        public bool UseShowGifted { get; set; } = true;
        public bool UseShowMaxFriendship { get; set; } = true;
        public bool UseShowUnmet { get; set; } = true;
        public int ButtonOffsetX { get; set; } = 0;
        public int ButtonOffsetY { get; set; } = 0;
        public int DropdownOffsetX { get; set; } = 0;
        public int DropdownOffsetY { get; set; } = 0;
        public int FilterOffsetX { get; set; } = 0;
        public int FilterOffsetY { get; set; } = 0;
        public int TalkedOffsetX {  get; set; } = 0;
        public int TalkedOffsetY { get; set; } = 0;
        public bool SearchBarAutoFocus { get; set; } = true;
    }
}
