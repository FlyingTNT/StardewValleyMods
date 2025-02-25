using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using Common.Integrations;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace Swim
{
    public class ModEntry : Mod
    {
        public static ModConfig Config { get; private set; }
        public static IMonitor SMonitor { get; private set; }
        public static IModHelper SHelper { get; private set; }

        public static Texture2D OxygenBarTexture => SHelper.GameContent.Load<Texture2D>("FlyingTNT.Swim/OxygenBar");
        public const string scubaMaskID = "Swim_ScubaMask";
        public const string scubaFinsID = "Swim_ScubaFins";
        public const string scubaTankID = "Swim_ScubaTank";
        private static readonly PerScreen<int> oxygen = new PerScreen<int>(() => 0);
        public static readonly PerScreen<bool> willSwim = new PerScreen<bool>(() => false);
        public static readonly PerScreen<bool> isUnderwater = new PerScreen<bool>(() => false);
        public static NPC oldMariner = null;
        public static readonly PerScreen<bool> marinerQuestionsWrongToday = new PerScreen<bool>(() => false);
        public static readonly PerScreen<Random> myRand = new PerScreen<Random>(() => new Random());
        public static readonly PerScreen<bool> locationIsPool = new PerScreen<bool>(() => false);

        public static readonly Dictionary<string, DiveMap> diveMaps = new Dictionary<string, DiveMap>();

        public static readonly PerScreen<List<Vector2>> bubbles = new PerScreen<List<Vector2>>(() => new List<Vector2>());
        public static bool IsGemIslesLoaded = false;

        public static int Oxygen
        {
            get => oxygen.Value;

            set
            {
                oxygen.Value = value;
            }
        }

        public override void Entry(IModHelper helper)
        {           
            Config = Helper.ReadConfig<ModConfig>();

            SMonitor = Monitor;
            SHelper = helper;

            // Without the config only option, the player would not be able to re-enable the mod after they disabled it because we never added the config options.
            helper.Events.GameLoop.GameLaunched += SetupModConfig;

            if (!Config.EnableMod)
                return;

            SwimPatches.Initialize(Monitor, helper);
            SwimDialog.Initialize(Monitor, helper);
            SwimMaps.Initialize(Monitor, helper);
            SwimHelperEvents.Initialize(Monitor, helper);
            SwimUtils.Initialize(Monitor, helper);
            AnimationManager.Initialize(Monitor, helper);

            helper.Events.GameLoop.UpdateTicked += SwimHelperEvents.GameLoop_UpdateTicked;
            helper.Events.GameLoop.OneSecondUpdateTicked += SwimHelperEvents.GameLoop_OneSecondUpdateTicked;
            helper.Events.Input.ButtonsChanged += SwimHelperEvents.Input_ButtonsChanged;
            helper.Events.GameLoop.DayStarted += SwimHelperEvents.GameLoop_DayStarted;
            helper.Events.GameLoop.SaveLoaded += SwimHelperEvents.GameLoop_SaveLoaded;
            helper.Events.GameLoop.Saving += SwimHelperEvents.GameLoop_Saving;
            helper.Events.GameLoop.GameLaunched += SwimHelperEvents.GameLoop_GameLaunched;
            helper.Events.Input.ButtonPressed += SwimHelperEvents.Input_ButtonPressed;
            helper.Events.Display.RenderingHud += SwimHelperEvents.Display_RenderingHud;
            helper.Events.Display.RenderedHud += SwimHelperEvents.Display_RenderedHud;
            helper.Events.Display.RenderedWorld += SwimHelperEvents.Display_RenderedWorld;
            helper.Events.Player.InventoryChanged += SwimHelperEvents.Player_InventoryChanged;
            helper.Events.Player.Warped += SwimHelperEvents.Player_Warped;
            helper.Events.Content.LocaleChanged += SwimHelperEvents.Content_LocaleChanged;
            helper.Events.Content.AssetRequested += Content_AssetRequested;

            var harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.startEvent)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_StartEvent_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(Event), nameof(Event.exitEvent)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Event_exitEvent_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), "updateCommon"),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_updateCommon_Prefix)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_updateCommon_Postfix)),
               transpiler: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_updateCommon_Transpiler))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.setRunning)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_setRunning_Prefix)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_setRunning_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Farmer), nameof(Farmer.changeIntoSwimsuit)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Farmer_changeIntoSwimsuit_Postfix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(Toolbar), nameof(Toolbar.draw), new Type[] { typeof(SpriteBatch) }),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Toolbar_draw_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Wand), nameof(Wand.DoFunction)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Wand_DoFunction_Prefix)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Wand_DoFunction_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.draw)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_draw_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.UpdateWhenCurrentLocation)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_UpdateWhenCurrentLocation_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.resetForPlayerEntry)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_resetForPlayerEntry_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition), new Type[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character) }),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_isCollidingPosition_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.checkAction)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_checkAction_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.isCollidingPosition), new Type[] { typeof(Rectangle), typeof(xTile.Dimensions.Rectangle), typeof(bool), typeof(int), typeof(bool), typeof(Character), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_isCollidingPosition_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.sinkDebris)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.GameLocation_sinkDebris_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(Utility), nameof(Utility.playerCanPlaceItemHere)),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Utility_playerCanPlaceItemHere_Prefix)),
               postfix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.Utility_playerCanPlaceItemHere_Postfix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(IslandSouth), nameof(IslandSouth.performTouchAction), new Type[] { typeof(string[]), typeof(Vector2)}),
               prefix: new HarmonyMethod(typeof(SwimPatches), nameof(SwimPatches.IslandSouth_performTouchAction_Prefix))
            );

            AnimationManager.Patch(harmony);
        }

        private void Content_AssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.StartsWith("aedenthorn.Swim/Fishies"))
            {
                e.LoadFromModFile<Texture2D>($"assets/{e.NameWithoutLocale.ToString().Substring("aedenthorn.Swim/".Length)}.png", AssetLoadPriority.Exclusive);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Portraits\\Mariner"))
            {
                e.LoadFrom(() => { return Game1.content.Load<Texture2D>("Portraits\\Gil"); }, AssetLoadPriority.Low);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Mods/FlyingTNT.Swim/i18n"))
            {
                e.LoadFrom(() => SwimUtils.Geti18nDict(), AssetLoadPriority.Medium);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("FlyingTNT.Swim/OceanForage"))
            {
                // Item id, weight
                e.LoadFrom(() => new List<(string, int)>(new[] {
                    ("(O)152", 25), // Seaweed
                    ("(O)153", 15), // Green Algae
                    ("(O)157", 20), // White Algae
                    ("(O)372", 15), // Clam
                    ("(O)393", 10), // Coral
                    ("(O)397", 9), // Sea Urchin
                    ("(O)394", 3), // Rainbow Shell
                    ("(O)392", 3), // Nautilus Shell
                    ("(O)719", 6), // Mussel
                    ("(O)723", 6), // Oyster
                    ("(O)718", 6) // Cockle
                }), AssetLoadPriority.Medium);
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("FlyingTNT.Swim/Minerals"))
            {
                // (Item id, rock hp or -1 if forage), weight
                e.LoadFrom(() => new List<((string, int), int)>(new[] {
                    (("(O)751", 2), 20), // Copper Stone
                    (("(O)290", 4), 10), // Iron Stone
                    (("(O)764", 8), 5), // Gold Stone
                    (("(O)765", 1), 1), // Iridium Stone
                    (("(O)80", -1), 9), // Quartz
                    (("(O)82", -1), 9), // Fire Quartz
                    (("(O)84", -1), 9), // Frozen Tear
                    (("(O)86", -1), 7), // Earth Crystal
                    (("(O)2", 10), 1), // Diamond Stone
                    (("(O)4", 5), 1), // Ruby Stone
                    (("(O)6", 5), 1), // Jade Stone
                    (("(O)8", 5), 1), // Amethyst Stone
                    (("(O)10", 5), 1), // Topaz Stone
                    (("(O)12", 5), 1), // Emerald Stone
                    (("(O)14", 5), 1), // Aquamarine Stone
                    (("(O)44", 5), 1), // Gem Stone
                }), AssetLoadPriority.Medium);
            }
            else if(e.NameWithoutLocale.IsEquivalentTo("FlyingTNT.Swim/DebuffedMinerals")) // Unused because stone drops don't work outside of the mines
            {
                e.LoadFrom(() => new List<((string, int), int)>(new[] {
                    (("(O)32", 3), 15), // Stone
                    (("(O)34", 3), 15), // Stone
                    (("(O)36", 3), 15), // Stone
                    (("(O)38", 3), 15), // Stone
                    (("(O)40", 3), 15), // Stone
                    (("(O)42", 3), 15), // Stone
                    (("(O)760", 3), 20), // Stone Node
                    (("(O)762", 3), 20), // Stone Node
                    (("(O)2", 10), 1), // Diamond Stone
                    (("(O)4", 5), 2), // Ruby Stone
                    (("(O)6", 5), 2), // Jade Stone
                    (("(O)8", 5), 2), // Amethyst Stone
                    (("(O)10", 5), 2), // Topaz Stone
                    (("(O)12", 5), 2), // Emerald Stone
                    (("(O)14", 5), 2), // Aquamarine Stone
                    (("(O)751", 2), 30), // Copper Stone
                    (("(O)290", 4), 15), // Iron Stone
                    (("(O)764", 8), 5), // Gold Stone
                    (("(O)765", 1), 1), // Iridium Stone
                    (("(O)80", -1), 4), // Quartz
                    (("(O)82", -1), 4), // Fire Quartz
                    (("(O)84", -1), 4), // Frozen Tear
                    (("(O)86", -1), 4), // Earth Crystal
                }), AssetLoadPriority.Medium);
            }
            else if(e.NameWithoutLocale.IsEquivalentTo("Maps/Beach"))
            {
                // Add water propery to tiles behind Willy's house to prevent the player from being able to clip out of bounds with them.
                e.Edit(asset =>
                {
                    var data = asset.AsMap();
                    const int x = 28;
                    const int y = 26;
                    var back = data.Data.RequireLayer("Back");
                    for(int i = 0; i < 8; i++)
                    {
                        for(int j = 0; j < 5; j++)
                        {
                            if(back.GetTileIndexAt(x + i, y + j) == -1)
                            {
                                continue;
                            }
                            if(back.Tiles[x + i, y + j].Properties.ContainsKey("Water"))
                            {
                                continue;
                            }
                            if (back.Tiles[x + i, y + j].TileIndexProperties.ContainsKey("Water"))
                            {
                                continue;
                            }
                            back.Tiles[x + i, y + j].Properties.Add("Water", "I");
                        }
                    }
                    /*
                    data.PatchMap(
                        source: SHelper.ModContent.Load<Map>("assets/BeachPatch.tmx"),
                        sourceArea: null,
                        targetArea: new(28, 26, 8, 5),
                        PatchMapMode.Overlay
                        );*/
                });
            }
            else if(e.NameWithoutLocale.IsEquivalentTo("FlyingTNT.Swim/OxygenBar"))
            {
                e.LoadFromModFile<Texture2D>("assets/O2Bar.png", AssetLoadPriority.Medium);
            }
            else if(e.NameWithoutLocale.IsEquivalentTo("Maps/Mountain"))
            {
                // At the bottom right of the Mountain lake, there is a pointless warp that takes the player out of bounds in the Town. We redirect that warp to take the player to the waterfall, like our warps.
                // We replace it instead of removing it for the off chance that there is actually a reason it exists.
                e.Edit(asset =>
                {
                    var properties = asset.AsMap()?.Data?.Properties;
                    if (properties is null || !properties.TryGetValue("Warp", out string warpProperty))
                    {
                        return;
                    }

                    properties["Warp"] = warpProperty.Replace("85 41 Town 98 0", "85 41 Town 95 5");
                });
            }
            else
            {
                AnimationManager.EditAssets(sender, e);
            }
        }

        public void SetupModConfig(object sender, GameLaunchedEventArgs e)
        {
            const string keybindsPageId = "keybinds";
            const string advancedPageId = "advancedSpawning";

            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = SHelper.ModRegistry.GetApi<IGenericModConfigMenuApi>(IDs.GMCM);
            if(configMenu is null)
            {
                return;
            }

            // Register mod.
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => SHelper.WriteConfig(Config)
            );

            #region Region: Basic Options.
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-EnableMod-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-EnableMod-Description"),
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-ReadyToSwim-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-ReadyToSwim-Description"),
                getValue: () => Config.ReadyToSwim,
                setValue: value => Config.ReadyToSwim = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-ShowOxygenBar-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-ShowOxygenBar-Description"),
                getValue: () => Config.ShowOxygenBar,
                setValue: value => Config.ShowOxygenBar = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimSuitAlways-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimSuitAlways-Description"),
                getValue: () => Config.SwimSuitAlways,
                setValue: value => Config.SwimSuitAlways = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-NoAutoSwimSuit-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-NoAutoSwimSuit-Description"),
                getValue: () => Config.NoAutoSwimSuit,
                setValue: value => Config.NoAutoSwimSuit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-DisplayHatWithSwimsuit-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-DisplayHatWithSwimsuit-Description"),
                getValue: () => Config.DisplayHatWithSwimsuit,
                setValue: value => Config.DisplayHatWithSwimsuit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-AllowActionsWhileInSwimsuit-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-AllowActionsWhileInSwimsuit-Description"),
                getValue: () => Config.AllowActionsWhileInSwimsuit,
                setValue: value => Config.AllowActionsWhileInSwimsuit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-AllowRunningWhileInSwimsuit-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-AllowRunningWhileInSwimsuit-Description"),
                getValue: () => Config.AllowRunningWhileInSwimsuit,
                setValue: value => Config.AllowRunningWhileInSwimsuit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-EnableClickToSwim-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-EnableClickToSwim-Description"),
                getValue: () => Config.EnableClickToSwim,
                setValue: value => Config.EnableClickToSwim = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimRestoresVitals-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimRestoresVitals-Description"),
                getValue: () => Config.SwimRestoresVitals,
                setValue: value => Config.SwimRestoresVitals = value
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: keybindsPageId,
                text: () => SwimUtils.GetTranslation("GMCM-Keybinds-PageName")
            );
            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: advancedPageId,
                text: () => SwimUtils.GetTranslation("GMCM-Advanced-PageName")
            );
            #endregion

            #region Region: Key Binds.

            configMenu.AddPage(
                mod: ModManifest,
                pageId: keybindsPageId,
                pageTitle: () => SwimUtils.GetTranslation("GMCM-Keybinds-PageName")
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimKey-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimKey-Description"),
                getValue: () => Config.SwimKey,
                setValue: value => Config.SwimKey = value
            );
            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimSuitKey-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimSuitKey-Description"),
                getValue: () => Config.SwimSuitKey,
                setValue: value => Config.SwimSuitKey = value
            );
            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-DiveKey-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-DiveKey-Description"),
                getValue: () => Config.DiveKey,
                setValue: value => Config.DiveKey = value
            );
            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-ManualJumpButton-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-ManualJumpButton-Description"),
                getValue: () => Config.ManualJumpButton,
                setValue: value => Config.ManualJumpButton = value
            );
            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-PreventJumpButton-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-PreventJumpButton-Description"),
                getValue: () => Config.PreventJumpButton,
                setValue: value => Config.PreventJumpButton = value
            );
            #endregion

            #region Region: Advanced Tweaks.

            configMenu.AddPage(
                mod: ModManifest,
                pageId: advancedPageId,
                pageTitle: () => SwimUtils.GetTranslation("GMCM-Advanced-PageName")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-StaminaLossPerSecond-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-StaminaLossPerSecond-Description"),
                getValue: () => Config.StaminaLossPerSecond.ToString(),
                setValue: value => Config.StaminaLossPerSecond = float.TryParse(value, out float staminaLossPerSecond) ? staminaLossPerSecond : Config.StaminaLossPerSecond
            );
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-StaminaLossMultiplierWithGear-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-StaminaLossMultiplierWithGear-Description"),
                getValue: () => Config.StaminaLossMultiplierWithGear.ToString(),
                setValue: value => Config.StaminaLossMultiplierWithGear = float.TryParse(value, out float staminaLossPerSecond) ? staminaLossPerSecond : Config.StaminaLossMultiplierWithGear
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimSpeed-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimSpeed-Description"),
                getValue: () => Config.SwimSpeed,
                setValue: value => Config.SwimSpeed = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-SwimRunSpeed-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-SwimRunSpeed-Description"),
                getValue: () => Config.SwimRunSpeed,
                setValue: value => Config.SwimRunSpeed = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-ScubaFinSpeed-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-ScubaFinSpeed-Description"),
                getValue: () => Config.ScubaFinSpeed,
                setValue: value => Config.ScubaFinSpeed = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-OxygenBarXOffset-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-OxygenBarXOffset-Description"),
                getValue: () => Config.OxygenBarXOffset,
                setValue: value => Config.OxygenBarXOffset = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-OxygenBarYOffset-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-OxygenBarYOffset-Description"),
                getValue: () => Config.OxygenBarYOffset,
                setValue: value => Config.OxygenBarYOffset = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-JumpTimeInMilliseconds-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-JumpTimeInMilliseconds-Description"),
                getValue: () => Config.JumpTimeInMilliseconds,
                setValue: value => Config.JumpTimeInMilliseconds = value
            );
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-TriggerDistanceMult-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-TriggerDistanceMult-Description"),
                getValue: () => Config.TriggerDistanceMult.ToString(),
                setValue: delegate (string value) { if (float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var f)) { Config.TriggerDistanceMult = f; } }
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MustClickOnOppositeTerrain-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MustClickOnOppositeTerrain-Description"),
                getValue: () => Config.MustClickOnOppositeTerrain,
                setValue: value => Config.MustClickOnOppositeTerrain = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-OxygenMult-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-OxygenMult-Description"),
                getValue: () => Config.OxygenMult,
                setValue: value => Config.OxygenMult = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-BubbleMult-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-BubbleMult-Description"),
                getValue: () => Config.BubbleMult,
                setValue: value => Config.BubbleMult = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-AddFishies-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-AddFishies-Description"),
                getValue: () => Config.AddFishies,
                setValue: value => Config.AddFishies = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-AddCrabs-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-AddCrabs-Description"),
                getValue: () => Config.AddCrabs,
                setValue: value => Config.AddCrabs = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-BreatheSound-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-BreatheSound-Description"),
                getValue: () => Config.BreatheSound,
                setValue: value => Config.BreatheSound = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MineralPerThousandMin-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MineralPerThousandMin-Description"),
                getValue: () => Config.MineralPerThousandMin,
                setValue: value => Config.MineralPerThousandMin = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MineralPerThousandMax-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MineralPerThousandMax-Description"),
                getValue: () => Config.MineralPerThousandMax,
                setValue: value => Config.MineralPerThousandMax = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-CrabsPerThousandMin-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-CrabsPerThousandMin-Description"),
                getValue: () => Config.CrabsPerThousandMin,
                setValue: value => Config.CrabsPerThousandMin = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-CrabsPerThousandMax-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-CrabsPerThousandMax-Description"),
                getValue: () => Config.CrabsPerThousandMax,
                setValue: value => Config.CrabsPerThousandMax = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-PercentChanceCrabIsMimic-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-PercentChanceCrabIsMimic-Description"),
                getValue: () => Config.PercentChanceCrabIsMimic,
                setValue: value => Config.PercentChanceCrabIsMimic = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MinSmolFishies-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MinSmolFishies-Description"),
                getValue: () => Config.MinSmolFishies,
                setValue: value => Config.MinSmolFishies = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MaxSmolFishies-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MaxSmolFishies-Description"),
                getValue: () => Config.MaxSmolFishies,
                setValue: value => Config.MaxSmolFishies = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-BigFishiesPerThousandMin-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-BigFishiesPerThousandMin-Description"),
                getValue: () => Config.BigFishiesPerThousandMin,
                setValue: value => Config.BigFishiesPerThousandMin = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-BigFishiesPerThousandMax-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-BigFishiesPerThousandMax-Description"),
                getValue: () => Config.BigFishiesPerThousandMax,
                setValue: value => Config.BigFishiesPerThousandMax = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-OceanForagePerThousandMin-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-OceanForagePerThousandMin-Description"),
                getValue: () => Config.OceanForagePerThousandMin,
                setValue: value => Config.OceanForagePerThousandMin = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-OceanForagePerThousandMax-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-OceanForagePerThousandMax-Description"),
                getValue: () => Config.OceanForagePerThousandMax,
                setValue: value => Config.OceanForagePerThousandMax = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MinOceanChests-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MinOceanChests-Description"),
                getValue: () => Config.MinOceanChests,
                setValue: value => Config.MinOceanChests = value
            );
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SwimUtils.GetTranslation("GMCM-MaxOceanChests-Name"),
                tooltip: () => SwimUtils.GetTranslation("GMCM-MaxOceanChests-Description"),
                getValue: () => Config.MaxOceanChests,
                setValue: value => Config.MaxOceanChests = value
            );
            #endregion
        }

        public override object GetApi()
        {
            return new SwimModApi(Monitor, this);
        }
    }
}
