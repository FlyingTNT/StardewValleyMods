using HarmonyLib;
using StardewModdingAPI;
using Common.Integrations;
using System.Collections.Generic;
using StardewModdingAPI.Events;
using StardewValley;
using Newtonsoft.Json;
using StardewValley.Menus;

namespace CustomGiftLimits
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        private const string GiftsGivenDataKey = "FlyingTNT.CustomGiftLimits.GiftsGiven";

        public static IMonitor SMonitor { get; private set; }
        public static IModHelper SHelper { get; private set; }
        public static ModConfig Config { get; private set; }
        public static Dictionary<string, GiftRecord> GiftsGiven { get; private set; } = new();

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            if (!Config.ModEnabled)
                return;

            SMonitor = Monitor;
            SHelper = helper;

            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.GameLoop.Saving += GameLoop_Saving;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;

            var harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.updateFriendshipGifts)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_updateFriendshipGifts_Postfix))
                );

            harmony.Patch(
                original: AccessTools.Method(typeof(NPC), nameof(NPC.tryToReceiveActiveObject)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(NPC_tryToReceiveActiveObject_Prefix)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(NPC_tryToReceiveActiveObject_Postfix)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(NPC_tryToReceiveActiveObject_Transpiler))
                );

            harmony.Patch(
                original: AccessTools.Method(typeof(SocialPage), nameof(SocialPage.drawNPCSlot)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_drawNPCSlot_Transpiler))
                );
        }


        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(IDs.GMCM);
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => { Helper.WriteConfig(Config); 
                              ReloadGiftsGiven(); }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                getValue: () => Config.ModEnabled,
                setValue: value => Config.ModEnabled = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_OrdinaryGiftsPerDay_Name"),
                getValue: () => Config.OrdinaryGiftsPerDay,
                setValue: value => Config.OrdinaryGiftsPerDay = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_OrdinaryGiftsPerWeek_Name"),
                getValue: () => Config.OrdinaryGiftsPerWeek,
                setValue: value => Config.OrdinaryGiftsPerWeek = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_FriendGiftsPerDay_Name"),
                getValue: () => Config.FriendGiftsPerDay,
                setValue: value => Config.FriendGiftsPerDay = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_FriendGiftsPerWeek_Name"),
                getValue: () => Config.FriendGiftsPerWeek,
                setValue: value => Config.FriendGiftsPerWeek = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_DatingGiftsPerDay_Name"),
                getValue: () => Config.DatingGiftsPerDay,
                setValue: value => Config.DatingGiftsPerDay = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_DatingGiftsPerWeek_Name"),
                getValue: () => Config.DatingGiftsPerWeek,
                setValue: value => Config.DatingGiftsPerWeek = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_SpouseGiftsPerDay_Name"),
                getValue: () => Config.SpouseGiftsPerDay,
                setValue: value => Config.SpouseGiftsPerDay = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_SpouseGiftsPerWeek_Name"),
                getValue: () => Config.SpouseGiftsPerWeek,
                setValue: value => Config.SpouseGiftsPerWeek = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_MaxedHeartsGiftsPerDay_Name"),
                getValue: () => Config.MaxedHeartsGiftsPerDay,
                setValue: value => Config.MaxedHeartsGiftsPerDay = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_MaxedHeartsGiftsPerWeek_Name"),
                getValue: () => Config.MaxedHeartsGiftsPerWeek,
                setValue: value => Config.MaxedHeartsGiftsPerWeek = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => ModEntry.SHelper.Translation.Get("GMCM_Option_CompatibilityMode_Name"),
                tooltip: () => ModEntry.SHelper.Translation.Get("GMCM_Option_CompatibilityMode_Description"),
                getValue: () => Config.CompatibilityMode,
                setValue: value => Config.CompatibilityMode = value
            );
        }

        private static void GameLoop_Saving(object sender, SavingEventArgs args)
        {
            Game1.player.modData[GiftsGivenDataKey] = JsonConvert.SerializeObject(GiftsGiven);
        }

        private static void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs args)
        {
            if(!Game1.player.modData.TryGetValue(GiftsGivenDataKey, out string data))
            {
                GiftsGiven = new();
                return;
            }

            GiftsGiven = JsonConvert.DeserializeObject<Dictionary<string, GiftRecord>>(data);
            ReloadGiftsGiven();
        }

        public override ICustomGiftLimitsAPI GetApi()
        {
            return new CustomGiftLimitsAPI();
        }

        public struct GiftRecord
        {
            public int GiftsToday = 0;
            public int GiftsThisWeek = 0;

            public GiftRecord(int today, int thisWeek)
            {
                GiftsToday = today;
                GiftsThisWeek = thisWeek;
            }
        }
    }
}