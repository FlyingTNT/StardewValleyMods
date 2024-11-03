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
using Common.Multiplayer;
using StardewValley.GameData.Characters;
using System.Collections.Generic;
using StardewValley.SpecialOrders;

namespace LongerSeasons
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        public static IMonitor SMonitor { get; private set; }
        public static IModHelper SHelper { get; private set; }
        public static ModConfig Config { get; private set; }

        internal static MultiplayerSynced<int> currentSeasonMonth;
        internal static MultiplayerSyncedGroup multiplayerSyncedGroup;
        internal static MultiplayerSynced<int> daysPerMonth;
        internal static MultiplayerSynced<int> monthsPerSpring;
        internal static MultiplayerSynced<int> monthsPerSummer;
        internal static MultiplayerSynced<int> monthsPerFall;
        internal static MultiplayerSynced<int> monthsPerWinter;

        public static int CurrentSeasonMonth
        {
            get => currentSeasonMonth?.IsReady ?? false ? currentSeasonMonth.Value : 1;
            set
            {
                currentSeasonMonth.Value = value;
            }
        }
        public static int DaysPerMonth => daysPerMonth?.IsReady ?? false ? daysPerMonth.Value : Config.DaysPerMonth;
        public static int MonthsPerSpring => monthsPerSpring?.IsReady ?? false ? monthsPerSpring.Value : Config.MonthsPerSpring;
        public static int MonthsPerSummer => monthsPerSummer?.IsReady ?? false ? monthsPerSummer.Value: Config.MonthsPerSummer;
        public static int MonthsPerFall => monthsPerFall?.IsReady ?? false ? monthsPerFall.Value : Config.MonthsPerFall;
        public static int MonthsPerWinter => monthsPerWinter?.IsReady ?? false ? monthsPerWinter.Value : Config.MonthsPerWinter;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            // Needs to happen before the EnableMod check so that the config options still get added to GMCM if the mod is disabled.
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            if (!Config.EnableMod)
                return;

            SMonitor = Monitor;
            SHelper = helper;

            multiplayerSyncedGroup = new(this);
            currentSeasonMonth = multiplayerSyncedGroup.AddSyncedValue("CurrentSeasonMonth", initializer: () => PerSaveConfig.LoadConfigOption<SeasonMonth>(SHelper, "CurrentSeasonMonth", new()).month);
            daysPerMonth = multiplayerSyncedGroup.AddSyncedValue("DaysPerMonth", initializer: () => Config.DaysPerMonth);
            monthsPerSpring = multiplayerSyncedGroup.AddSyncedValue("MonthsPerSpring", initializer: () => Config.MonthsPerSpring);
            monthsPerSummer = multiplayerSyncedGroup.AddSyncedValue("MonthsPerSummer", initializer: () => Config.MonthsPerSummer);
            monthsPerFall = multiplayerSyncedGroup.AddSyncedValue("MonthsPerFall", initializer: () => Config.MonthsPerFall);
            monthsPerWinter = multiplayerSyncedGroup.AddSyncedValue("MonthsPerWinter", initializer: () => Config.MonthsPerWinter);

            Helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            Helper.Events.GameLoop.Saving += GameLoop_Saving;
            Helper.Events.Content.AssetRequested += Content_AssetRequested;
            Helper.Events.Content.AssetsInvalidated += Content_AssetsInvalidated;
            Helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;

            SetupConsoleCommands();

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
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Billboard_Constructor_Transpiler)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Billboard_Constructor_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Billboard), nameof(Billboard.draw), new Type[] { typeof(SpriteBatch) }),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Billboard_draw_Transpiler))
            );

            // SpecialOrder Patches
            harmony.Patch(
               original: AccessTools.Method(typeof(SpecialOrder), nameof(SpecialOrder.GetDaysLeft)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.SpecialOrder_GetDaysLeft_PostFix))
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
            SetupConfig();

            if(!Config.EnableMod)
            {
                return;
            }

            SetupCPToken();
        }

        private void SetupConfig()
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(IDs.GMCM);
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () =>
                {
                    Config = new ModConfig();
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_EnableMod_Name"),
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_ExtendBerry_Name"),
                getValue: () => Config.ExtendBerry,
                setValue: value => Config.ExtendBerry = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_DistributeBirthdays_Name"),
                getValue: () => Config.ExtendBirthdays,
                setValue: value => {
                    if (Config.ExtendBirthdays != value)
                    {
                        Config.ExtendBirthdays = value;
                        Helper.GameContent.InvalidateCache("Data/Characters");
                    }
                }
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_AvoidBirthdayOverlap_Name"),
                getValue: () => Config.AvoidBirthdayOverlaps,
                setValue: value => {
                    if (value != Config.AvoidBirthdayOverlaps)
                    {
                        Config.AvoidBirthdayOverlaps = value;
                        Helper.GameContent.InvalidateCache("Data/Characters");
                    }
                }
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_DaysPerMonth_Name"),
                getValue: () => Config.DaysPerMonth,
                setValue: value => {
                    if (value != Config.DaysPerMonth)
                    {
                        Config.DaysPerMonth = value;
                        if (Context.IsMainPlayer)
                        {
                            daysPerMonth.Value = value;
                        }
                        Helper.GameContent.InvalidateCache("LooseSprites/Billboard");
                        Helper.GameContent.InvalidateCache("Data/Characters");
                    }
                }
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_MonthsPerSpring_Name"),
                getValue: () => Config.MonthsPerSpring,
                setValue: value => {
                    if (value != Config.MonthsPerSpring)
                    {
                        Config.MonthsPerSpring = value;
                        monthsPerSpring.Value = value;
                    } 
                }
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_MonthsPerSummer_Name"),
                getValue: () => Config.MonthsPerSummer,
                setValue: value => {
                    if (value != Config.MonthsPerSummer)
                    {
                        Config.MonthsPerSummer = value;
                        monthsPerSummer.Value = value;
                    }
                }
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_MonthsPerFall_Name"),
                getValue: () => Config.MonthsPerFall,
                setValue: value => { 
                    if (value != Config.MonthsPerFall) 
                    { 
                        Config.MonthsPerFall = value; 
                        monthsPerFall.Value = value; 
                    } 
                }
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_MonthsPerWinter_Name"),
                getValue: () => Config.MonthsPerWinter,
                setValue: value => { 
                    if (value != Config.MonthsPerWinter) 
                    { 
                        Config.MonthsPerWinter = value; 
                        monthsPerWinter.Value = value; 
                    } 
                }
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "AdvancedBillboardOptions",
                text: () => SHelper.Translation.Get("GMCM_BillboardOptions_Title")
            );

            // ADVANCED BILLBOARD OPTIONS PAGE
            configMenu.AddPage(
                mod: ModManifest,
                pageId: "AdvancedBillboardOptions",
                pageTitle: () => SHelper.Translation.Get("GMCM_BillboardOptions_Title"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_GetNumbersFromBillboard_Name"),
                getValue: () => Config.GetNumbersFromBillboard,
                setValue: value => {
                    if (value != Config.GetNumbersFromBillboard)
                    {
                        Config.GetNumbersFromBillboard = value;
                        SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    }
                }
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_BillboardNumberWidth_Name"),
                getValue: () => Config.BillboardNumberWidth,
                setValue: value => {
                    if (value != Config.BillboardNumberWidth)
                    {
                        Config.BillboardNumberWidth = value;
                        SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    }
                }
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_BillboardNumberHeight_Name"),
                getValue: () => Config.BillboardNumberHeight,
                setValue: value => {
                    if (value != Config.BillboardNumberHeight)
                    {
                        Config.BillboardNumberHeight = value;
                        SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    }
                }
            );

            for(int i = 0; i < 10; i++)
            {
                int j = i; // Necessary so the scope is correct
                configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_NumberOffsetX_Name", new{number = j.ToString()}),
                tooltip: () => SHelper.Translation.Get("GMCM_NumberOffsetX_Description", new { number = j.ToString() }),
                getValue: () => { SMonitor.Log($"{j}"); return Config.BillboardNumberOffestsX[j]; },
                setValue: value => {
                    if (value != Config.BillboardNumberOffestsX[j])
                    {
                        Config.BillboardNumberOffestsX[j] = value;
                        SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    }
                });

                configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_NumberOffsetY_Name", new { number = j.ToString() }),
                tooltip: () => SHelper.Translation.Get("GMCM_NumberOffsetY_Description", new { number = j.ToString() }),
                getValue: () => Config.BillboardNumberOffestsY[j],
                setValue: value => {
                    if (value != Config.BillboardNumberOffestsY[j])
                    {
                        Config.BillboardNumberOffestsY[j] = value;
                        SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    }
                });
            }
        }

        private void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            Helper.GameContent.InvalidateCache("LooseSprites/Billboard");
        }

        private void Content_AssetRequested(object sender, AssetRequestedEventArgs args)
        {
            if(args.NameWithoutLocale.IsEquivalentTo("Data/Characters") && Config.ExtendBirthdays)
            {
                args.Edit((asset) =>
                {
                    var characters = asset.AsDictionary<string, CharacterData>();

                    Dictionary<string, string> birthdays = new();

                    foreach(var character in characters.Data)
                    {
                        int proposedBirthday = (int)Math.Round((character.Value.BirthDay / 28f) * DaysPerMonth);

                        // If we don't care about overlaps, take the proposal unless it is a festival day
                        if(!Config.AvoidBirthdayOverlaps && !Utility.isFestivalDay(proposedBirthday, character.Value.BirthSeason ?? Season.Spring, null))
                        {
                            character.Value.BirthDay = proposedBirthday;
                            continue;
                        }

                        // Try the days surrounding the proposal until we find one without a birthday / festival
                        int i = 0;
                        int newProposal = proposedBirthday;

                        while (Utility.isFestivalDay(newProposal, character.Value.BirthSeason ?? Season.Spring, null) || birthdays.ContainsKey($"{character.Value.BirthSeason}{newProposal}"))
                        {
                            // will add +1, -1, +2, -2, +3, -3,.... until it finds a hit
                            newProposal = Utility.Clamp(proposedBirthday + ((i+2) / 2) * (int)Math.Pow(-1, i), 1, DaysPerMonth);
                            i++;

                            // If we somehow didn't find a valid spot, just use the OG proposal (2 * days/month is the absolute max number of days we need to check)
                            if(i >= 2 * DaysPerMonth)
                            {
                                newProposal = proposedBirthday;
                                break;
                            }
                        }

                        birthdays[$"{character.Value.BirthSeason}{newProposal}"] = character.Key;
                        character.Value.BirthDay = newProposal;
                    }

                }, AssetEditPriority.Late);
            }
            else if(args.NameWithoutLocale.IsEquivalentTo("FlyingTNT.LongerSeasons/Billboard"))
            {
                args.LoadFrom(() =>
                {
                    // Just copy the billboard texture
                    Texture2D billboard = Game1.content.Load<Texture2D>("LooseSprites\\Billboard");
                    Texture2D output = new(Game1.graphics.GraphicsDevice, billboard.Width, billboard.Height);
                    output.CopyFromTexture(billboard);
                    return output;
                }, AssetLoadPriority.Medium);

                args.Edit(asset =>
                {
                    int startDay = 28 * (Game1.dayOfMonth / 28) + 1;
                    int daysCount = Utility.Clamp(DaysPerMonth - startDay + 1, 0, 28);

                    if(startDay <= 28)
                    {
                        return;
                    }

                    const int numberXOffset = 1; // How many pixels the numbers are from the left side of the date boxes
                    const int numberYOffset = 0;
                    Point numberSize = new(Config.BillboardNumberWidth, Config.BillboardNumberHeight);

                    IAssetDataForImage image = asset.AsImage();
                    Texture2D numbersSource = Config.GetNumbersFromBillboard ? Game1.temporaryContent.Load<Texture2D>("LooseSprites\\Billboard") : SHelper.ModContent.Load<Texture2D>("assets/numbers.png");

                    Rectangle[] numberSourcePositions = new Rectangle[10];
                    
                    // Remove the old numbers by covering them up by duplicating the bottom half of the calendar slot
                    for(int i = 1; i <= 28; i++)
                    {
                        Point topLeftCorner = GetPositionInBillboardTexture(i);
                        Point halfwayDown = new Point(topLeftCorner.X, topLeftCorner.Y + 15);
                        Point halfOfTheSlot = new(31, 16);

                        image.PatchImage(image.Data, new Rectangle(halfwayDown, halfOfTheSlot), new(topLeftCorner, halfOfTheSlot));
                    }

                    // Generate source rectangles for digits
                    if(Config.GetNumbersFromBillboard)
                    {
                        // Get 1-9 from their respective dates
                        for (int i = 1; i < 10; i++)
                        {
                            numberSourcePositions[i] = new Rectangle(GetPositionInBillboardTexture(i), numberSize);
                            numberSourcePositions[i].X += numberXOffset + Config.BillboardNumberOffestsX[i];
                            numberSourcePositions[i].Y += numberYOffset + Config.BillboardNumberOffestsY[i];
                        }

                        // Get 0 from 10
                        numberSourcePositions[0] = new Rectangle(GetPositionInBillboardTexture(10) + new Point(numberXOffset + numberSize.X + Config.BillboardNumberOffestsX[0], numberYOffset + Config.BillboardNumberOffestsY[0]), numberSize);
                    }
                    else
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            numberSourcePositions[i] = new Rectangle(i*6, 0, numberSize.X, numberSize.Y);
                        }
                    }

                    // Add the correct numbers to the billboard
                    for (int i = startDay; i < startDay + daysCount; i++)
                    {
                        int cents = i / 100;
                        int tens = (i - cents * 100) / 10;
                        int ones = i - cents * 100 - tens * 10;
                        Point offset = new(numberXOffset, numberYOffset);
                        if (cents > 0)
                        {
                            image.PatchImage(numbersSource, numberSourcePositions[cents], new Rectangle(GetPositionInBillboardTexture(i) + offset, numberSize), PatchMode.Overlay);
                            offset.X += numberSize.X;
                        }
                        image.PatchImage(numbersSource, numberSourcePositions[tens], new Rectangle(GetPositionInBillboardTexture(i) + offset, numberSize), PatchMode.Overlay);
                        offset.X += numberSize.X;
                        image.PatchImage(numbersSource, numberSourcePositions[ones], new Rectangle(GetPositionInBillboardTexture(i) + offset, numberSize), PatchMode.Overlay);
                    }
                });
            }
        }

        private static Point GetPositionInBillboardTexture(int day)
        {
            // If the day is over 28, gets the number that is at the same position as it in the base calendar.
            day = ((day - 1) % 28) + 1;
            return new(6+32 + ((day - 1) % 7) * 32, 248 + ((day - 1) / 7) * 32);
        }

        private static void Content_AssetsInvalidated(object sender, AssetsInvalidatedEventArgs args)
        {
            foreach(var name in args.NamesWithoutLocale)
            {
                if(name.IsEquivalentTo("LooseSprites/Billboard"))
                {
                    SHelper.GameContent.InvalidateCache("FlyingTNT.LongerSeasons/Billboard");
                    return;
                }
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

        private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs args)
        {
            if(!Context.IsMainPlayer)
            {
                return;
            }

            if(!args.Peer.HasSmapi || args.Peer.GetMod(IDs.LongerSeasons) is not IMultiplayerPeerMod otherLongerSeasons)
            {
                SMonitor.Log(SHelper.Translation.Get("MultiplayerWarningNotInstalled", new { playerName = Game1.otherFarmers.TryGetValue(args.Peer.PlayerID, out Farmer other) ? other.displayName : args.Peer.PlayerID.ToString() }), LogLevel.Warn);
                return;
            }

            // Data sync method changed from syncing whole config to only syncing certain values in 1.2.0
            if(otherLongerSeasons.Version.MajorVersion <= 1 && otherLongerSeasons.Version.MinorVersion < 2)
            {
                SMonitor.Log(SHelper.Translation.Get("MultiplayerWarningOldVersion", new { playerName = Game1.otherFarmers.TryGetValue(args.Peer.PlayerID, out Farmer other) ? other.displayName : args.Peer.PlayerID.ToString() }), LogLevel.Warn);
            }
        }

        public static int GetMonthsInSeason(Season season)
        {
            return season switch
            {
                Season.Spring => MonthsPerSpring,
                Season.Summer => MonthsPerSummer,
                Season.Fall => MonthsPerFall,
                Season.Winter => MonthsPerWinter,
                _ => MonthsPerSpring
            };
        }

        public static int GetMonthsInCurrentSeason()
        {
            return GetMonthsInSeason(Game1.season);
        }

        public override object GetApi()
        {
            return new LongerSeasonsAPI();
        }
    }

    public class SeasonMonth
    {
        public int month = 1;
    }
}