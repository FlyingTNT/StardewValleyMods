using Common.Utilities;
using Common.Integrations;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static StardewValley.Menus.SocialPage;
using SocialPageOrderRedux.UI;

namespace SocialPageOrderRedux
{
    public class ModEntry : Mod
    {
        public static ModConfig Config;
        public static IMonitor SMonitor;
        public static IModHelper SHelper;

        /// <summary> Location of the organize button within LooseSprites/Cursors </summary>
        public static readonly Rectangle buttonTextureSource = new Rectangle(162, 440, 16, 16);

        /// <summary> The X offset of the sort button from the left of the game menu. </summary>
        private const int buttonXOffset = -16;

        /// <summary> The X offset of the hide talked item from the left of the game menu. </summary>
        private const int talkedXOffset = buttonXOffset - 20;

        /// <summary> The Y offset of the hide talked item from the left of the game menu. </summary>
        private const int talkedYOffset = 80;

        /// <summary> The Y offset ot the dropdown from the bottom of the game menu. </summary>
        private const int dropdownYOffset = -28;

        /// <summary> The unique id of the sort button Clickable Component. </summary>
        private const int buttonId = 231445356;

        /// <summary> The dropdown object. </summary>
        public static readonly PerScreen<MyOptionsDropDown> dropDown = new();

        /// <summary> The button object. </summary>
        public static readonly PerScreen<ClickableTextureComponent> button = new();

        /// <summary> The string that was in the filter field the last time it was checked. </summary>
        private static readonly PerScreen<string> lastFilterString = new PerScreen<string>(() => "");

        /// <summary> The filter field (search bar) object. </summary>
        private static readonly PerScreen<TextBox> filterField = new();

        private static SocialPageFilter talkedFilter;
        private static SocialPageFilter giftedFilter;
        private static SocialPageFilter maxFriendFilter;
        private static SocialPageFilter unmetFilter;
        internal static readonly List<SocialPageFilter> filters = new();

        /// <summary> All of the entries in the social page, before we removed any when searching. Used to restore the page when we clear the search bar. </summary>
        private static readonly PerScreen<List<SocialEntry>> allEntries = new PerScreen<List<SocialEntry>>(() => new List<SocialEntry>());

        /// <summary> Whether the mod was enabled when the game was loaded. If it wasn't, we don't do anything because the patches weren't applied. </summary>
        private static bool WasModEnabled = false;

        /// <summary> Whether the GameMenu.receiveLeftClick method is currently being called. Used in <see cref="IClickableMenu_readyToClose_Postfix(IClickableMenu, ref bool)"/>. </summary>
        private static readonly PerScreen<bool> isInGameMenuLeftClick = new(() => false);

        private static IBetterGameMenuApi BetterGameMenuAPI;

        /// <summary>
        /// The sort curently selected by Game1.player. It is stored in their mod data so that it is preserved between sessions, and for splitscreen support (it used to be stored in the config file,
        /// but this would link the two screens' sorts together).
        /// </summary>
        public static int CurrentSort
        {
            get
            {
                return PerPlayerConfig.LoadConfigOption(Game1.player, "FlyingTNT.SocialPageOrderRedux.CurrentSort", defaultValue: 0);
            }
            set
            {
                PerPlayerConfig.SaveConfigOption(Game1.player, "FlyingTNT.SocialPageOrderRedux.CurrentSort", value);
            }
        }

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            WasModEnabled = Config.EnableMod;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            if (!Config.EnableMod)
                return;
            SMonitor = Monitor;
            SHelper = Helper;

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.MenuChanged += Display_MenuChanged;
            helper.Events.Content.LocaleChanged += Content_LocaleChanged;
            helper.Events.Content.AssetRequested += Content_AssetRequested;

            var harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(AccessTools.Constructor(typeof(SocialPage), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_Constructor_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.receiveKeyPress)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_recieveKeyPress_Prefix))
            );
            
            harmony.Patch(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.receiveKeyPress)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(GameMenu_recieveKeyPress_Prefix))
            );

            harmony.Patch(AccessTools.Method(typeof(SocialPage), nameof(SocialPage.receiveLeftClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_receiveLeftClick_Prefix))
            );

            harmony.Patch(AccessTools.Method(typeof(SocialPage), nameof(SocialPage.releaseLeftClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_releaseLeftClick_Prefix))
            );

            harmony.Patch(AccessTools.Method(typeof(SocialPage), nameof(SocialPage.performHoverAction)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_performHoverAction_Postfix))
            );
            
            harmony.Patch(AccessTools.Method(typeof(SocialPage), nameof(SocialPage.FindSocialCharacters)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_FindSocialCharacters_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(SocialPage), nameof(SocialPage.draw), new Type[] {typeof(SpriteBatch)}),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(SocialPage_draw_Transpiler))
            );

            harmony.Patch(AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(IClickableMenu_populateClickableComponentList_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.gameWindowSizeChanged)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ICLickableMenu_gameWindowSizeChanged_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.readyToClose)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(IClickableMenu_readyToClose_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.changeTab)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameMenu_changeTab_Postfix))
            );

            harmony.Patch(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.receiveLeftClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(GameMenu_receiveLeftClick_Prefix)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameMenu_receiveLeftClick_Postfix))
            );
        }

        #region EVENTS
        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            talkedFilter = new(0, 0, new Filters.TalkedFilter(), OnFilterClicked);
            giftedFilter = new(0, 0, new Filters.GiftedFilter(), OnFilterClicked);
            maxFriendFilter = new(0, 0, new Filters.MaxedFriendshipFilter(), OnFilterClicked);
            unmetFilter = new(0, 0, new Filters.UnmetFilter(), OnFilterClicked);
            filters.Add(talkedFilter);
            filters.Add(giftedFilter);
            filters.Add(maxFriendFilter);
            filters.Add(unmetFilter);

            BetterGameMenuAPI = SHelper.ModRegistry.GetApi<IBetterGameMenuApi>(IDs.BetterGameMenu);

            if(BetterGameMenuAPI is not null)
            {
                BetterGameMenuAPI.OnTabChanged(BetterGameMenuChangeTab);
                BetterGameMenuAPI.OnMenuCreated(BetterGameMenuMenuCreated);
                BetterGameMenuAPI.OnPageReadyToClose(BetterGameMenuPageReadyToClose);
                BetterGameMenuAPI.OnPageCreated(BetterGameMenuPageCreated);
            }

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
                name: () => SHelper.Translation.Get("GMCM-EnableMod"),
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-PreviousSortKey"),
                getValue: () => Config.prevButton,
                setValue: value => Config.prevButton = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-NextSortKey"),
                getValue: () => Config.nextButton,
                setValue: value => Config.nextButton = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-ToggleShowTalkedKey"),
                getValue: () => Config.toggleShowTalked,
                setValue: value => Config.toggleShowTalked = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-ToggleShowGiftedKey"),
                getValue: () => Config.toggleShowGifted,
                setValue: value => Config.toggleShowGifted = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-ToggleShowMaxFriendshipKey"),
                getValue: () => Config.toggleShowMaxFriendship,
                setValue: value => Config.toggleShowMaxFriendship = value
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-ToggleShowUnmetKey"),
                getValue: () => Config.toggleShowUnmet,
                setValue: value => Config.toggleShowUnmet = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseFilter"),
                getValue: () => Config.UseFilter,
                setValue: (value) => { Config.UseFilter = value;
                                       InitElements();}
            );
            
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseButton"),
                getValue: () => Config.UseButton,
                setValue: (value) => { Config.UseButton = value;
                                       InitElements();}
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseDropdown"),
                getValue: () => Config.UseDropdown,
                setValue: (value) => { Config.UseDropdown = value;
                                       InitElements();}
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseShowTalked"),
                getValue: () => Config.UseShowTalked,
                setValue: (value) => {
                    Config.UseShowTalked = value;
                    InitElements();
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseShowGifted"),
                getValue: () => Config.UseShowGifted,
                setValue: (value) => {
                    Config.UseShowGifted = value;
                    InitElements();
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseShowMaxFriendship"),
                getValue: () => Config.UseShowMaxFriendship,
                setValue: (value) => {
                    Config.UseShowMaxFriendship = value;
                    InitElements();
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-UseShowUnmet"),
                getValue: () => Config.UseShowMaxFriendship,
                setValue: (value) => {
                    Config.UseShowMaxFriendship = value;
                    InitElements();
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-AutoFocusSearch"),
                getValue: () => Config.SearchBarAutoFocus,
                setValue: value => Config.SearchBarAutoFocus = value
            );

            configMenu.AddNumberOption(
               mod: ModManifest,
               name: () => SHelper.Translation.Get("GMCM-ButtonOffsetX"),
               getValue: () => Config.ButtonOffsetX,
               setValue: value => Config.ButtonOffsetX = value
           );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-ButtonOffsetY"),
                getValue: () => Config.ButtonOffsetY,
                setValue: value => Config.ButtonOffsetY = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-DropdownOffsetX"),
                getValue: () => Config.DropdownOffsetX,
                setValue: value => Config.DropdownOffsetX = value
            );
            
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-DropdownOffsetY"),
                getValue: () => Config.DropdownOffsetY,
                setValue: value => Config.DropdownOffsetY = value
            );
            
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-FilterOffsetX"),
                getValue: () => Config.FilterOffsetX,
                setValue: value => Config.FilterOffsetX = value
            );
            
            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-FilterOffsetY"),
                getValue: () => Config.FilterOffsetY,
                setValue: value => Config.FilterOffsetY = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-TalkedOffsetX"),
                getValue: () => Config.TalkedOffsetX,
                setValue: value => Config.TalkedOffsetX = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-TalkedOffsetY"),
                getValue: () => Config.TalkedOffsetY,
                setValue: value => Config.TalkedOffsetY = value
            );
        }

        private void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (!WasModEnabled || GetGameMenuPage(Game1.activeClickableMenu) is not SocialPage page)
                return;
            if (Config.prevButton.JustPressed())
            {
                DecrementSort();
            }
            else if (Config.nextButton.JustPressed())
            {
                IncrementSort();
            }

            if(Config.UseFilter && filterField.Value.Selected)
            {
                return;
            }

            if(Config.UseShowGifted && Config.toggleShowGifted.JustPressed())
            {
                giftedFilter.IsFiltering = !giftedFilter.IsFiltering;
                ApplyFilter(page, true);
            }

            if (Config.UseShowTalked && Config.toggleShowTalked.JustPressed())
            {
                talkedFilter.IsFiltering = !talkedFilter.IsFiltering;
                ApplyFilter(page, true);
            }

            if (Config.UseShowMaxFriendship && Config.toggleShowMaxFriendship.JustPressed())
            {
                maxFriendFilter.IsFiltering = !maxFriendFilter.IsFiltering;
                ApplyFilter(page, true);
            }

            if (Config.UseShowUnmet && Config.toggleShowUnmet.JustPressed())
            {
                unmetFilter.IsFiltering = !unmetFilter.IsFiltering;
                ApplyFilter(page, true);
            }
        }

        private void Display_MenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (Config.UseFilter && filterField.Value is not null && e.OldMenu is not ProfileMenu && e.NewMenu is not ProfileMenu)
            {
                // For some reason, when the window is resized it closes and then re-opens the GameMenu back to the same tab, and we don't want to clear the field in that case.
                // That should hopefully be the only time when it goes directly from GameMenu to GameMenu
                if(e.OldMenu is GameMenu && e.NewMenu is GameMenu)
                {
                    return;
                }

                SMonitor.Log("Clearing the filter field.");
                filterField.Value.Text = "";
            }
        }

        public static void Content_LocaleChanged(object sender, LocaleChangedEventArgs args)
        {
            if (dropDown.Value is null)
                return;

            dropDown.Value.dropDownDisplayOptions.Clear();
            dropDown.Value.dropDownOptions.Clear();

            for (int i = 0; i < 4; i++)
            {
                dropDown.Value.dropDownDisplayOptions.Add(SHelper.Translation.Get($"sort-{i}"));
                dropDown.Value.dropDownOptions.Add(SHelper.Translation.Get($"sort-{i}"));
            }

            dropDown.Value.RecalculateBounds();
        }

        public static void Content_AssetRequested(object sender, AssetRequestedEventArgs args)
        {
            if(args.NameWithoutLocale.IsEquivalentTo("Mods/FlyingTNT.SocialPageOrderRedux/MaxHeartsIcon"))
            {
                args.LoadFromModFile<Texture2D>("assets/MaxHeartsIcon.png", AssetLoadPriority.Medium);
            }
            else if (args.NameWithoutLocale.IsEquivalentTo("Mods/FlyingTNT.SocialPageOrderRedux/UnmetIcon"))
            {
                args.LoadFromModFile<Texture2D>("assets/UnmetIcon.png", AssetLoadPriority.Medium);
            }
        }

        #endregion

        #region SOCIAL_PAGE_SETUP_PATCHES

        /// <summary>
        /// Prevents a null error due to the order that BetterGameMenu events fire relative to our patches.
        /// </summary>
        public static void BetterGameMenuMenuCreated(IClickableMenu menu)
        {
            try
            {
                InitElements();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuMenuCreated)}: {ex}", LogLevel.Error);
            }
        }

        public static void SocialPage_Constructor_Postfix(SocialPage __instance)
        {
            if (!Config.EnableMod)
                return;

            InitElements();
            UpdateElementPositions(__instance);
        }

        public static void SocialPage_FindSocialCharacters_Postfix(List<SocialEntry> __result)
        {
            // __result is the list of all of the entries
            allEntries.Value.Clear();
            allEntries.Value.AddRange(__result);

            // Remove all of the characters affected by the filter
            ApplyFilter(__result);

            // Sort the characters
            __result.Sort(GetSort());
        }

        public static void IClickableMenu_populateClickableComponentList_Postfix(IClickableMenu __instance)
        {
            if (!Config.EnableMod || __instance is not SocialPage)
                return;

            if(Config.UseButton)
            {
                __instance.allClickableComponents.Add(button.Value);
            }

            __instance.allClickableComponents.AddRange(filters.Where(filter => filter.IsEnabled));
        }

        /// <remarks>
        /// As it stands, this is not necessary as logging the stack trace shows that the SocialPage constructor is called every time the window is resized for some reason, but I stll do this patch just to be
        /// safe.
        /// </remarks>
        public static void ICLickableMenu_gameWindowSizeChanged_Postfix(IClickableMenu __instance)
        {
            if(__instance is not SocialPage page)
            {
                return;
            }

            UpdateElementPositions(page);
        }

        #endregion

        #region DRAW_PATCH

        public static IEnumerable<CodeInstruction> SocialPage_draw_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // This needs to be a transpiler instead of a prefix/postfix because the page starts a new SpriteBatch at the start and end of the method, and we want to use the same SpriteBatch it uses.
            SMonitor.Log("Transpiling SocialPage.draw");
            var codes = new List<CodeInstruction>(instructions);
            int index = codes.FindLastIndex(ci => ci.opcode == OpCodes.Call && (MethodInfo)ci.operand == AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.drawTextureBox), new Type[] { typeof(SpriteBatch), typeof(Texture2D), typeof(Rectangle), typeof(int), typeof(int), typeof(int), typeof(int), typeof(Color), typeof(float), typeof(bool), typeof(float) }));
            if(index > -1)
            {
                SMonitor.Log("Inserting dropdown draw method");
                codes.Insert(index + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(DrawDropDown))));
                codes.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_1));
                codes.Insert(index + 1, new CodeInstruction(OpCodes.Ldarg_0));

            }
            return codes.AsEnumerable();
        }

        public static void DrawDropDown(SocialPage page, SpriteBatch b)
        {
            if (!Config.EnableMod)
                return;

            try
            {
                if (Config.UseFilter)
                {
                    filterField.Value.Draw(b);
                }

                if (Config.UseDropdown)
                {
                    dropDown.Value.draw(b, GetDropdownX(page), GetDropdownY(page));
                    if (SHelper.Input.IsDown(SButton.MouseLeft) && AccessTools.FieldRefAccess<OptionsDropDown, bool>(dropDown.Value, "clicked") && dropDown.Value.dropDownBounds.Contains(Game1.getMouseX() - GetDropdownX(page), Game1.getMouseY() - GetDropdownY(page)))
                    {
                        dropDown.Value.selectedOption = (int)Math.Max(Math.Min((float)(Game1.getMouseY() - GetDropdownY(page) - dropDown.Value.dropDownBounds.Y) / (float)dropDown.Value.bounds.Height, (float)(dropDown.Value.dropDownOptions.Count - 1)), 0f);
                    }
                }

                if (Config.UseButton)
                {
                    button.Value.draw(b);
                }

                foreach(SocialPageFilter filter in filters)
                {
                    if(filter.IsEnabled)
                    {
                        filter.draw(b);
                    }
                }
            }
            catch(Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(DrawDropDown)}: {ex}", LogLevel.Error);
            }
        }

        #endregion

        #region PLAYER_INPUT_PATCHES
        public static void GameMenu_receiveLeftClick_Prefix()
        {
            isInGameMenuLeftClick.Value = true;
        }

        public static void GameMenu_receiveLeftClick_Postfix()
        {
            isInGameMenuLeftClick.Value = false;
        }

        public static void IClickableMenu_readyToClose_Postfix(IClickableMenu __instance, ref bool __result)
        {
            // isInGameMenuLeftClick is necessary so that the player doesn't need to deselect the field before moving to another tab
            if (!Config.EnableMod || __instance is not SocialPage || filterField.Value is null || isInGameMenuLeftClick.Value || BetterGameMenuAPI is not null)
                return;

            // If the filter is selected, make the result false. This is because some mods that add their own menus will overwrite the current one when their menu's key is pressed,
            // but if the player has the filter selected, they didn't mean to open the other menu; they were just typing in the search bar.
            __result &= !filterField.Value.Selected;
        }

        /// <summary>
        /// Mirror of <see cref="IClickableMenu_readyToClose_Postfix(IClickableMenu, ref bool)"/> for BetterGameMenu
        /// </summary>
        public static void BetterGameMenuPageReadyToClose(IPageReadyToCloseEvent readyToCloseEvent)
        {
            try
            {
                if (filterField.Value is null)
                {
                    return;
                }

                if (readyToCloseEvent.Page is not SocialPage)
                {
                    return;
                }

                if (SHelper.Input.IsDown(SButton.Escape))
                {
                    return;
                }

                // We need to check this instead of checking PageReadyToCloseReason.MenuClosing because PageReadyToCloseReason.MenuClosing is flagged both for clicking the X and pressing the menu button, and
                // we don't want to close for the menu button if the filter is selected.
                if (readyToCloseEvent.Menu.upperRightCloseButton.bounds.Contains(Game1.getMousePosition(true)))
                {
                    return;
                }

                if (readyToCloseEvent.Reason is not (PageReadyToCloseReason.TabChanging or PageReadyToCloseReason.TabReloading))
                {
                    readyToCloseEvent.ReadyToClose &= !filterField.Value.Selected;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuPageReadyToClose)}: {ex}", LogLevel.Error);
            }
        }

        public static void SocialPage_performHoverAction_Postfix(SocialPage __instance, int x, int y)
        {
            if (!Config.EnableMod)
                return;

            if (Config.UseFilter && filterField.Value is not null)
            {
                filterField.Value.Hover(x, y);
            }

            if (Config.UseButton && button.Value is not null)
            {
                if (button.Value.bounds.Contains(x, y))
                {
                    __instance.hoverText = SHelper.Translation.Get($"sort-by") + SHelper.Translation.Get($"sort-{CurrentSort}");
                }
            }

            foreach(SocialPageFilter filter in filters)
            {
                if(filter.IsEnabled && filter.bounds.Contains(x, y))
                {
                    __instance.hoverText = filter.HoverText;
                }
            }
        }

        public static bool SocialPage_receiveLeftClick_Prefix(SocialPage __instance, int x, int y)
        {
            if (!Config.EnableMod)
                return true;

            if (Config.UseDropdown && dropDown.Value.bounds.Contains(x - GetDropdownX(__instance), y - GetDropdownY(__instance)))
            {
                dropDown.Value.receiveLeftClick(x - GetDropdownX(__instance), y - GetDropdownY(__instance));
                return false;
            }

            if(Config.UseButton)
            {
                if (button.Value.bounds.Contains(x, y))
                {
                    IncrementSort();
                    return false;
                }
            }

            if (Config.UseFilter)
                filterField.Value.Update();

            foreach (SocialPageFilter filter in filters)
            {
                if (filter.IsEnabled)
                {
                    filter.receiveLeftClick(x, y);
                }
            }

            return true;
        }

        public static bool SocialPage_releaseLeftClick_Prefix(SocialPage __instance, int x, int y)
        {
            if (!Config.EnableMod || !Config.UseDropdown)
                return true;

            if (AccessTools.FieldRefAccess<OptionsDropDown, bool>(dropDown.Value, "clicked"))
            {
                if(dropDown.Value.dropDownBounds.Contains(x - GetDropdownX(__instance), y - GetDropdownY(__instance)))
                {
                    dropDown.Value.leftClickReleased(x - GetDropdownX(__instance), y - GetDropdownY(__instance));
                }
                return false;
            }
            return true;
        }

        public static bool SocialPage_recieveKeyPress_Prefix(IClickableMenu __instance, Keys key)
        {
            if (!Config.EnableMod || __instance is not SocialPage socialPage || !Config.UseFilter || filterField.Value is null || !filterField.Value.Selected || Game1.options.gamepadControls)
                return true;

            if(key == Keys.Escape)
            {
                filterField.Value.Selected = false;
                return true;
            }

            ApplyFilter(socialPage);
            return false;
        }

        public static bool GameMenu_recieveKeyPress_Prefix(GameMenu __instance, Keys key)
        {
            if (!Config.EnableMod || __instance.GetCurrentPage() is not SocialPage socialPage || !Config.UseFilter || filterField.Value is null || !filterField.Value.Selected || Game1.options.gamepadControls)
                return true;

            if (key == Keys.Escape)
            {
                filterField.Value.Selected = false;
                return true;
            }

            ApplyFilter(socialPage);
            return false;
        }

        public static void GameMenu_changeTab_Postfix(GameMenu __instance)
        {
            if (!Config.EnableMod)
                return;

            if (__instance.GetCurrentPage() is SocialPage page)
            {
                if (Config.UseButton)
                    __instance.tabs[GameMenu.inventoryTab].leftNeighborID = buttonId;

                if (Config.UseFilter && filterField.Value is not null && !Game1.options.gamepadControls)
                    filterField.Value.Selected = Config.SearchBarAutoFocus;

                ApplyFilter(page);
                if(Game1.options.SnappyMenus)
                    __instance.snapToDefaultClickableComponent();
            }
            else
            {
                filterField.Value.Selected = false;
            }
        }

        /// <summary>
        /// Mirror of <see cref="GameMenu_changeTab_Postfix(GameMenu)"/> for BetterGameMenu
        /// </summary>
        public static void BetterGameMenuChangeTab(ITabChangedEvent e)
        {
            try
            {
                if (BetterGameMenuAPI.ActivePage is SocialPage page)
                {
                    if (Config.UseFilter && filterField.Value is not null && !Game1.options.gamepadControls)
                    {
                        filterField.Value.Selected = Config.SearchBarAutoFocus;
                    }

                    UpdateElementPositions(page);

                    // I don't think this is necessary, but I'm not removing it because it also isn't hurting anything.
                    ApplyFilter(page);
                    if (Game1.options.SnappyMenus)
                    {
                        page.snapToDefaultClickableComponent();
                    }
                }
                else
                {
                    filterField.Value.Selected = false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuChangeTab)}: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Makes sure the filter is selected as necessary when the page is loaded
        /// </summary>
        /// <remarks>
        /// This is usually handled by <see cref="BetterGameMenuChangeTab(ITabChangedEvent)"/>, but that method doesn't fire when returning from ProfileMenus and the such
        /// </remarks>
        public static void BetterGameMenuPageCreated(IPageCreatedEvent e)
        {
            if(e.Page is SocialPage)
            {
                if (Config.UseFilter && filterField.Value is not null && !Game1.options.gamepadControls)
                {
                    filterField.Value.Selected = Config.SearchBarAutoFocus;
                }
            }
        }

        #endregion

        #region SORT_AND_FILTER_METHODS

        public static void ResortSocialList(SocialPage page = null)
        {
            if(page is null)
            {
                if (GetGameMenuPage(Game1.activeClickableMenu) is not SocialPage sp)
                {
                    SMonitor.Log("Skipping sort.");
                    return;
                }

                page = sp;
            }

            List<NameSpriteSlot> nameSprites = new();

            // Make sure the SocialEntries, sprites, and characterSlots have the same number of elements. If they don't use the lowest number of elements.
            // It should be impossible for them to not be equal, but somebody got an index error somewhere in this function so I'm just being safe.
            int count = page.SocialEntries.Count;
            if(count != page.sprites.Count || count != page.characterSlots.Count)
            {
                SMonitor.Log($"The Social Entry, sprites, and character slot counts are not equal ({page.SocialEntries.Count} vs {page.sprites.Count} vs {page.characterSlots.Count}).");
                count = Math.Min(Math.Min(count, page.SocialEntries.Count), page.characterSlots.Count);
            }

            for (int i = 0; i < count; i++)
            {
                nameSprites.Add(new NameSpriteSlot(page.SocialEntries[i], page.sprites[i], page.characterSlots[i]));
            }

            // Sort the nameSprites list based on the current sort
            Comparison<SocialEntry> sort = GetSort();
            nameSprites.Sort((NameSpriteSlot x, NameSpriteSlot y) => sort(x.entry, y.entry));

            var bounds = page.characterSlots.Select(slot => slot.bounds).ToList();

            for (int i = 0; i < count; i++)
            {
                NameSpriteSlot nameSpriteSlot = nameSprites[i];

                nameSpriteSlot.slot.myID = i;
                nameSpriteSlot.slot.downNeighborID = i + 1;
                nameSpriteSlot.slot.upNeighborID = i - 1;

                // If this is the first slot, set its up neighbor to the social tab.
                if (nameSpriteSlot.slot.upNeighborID < 0)
                {
                    nameSpriteSlot.slot.upNeighborID = GameMenu.region_socialTab;
                }

                // If we have the button, set the slot's left neighbor to the button
                if(Config.UseButton)
                {
                    nameSpriteSlot.slot.leftNeighborID = buttonId;
                }

                nameSpriteSlot.slot.bounds = bounds[i];

                // Update the page's characterSlots, sprites, and SocialEntries
                page.sprites[i] = nameSpriteSlot.sprite;
                page.characterSlots[i] = nameSpriteSlot.slot;
                page.SocialEntries[i] = nameSpriteSlot.entry;
            }

            page.updateSlots();
        }

        private static Comparison<SocialEntry> GetSort()
        {
            switch (CurrentSort)
            {
                case 0: // friend asc
                    SMonitor.Log("sorting by friend asc");
                    return delegate (SocialEntry x, SocialEntry y)
                    {
                        if (x.IsPlayer && y.IsPlayer)
                            return 0;

                        if (x.IsPlayer)
                            return -1;

                        if (y.IsPlayer)
                            return 1;

                        bool xIsNullFriendship = x.Friendship is null || x.IsChild;
                        bool yIsNullFriendship = y.Friendship is null || y.IsChild;
                        if (xIsNullFriendship && yIsNullFriendship)
                            return 0;

                        if (xIsNullFriendship)
                            return 1;

                        if (yIsNullFriendship)
                            return -1;

                        int c = x.Friendship.Points.CompareTo(y.Friendship.Points);
                        if (c == 0)
                            c = x.DisplayName.CompareTo(y.DisplayName);
                        return c;
                    };
                case 1: // friend desc
                    SMonitor.Log("sorting by friend desc");
                    return delegate (SocialEntry x, SocialEntry y)
                    {
                        if (x.IsPlayer && y.IsPlayer)
                            return 0;

                        if (x.IsPlayer)
                            return -1;

                        if (y.IsPlayer)
                            return 1;

                        bool xIsNullFriendship = x.Friendship is null || x.IsChild;
                        bool yIsNullFriendship = y.Friendship is null || y.IsChild;
                        if (xIsNullFriendship && yIsNullFriendship)
                            return 0;

                        if (xIsNullFriendship)
                            return 1;

                        if (yIsNullFriendship)
                            return -1;

                        int c = -x.Friendship.Points.CompareTo(y.Friendship.Points);
                        if (c == 0)
                            c = x.DisplayName.CompareTo(y.DisplayName);
                        return c;

                    };
                case 2: // alpha asc
                    SMonitor.Log("sorting by alpha asc");
                    return delegate (SocialEntry x, SocialEntry y)
                    {
                        if (x.IsPlayer && y.IsPlayer)
                            return 0;

                        if (x.IsPlayer)
                            return -1;

                        if (y.IsPlayer)
                            return 1;

                        if (!x.IsMet && !y.IsMet)
                            return 0;
                        if (!x.IsMet)
                            return 1;
                        if (!y.IsMet)
                            return -1;

                        return x.DisplayName.CompareTo(y.DisplayName);
                    };
                case 3: // alpha desc
                    SMonitor.Log("sorting by alpha desc");
                    return delegate (SocialEntry x, SocialEntry y)
                    {
                        if (x.IsPlayer && y.IsPlayer)
                            return 0;

                        if (x.IsPlayer)
                            return -1;

                        if (y.IsPlayer)
                            return 1;

                        if (!x.IsMet && !y.IsMet)
                            return 0;
                        if (!x.IsMet)
                            return 1;
                        if (!y.IsMet)
                            return -1;
                        return -x.DisplayName.CompareTo(y.DisplayName);
                    };
                default:
                    goto case 2;
            }
        }
        public static void IncrementSort()
        {
            CurrentSort++;
            CurrentSort %= 4;
            ResortSocialList();
            if (Config.UseDropdown)
            {
                dropDown.Value.selectedOption = CurrentSort;
            }
        }

        public static void DecrementSort()
        {
            CurrentSort--;
            if (CurrentSort < 0)
                CurrentSort = 3;
            ResortSocialList();
            if (Config.UseDropdown)
            {
                dropDown.Value.selectedOption = CurrentSort;
            }
        }

        private static void ApplyFilter(SocialPage socialPage, bool keepPosition = false)
        {
            if (!Config.EnableMod)
                return;

            // Filter the SocialEntries
            ApplyFilter(socialPage.SocialEntries);

            // Recalculate the number of farmers (affects the way the first {# of farmers} slots render)
            socialPage.numFarmers = socialPage.SocialEntries.Count((SocialEntry p) => p.IsPlayer);

            if (!keepPosition)
            {
                // Move the slot position back to the top
                for (int i = 0; i < socialPage.SocialEntries.Count; i++)
                {
                    if (!socialPage.SocialEntries[i].IsPlayer)
                    {
                        socialPage.slotPosition = i;
                        break;
                    }
                }
            }
            else
            {
                socialPage.slotPosition = Math.Max(0, Math.Min(socialPage.SocialEntries.Count - 5, socialPage.slotPosition));
            }

            // Recreate the characterSlots and sprites components
            socialPage.CreateComponents();

            // Reapply the sort (it will be unsorted because allEntries is unsorted)
            ResortSocialList(socialPage);

            SHelper.Reflection.GetMethod(socialPage, "setScrollBarToCurrentIndex").Invoke();
            socialPage.updateSlots();
        }

        private static void ApplyFilter(List<SocialEntry> entries)
        {
            string filterString = Config.UseFilter ? filterField.Value?.Text ?? "" : "";

            entries.Clear();
            entries.AddRange(filterString is "" ? allEntries.Value : allEntries.Value.Where((entry) => entry.DisplayName.ToLower().StartsWith(filterString.ToLower())));

            foreach (SocialPageFilter filter in filters)
            {
                if (filter.IsEnabled && filter.IsFiltering)
                {
                    entries.RemoveAll(entry => filter.Filter.ShouldFilter(entry));
                }
            }

            lastFilterString.Value = filterString;
        }

        internal static void OnFilterClicked(bool _)
        {
            if (GetGameMenuPage(Game1.activeClickableMenu) is SocialPage page)
            {
                ApplyFilter(page, true);
            }
        }

        #endregion

        #region ELEMENT_SETUP_METHODS

        private static IClickableMenu GetGameMenuPage(IClickableMenu menu)
        {
            if (menu is GameMenu gameMenu)
                return gameMenu.GetCurrentPage();
            else if (BetterGameMenuAPI is not null && menu is not null)
                return BetterGameMenuAPI.GetCurrentPage(menu);
            return null;
        }

        private static void UpdateElementPositions(SocialPage page)
        {
            if(Config.UseButton)
            {
                button.Value.bounds = GetButtonRectangle(page);
            }

            if (Config.UseFilter)
            {
                filterField.Value.X = page.xPositionOnScreen + page.width / 2 - filterField.Value.Width / 2 + Config.FilterOffsetX;
                filterField.Value.Y = page.yPositionOnScreen + page.height + (Config.UseDropdown ? dropDown.Value.bounds.Height + dropdownYOffset + 20 : 0) + Config.FilterOffsetY;
            }

            int baseFilterOffsetX = page.xPositionOnScreen + talkedXOffset + Config.TalkedOffsetX;
            int baseFilterOffsetY = page.yPositionOnScreen + talkedYOffset + Config.TalkedOffsetY;

            foreach (SocialPageFilter filter in filters)
            {
                if(!filter.IsEnabled)
                {
                    continue;
                }

                filter.bounds.X = baseFilterOffsetX + filter.Filter.OffsetX;
                filter.bounds.Y = baseFilterOffsetY + filter.Filter.OffsetY;

                baseFilterOffsetY += filter.bounds.Height + 4;
            }
        }

        public static int GetDropdownX(SocialPage page)
        {
            return page.xPositionOnScreen + page.width / 2 - dropDown.Value.bounds.Width / 2 + Config.DropdownOffsetX;
        }
        public static int GetDropdownY(SocialPage page)
        {
            return page.yPositionOnScreen + page.height + Config.DropdownOffsetY + dropdownYOffset;
        }

        public static int GetButtonX(SocialPage page)
        {
            return page.xPositionOnScreen + buttonXOffset + Config.ButtonOffsetX + (BetterGameMenuAPI is null ? 0 : -16);
        }

        public static int GetButtonY(SocialPage page)
        {
            return page.yPositionOnScreen + Config.ButtonOffsetY;
        }

        public static Rectangle GetButtonRectangle(SocialPage page)
        {
            return new Rectangle(GetButtonX(page), GetButtonY(page), buttonTextureSource.Width * 4, buttonTextureSource.Height * 4);
        }

        public static void InitElements()
        {
            if (!WasModEnabled)
                return;

            if (filterField.Value is null)
            {
                filterField.Value = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
                {
                    Text = ""
                };

                filterField.Value.OnEnterPressed += sender => sender.Selected = false;
                filterField.Value.OnTabPressed += sender => sender.Selected = false;
            }

            if(dropDown.Value is null)
            {
                dropDown.Value = new MyOptionsDropDown("");

                for (int i = 0; i < 4; i++)
                {
                    dropDown.Value.dropDownDisplayOptions.Add(SHelper.Translation.Get($"sort-{i}"));
                    dropDown.Value.dropDownOptions.Add(SHelper.Translation.Get($"sort-{i}"));
                }
                dropDown.Value.RecalculateBounds();
                dropDown.Value.selectedOption = CurrentSort;
            }

            button.Value ??= new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, buttonTextureSource, 4, false)
                {
                    rightNeighborID = ClickableComponent.SNAP_AUTOMATIC,
                    rightNeighborImmutable = true,
                    myID = buttonId
                };

            SetupFilterNeighbors();
        }

        public static void SetupFilterNeighbors()
        {
            ClickableComponent upNeighbor = Config.UseButton ? button.Value : null;
            int currentId = buttonId;

            foreach(SocialPageFilter filter in filters)
            {
                if(!filter.IsEnabled)
                {
                    continue;
                }

                filter.myID = ++currentId;
                filter.rightNeighborID = 0;

                if(upNeighbor is not null)
                {
                    filter.upNeighborID = upNeighbor.myID;
                    upNeighbor.downNeighborID = filter.myID;
                }

                upNeighbor = filter;
            }
        }

        #endregion

        #region API
        public override object GetApi()
        {
            return new SocialPageOrderAPI();
        }
        #endregion
    }

    internal class NameSpriteSlot
    {
        public SocialEntry entry;
        public ClickableTextureComponent sprite;
        public ClickableTextureComponent slot;

        public NameSpriteSlot(SocialEntry obj, ClickableTextureComponent clickableTextureComponent, ClickableTextureComponent slotComponent)
        {
            entry = obj;
            sprite = clickableTextureComponent;
            slot = slotComponent;
        }
    }
}