using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley.GameData.Shops;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using Common.Integrations;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;

namespace CatalogueFilter
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

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
            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;

            var harmony = new Harmony(ModManifest.UniqueID);

            // The postfix only uses the ShopMenu instance as a parameter so it is safe to iterate regardless of the types
            foreach(ConstructorInfo constructor in AccessTools.GetDeclaredConstructors(typeof(ShopMenu)))
            {
                harmony.Patch(
                    original: constructor,
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_Constructor_Postfix)));
            }

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.applyTab)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_applyTab_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.updatePosition)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_updatePosition_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.drawCurrency)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_drawCurrency_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveLeftClick)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_receiveLeftClick_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.receiveKeyPress)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_receiveKeyPress_Prefix)));

            harmony.Patch(
                original: AccessTools.Method(typeof(ShopMenu), nameof(ShopMenu.performHoverAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ShopMenu_performHoverAction_Postfix)));
        }


        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {

            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
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
                name: () => SHelper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                getValue: () => Config.ModEnabled,
                setValue: value => Config.ModEnabled = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_AutoSelectFilter_Name"),
                getValue: () => Config.AutoSelectFilter,
                setValue: value => Config.AutoSelectFilter = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_FilterSelectKey_Name"),
                getValue: () => Config.SelectFilterKey,
                setValue: value => Config.SelectFilterKey = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_FilterOffsetX_Name"),
                getValue: () => Config.FilterOffsetX,
                setValue: value => Config.FilterOffsetX = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_FilterOffsetY_Name"),
                getValue: () => Config.FilterOffsetY,
                setValue: value => Config.FilterOffsetY = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_ShowLabel_Name"),
                getValue: () => Config.ShowLabel,
                setValue: value => Config.ShowLabel = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_TextColorR_Name"),
                getValue: () => Config.LabelColor.R,
                setValue: value => { if (0 <= value && value <= 255) Config.LabelColor = new((byte)value, Config.LabelColor.G, Config.LabelColor.B, Config.LabelColor.A);}
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_TextColorG_Name"),
                getValue: () => Config.LabelColor.G,
                setValue: value => { if (0 <= value && value <= 255) Config.LabelColor = new(Config.LabelColor.R, (byte)value, Config.LabelColor.B, Config.LabelColor.A); }
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM_Option_TextColorB_Name"),
                getValue: () => Config.LabelColor.G,
                setValue: value => { if (0 <= value && value <= 255) Config.LabelColor = new(Config.LabelColor.R, Config.LabelColor.G, (byte)value, Config.LabelColor.A); }
            );
        }

        private static void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs args)
        {
            if(Config.ModEnabled && Game1.activeClickableMenu is ShopMenu)
            {
                if(Config.SelectFilterKey.JustPressed())
                {
                    FilterField.Selected = !FilterField.Selected;
                }
            }
        }
    }
}