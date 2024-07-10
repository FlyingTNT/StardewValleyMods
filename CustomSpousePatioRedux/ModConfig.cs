using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace CustomSpousePatioRedux
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public int MaxSpousesPerPage { get; set; } = 6;
        public KeybindList PatioWizardKey { get; set; } = KeybindList.Parse("F8");
    }
}
