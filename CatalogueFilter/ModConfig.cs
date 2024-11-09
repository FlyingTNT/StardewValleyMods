using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System.Collections.Generic;

namespace CatalogueFilter
{
    public class ModConfig
    {
        public bool ModEnabled { get; set; } = true;
        public bool ShowLabel { get; set; } = true;
        public Color LabelColor { get; set; } = Color.White; // Color for the text that says "Filter" in the shop menu
        public bool AutoSelectFilter { get; set; } = false;
        public int FilterOffsetX { get; set; } = 0;
        public int FilterOffsetY { get; set; } = 0;
        public KeybindList SelectFilterKey { get; set; } = KeybindList.Parse("Tab");
    }
}
