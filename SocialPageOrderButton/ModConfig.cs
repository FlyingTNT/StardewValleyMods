using StardewModdingAPI;

namespace SocialPageOrderButton
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public SButton prevButton { get; set; } = SButton.Up;
        public SButton nextButton { get; set; } = SButton.Down;
        public int ButtonOffsetX { get; set; } = 0;
        public int ButtonOffsetY { get; set; } = 0;
        public bool UseFilter { get; set; } = false;
        public int FilterOffsetX { get; set; } = 0;
        public int FilterOffsetY { get; set; } = 0;
    }
}
