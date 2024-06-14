using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using System;
using Common.Integrations;
using Common.Utilities;

namespace LongerSeasons
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {

        public static IMonitor SMonitor;
        public static IModHelper SHelper;

        private static ModConfig LocalConfig;
        private static MultiplayerSynced<ModConfig> CommonConfig;
        public static ModConfig Config => Context.IsWorldReady ? CommonConfig.Value : LocalConfig;

        private static MultiplayerSynced<int> currentSeasonMonth;
        public static int CurrentSeasonMonth
        {
            get
            {
                return Context.IsWorldReady ? currentSeasonMonth.Value : 1;
            }

            set
            {
                currentSeasonMonth.Value = value;
            }
        }

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            LocalConfig = Helper.ReadConfig<ModConfig>();

            if (!LocalConfig.EnableMod)
                return;

            SMonitor = Monitor;
            SHelper = helper;

            CommonConfig = new MultiplayerSynced<ModConfig>(helper, "Config", initializer: () => LocalConfig);
            currentSeasonMonth = new MultiplayerSynced<int>(helper, "CurrentSeasonMonth", initializer: () => PerSaveConfig.LoadConfigOption<SeasonMonth>(SHelper, "CurrentSeasonMonth", new()).month);

            Helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.Saving += GameLoop_Saving;
            Helper.Events.Content.AssetRequested += Content_AssetRequested;

            var harmony = new Harmony(ModManifest.UniqueID);

            // Game1 Patches

            harmony.Patch(
               original: AccessTools.Method(typeof(Game1), "_newDayAfterFade"),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Game1__newDayAfterFade_Prefix))
            );

            foreach(var type in typeof(Game1).Assembly.GetTypes())
            {
                if (type.FullName.StartsWith("StardewValley.Game1+<_newDayAfterFade>"))
                {
                    Monitor.Log($"Found {type}");
                    harmony.Patch(
                       original: AccessTools.Method(type, "MoveNext"),
                       transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Game1__newDayAfterFade_Transpiler))
                    );
                    break;
                }
            }
            

            // SDate Patches

            harmony.Patch(
               original: AccessTools.Constructor(typeof(SDate), new Type[] { typeof(int), typeof(Season), typeof(int), typeof(bool) }),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.SDate_Transpiler)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.SDate_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Constructor(typeof(SDate), new Type[] { typeof(int), typeof(Season) }),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.SDate_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Constructor(typeof(SDate), new Type[] { typeof(int), typeof(Season), typeof(int)}),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.SDate_Postfix))
            );

            // Utility Patches


            harmony.Patch(
               original: AccessTools.Method(typeof(Utility), nameof(Utility.getDateStringFor)),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Utility_getDateStringFor_Transpiler))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Utility), nameof(Utility.getDaysOfBooksellerThisSeason)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Utility_getDaysOfBooksellerThisSeason_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Utility), nameof(Utility.getSeasonNameFromNumber)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Utility_getSeasonNameFromNumber_Postfix))
            );


            // Billboard Patches

            harmony.Patch(
               original: AccessTools.Constructor(typeof(Billboard), new Type[]{ typeof(bool) }),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Billboard_Constructor_Transpiler))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Billboard), nameof(Billboard.draw), new Type[] { typeof(SpriteBatch) }),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Billboard_draw_Transpiler))
            );

            // WorldDate Patches
            harmony.Patch(
               original: AccessTools.PropertyGetter(typeof(WorldDate), nameof(WorldDate.TotalDays)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.WorldDate_TotalDays_Getter_Prefix))
            );
            harmony.Patch(
               original: AccessTools.PropertySetter(typeof(WorldDate), nameof(WorldDate.TotalDays)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.WorldDate_TotalDays_Setter_Prefix))
            );

            // Bush Patches
            harmony.Patch(
               original: AccessTools.Method(typeof(Bush), nameof(Bush.inBloom)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Bush_inBloom_Prefix))
            );
        }

        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => 
                { 
                    LocalConfig = new ModConfig();
                    if (Context.IsMainPlayer)
                        CommonConfig.Value = LocalConfig;
                },
                save: () => 
                {
                    Helper.WriteConfig(LocalConfig);
                    if (Context.IsMainPlayer)
                        CommonConfig.Value = LocalConfig;
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Enable Mod",
                getValue: () => Config.EnableMod,
                setValue: value => (Context.IsMainPlayer ? Config : LocalConfig).EnableMod = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Extend Berry Seasons",
                getValue: () => Config.ExtendBerry,
                setValue: value => (Context.IsMainPlayer ? Config : LocalConfig).ExtendBerry = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Days per Month",
                getValue: () => Config.DaysPerMonth,
                setValue: value => {
                    (Context.IsMainPlayer ? Config : LocalConfig).DaysPerMonth = value;
                    Helper.GameContent.InvalidateCache("LooseSprites/Billboard");
                }
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Months per Season",
                getValue: () => Config.MonthsPerSeason,
                setValue: value => (Context.IsMainPlayer ? Config : LocalConfig).MonthsPerSeason = value
            );
        }

        private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            Helper.GameContent.InvalidateCache("LooseSprites/Billboard");
        }

        private void Content_AssetRequested(object sender, AssetRequestedEventArgs args)
        {
            if(args.NameWithoutLocale.IsEquivalentTo("LooseSprites/Billboard") && Game1.dayOfMonth > 28)
            {
                args.Edit((asset) =>
                {
                    var editor = asset.AsImage();

                    IRawTextureData sourceImage = Helper.ModContent.Load<IRawTextureData>("assets/numbers.png");
                    IRawTextureData blankImage = Helper.ModContent.Load<IRawTextureData>("assets/BlankBillboard.png");

                    int startDay = 28 * (Game1.dayOfMonth / 28) + 1;
                    int daysCount = Config.DaysPerMonth - startDay + 1;

                    // Add the correct numbers to the billboard
                    for (int i = startDay; i < startDay + daysCount; i++)
                    {
                        int cents = i / 100;
                        int tens = (i - cents * 100) / 10;
                        int ones = i - cents * 100 - tens * 10;
                        int xOff = 7;
                        if (cents > 0)
                        {
                            xOff = 14;
                            editor.PatchImage(sourceImage, new Rectangle(6 * cents, 0, 7, 11), new Rectangle(39 + (i - 1) % 7 * 32, 248 + (i - startDay) / 7 * 32, 7, 11), PatchMode.Overlay);
                        }
                        editor.PatchImage(sourceImage, new Rectangle(6 * tens, 0, 7, 11), new Rectangle(32 + xOff + (i - 1) % 7 * 32, 248 + (i - startDay) / 7 * 32, 7, 11), PatchMode.Overlay);
                        editor.PatchImage(sourceImage, new Rectangle(6 * ones, 0, 7, 11), new Rectangle(39 + xOff + (i - 1) % 7 * 32, 248 + (i - startDay) / 7 * 32, 7, 11), PatchMode.Overlay);
                    }

                    // Remove any old numbers if the month does not fill up the entire billboard
                    for(int i = startDay + daysCount; i < startDay + 28; i++)
                    {
                        editor.PatchImage(blankImage, new Rectangle(0, 0, 31, 31), new Rectangle(38 + (i - 1) % 7 * 32, 248 + (i - startDay) / 7 * 32, 31, 31), PatchMode.Overlay);
                    }
                });
            }
        }

        private void GameLoop_Saving(object sender, SavingEventArgs args)
        {
            if(!Context.IsMainPlayer)
            {
                return;
            }

            PerSaveConfig.SaveConfigOption(SHelper, "CurrentSeasonMonth", new SeasonMonth(){ month = CurrentSeasonMonth });
        }
    }

    public class SeasonMonth
    {
        public int month = 1;
    }
}