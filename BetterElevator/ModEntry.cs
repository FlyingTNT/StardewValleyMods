using HarmonyLib;
using StardewModdingAPI;
using Common.Integrations;
using StardewValley;
using System;
using xTile.Dimensions;
using StardewValley.Locations;

namespace BetterElevator
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

        public static ModEntry context;

        public static string coopName;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            if (!Config.ModEnabled)
                return;

            context = this;

            SMonitor = Monitor;
            SHelper = helper;

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performAction), new Type[] { typeof(string[]), typeof(Farmer), typeof(Location) }),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(GameLocation_performAction_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(MineShaft), nameof(MineShaft.checkAction)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(MineShaft_checkAction_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(MineShaft), nameof(MineShaft.shouldCreateLadderOnThisLevel)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(MineShaft_shouldCreateLadderOnThisLevel_Postfix))
            );
        }
        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(IDs.GMCM);
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                getValue: () => Config.ModEnabled,
                setValue: value => Config.ModEnabled = value
            );
            
            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_ModKey_Name"),
                getValue: () => Config.ModKey,
                setValue: value => Config.ModKey = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_Unrestricted_Name"),
                tooltip: () => ModEntry.SHelper.Translation.Get("GMCM_Option_Unrestricted_Tooltip"),
                getValue: () => Config.Unrestricted,
                setValue: value => Config.Unrestricted = value
            );
        }
    }
}