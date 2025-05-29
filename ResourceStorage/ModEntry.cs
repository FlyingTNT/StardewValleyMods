using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using ResourceStorage.BetterCrafting;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using Common.Integrations;
using Common.Utilities;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ResourceStorage
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

        public static ModEntry context;
        public static string dictKey = "aedenthorn.ResourceStorage/dictionary"; // Not updating to FlyingTNT.ResourceStorage for backwards compatibility
        public static Dictionary<long, Dictionary<string, long>> resourceDict = new();

        public const string sharedDictionaryKey = "FlyingTNT.ResourceStorage/sharedDictionary";
        public const string autoStoreKey = "FlyingTNT.ResourceStorage/autoStore";
        public const string resourceIconKey = "FlyingTNT.ResourceStorage/resourceIcon";

        public static IBetterGameMenuApi BetterGameMenuAPI = null;
        public static PerScreen<IClickableMenu> gameMenu = new PerScreen<IClickableMenu>();
        public static PerScreen<ClickableTextureComponent> resourceButton = new PerScreen<ClickableTextureComponent>();

        private static readonly PerScreen<List<string>> cachedAutoStore = new PerScreen<List<string>>(() => new());
        public static List<string> AutoStore => cachedAutoStore.Value;


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            context = this;

            SMonitor = Monitor;
            SHelper = helper;

            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.Saving += GameLoop_Saving;
            Helper.Events.Content.AssetRequested += Content_AssetRequested;
            Helper.Events.Content.AssetsInvalidated += Content_AssetsInvalidated;
            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;

            SharedResourceManager.Initialize(Monitor, helper, Config, ModManifest);

            Harmony harmony = new Harmony(ModManifest.UniqueID);

            #region INVENTORY_PATCHES
            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.ReduceId)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_ReduceId_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.Reduce)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_Reduce_Prefix)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_Reduce_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.CountId)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_CountId_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_ContainsId_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_ContainsId2_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Inventory), nameof(Inventory.GetById)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Inventory_GetById_Postfix))
            );
            #endregion

            #region FARMER_PATCHES
            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.addItemToInventory), new Type[] { typeof(Item), typeof(List<Item>) }),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_addItemToInventory_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.getItemCount)),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_getItemCount_Transpiler))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), new Type[] { typeof(Item) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_couldInventoryAcceptThisItem_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), new Type[] { typeof(string), typeof(int), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_couldInventoryAcceptThisItem2_Postfix))
            );
            #endregion

            #region CRAFTING_RECIPE_PATCHES
            harmony.Patch(
                original: AccessTools.Method(typeof(CraftingRecipe), nameof(CraftingRecipe.ConsumeAdditionalIngredients)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(CraftingRecipe_ConsumeAdditionalIngredientsPrefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(CraftingRecipe), nameof(CraftingRecipe.getCraftableCount), new Type[] { typeof(IList<Item>) }),
                transpiler: new HarmonyMethod(typeof(ModEntry), nameof(CraftingRecipe_getCraftableCount_Transpiler))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(CraftingRecipe), nameof(CraftingRecipe.consumeIngredients)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(CraftingRecipe_consumeIngredients_Prefix)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(CraftingRecipe_consumeIngredients_Postfix))
            );
            #endregion

            #region GAME_MENU_PATCHES
            harmony.Patch(
                original: AccessTools.Constructor(typeof(GameMenu), new Type[] { typeof(bool) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(GameMenu_Constructor_Postfix))
            );
            #endregion

            #region INVENTORY_PAGE_PATCHES
            harmony.Patch(
                original: AccessTools.Constructor(typeof(InventoryPage), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) }),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(InventoryPage_Constructor_Postfix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(InventoryPage), nameof(InventoryPage.draw), new Type[] {typeof(SpriteBatch)}),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(InventoryPage_draw_Prefix))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(InventoryPage), nameof(InventoryPage.performHoverAction)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(InventoryPage_performHoverAction_Prefix))
            );
            
            harmony.Patch(
                original: AccessTools.Method(typeof(InventoryPage), nameof(InventoryPage.receiveLeftClick)),
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(InventoryPage_receiveLeftClick_Prefix))
            );
            #endregion

            #region ICLICKABLE_MENU_PATCHES
            harmony.Patch(
                original: AccessTools.Method(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(IClickableMenu_populateClickableComponentList_Postfix))
            );
            #endregion
        }

        public void GameLoop_Saving(object sender, SavingEventArgs e)
        {
            SaveResourceDictionary(Game1.player);

            // We always save it after we edit AutoStore, so it shouldn't be necessary to save it here, but I'm doing it just to be safe.
            PerPlayerConfig.SaveConfigOption(Game1.player, autoStoreKey, string.Join(',', AutoStore));
        }

        public void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            SMonitor.Log("Removing this player's dictionary.");
            resourceDict.Remove(Game1.player.UniqueMultiplayerID);
            cachedAutoStore.Value = PerPlayerConfig.LoadConfigOption(Game1.player, autoStoreKey, defaultValue: Config.AutoStore).Split(',').ToList();
        }

        public void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            SMonitor.Log("Removing this player's dictionary.");
            resourceDict.Remove(Game1.player.UniqueMultiplayerID);
        }

        public void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs args)
        {
            if(!Config.ResourcesKey.JustPressed())
            {
                return;
            }

            if(IsGameMenu(Game1.activeClickableMenu) && GetGameMenuPage(Game1.activeClickableMenu) is InventoryPage page)
            {
                page.hoverText = "";
                Game1.playSound("bigSelect");
                gameMenu.Value = Game1.activeClickableMenu;
                Game1.activeClickableMenu = new ResourceMenu();
            }
            else if(Game1.activeClickableMenu is ResourceMenu resourceMenu && resourceMenu.readyToClose())
            {
                resourceMenu.exitThisMenu();
                Game1.activeClickableMenu = gameMenu.Value;
            }
        }

        public void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            BetterGameMenuAPI = SHelper.ModRegistry.GetApi<IBetterGameMenuApi>(IDs.BetterGameMenu);

            if (BetterGameMenuAPI is not null)
            {
                BetterGameMenuAPI.OnMenuCreated(BetterGameMenuMenuCreated);
            }    

            BetterCraftingIntegration.Initialize(SMonitor, SHelper, Config);

            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = SHelper.ModRegistry.GetApi<IGenericModConfigMenuApi>(IDs.GMCM);
            if (configMenu is not null)
            {
                // register mod
                configMenu.Register(
                    mod: ModManifest,
                    reset: () => Config = new ModConfig(),
                    save: () => SHelper.WriteConfig(Config)
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModEnabled_Name"),
                    getValue: () => Config.ModEnabled,
                    setValue: value => Config.ModEnabled = value
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_AutoUse_Name"),
                    getValue: () => Config.AutoUse,
                    setValue: value => Config.AutoUse = value
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ShowMessage_Name"),
                    getValue: () => Config.ShowMessage,
                    setValue: value => Config.ShowMessage = value
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_UseSharedResources_Name"),
                    getValue: () => SharedResourceManager.UseSharedResources.Value,
                    setValue: value => SharedResourceManager.ChangeShouldUseShared(value)
                );

                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_AutoSelectSearchBar_Name"),
                    getValue: () => Config.AutoSelectSearchBar,
                    setValue: value => Config.AutoSelectSearchBar = value
                );

                // KEYBINDS
                configMenu.AddKeybindList(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ResourcesKey_Name"),
                    getValue: () => Config.ResourcesKey,
                    setValue: value => Config.ResourcesKey = value
                );
                
                configMenu.AddKeybind(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey1_Name"),
                    getValue: () => Config.ModKey1,
                    setValue: value => Config.ModKey1 = value
                );
                
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey1Amount_Name"),
                    getValue: () => Config.ModKey1Amount,
                    setValue: value => Config.ModKey1Amount = value
                );
                
                configMenu.AddKeybind(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey2_Name"),
                    getValue: () => Config.ModKey2,
                    setValue: value => Config.ModKey2 = value
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey2Amount_Name"),
                    getValue: () => Config.ModKey2Amount,
                    setValue: value => Config.ModKey2Amount = value
                );

                configMenu.AddKeybind(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey3_Name"),
                    getValue: () => Config.ModKey3,
                    setValue: value => Config.ModKey3 = value
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_ModKey3Amount_Name"),
                    getValue: () => Config.ModKey3Amount,
                    setValue: value => Config.ModKey3Amount = value
                );

                // ICON POSITIONS
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_IconOffsetX_Name"),
                    getValue: () => Config.IconOffsetX,
                    setValue: value => Config.IconOffsetX = value
                );
                
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_IconOffsetY_Name"),
                    getValue: () => Config.IconOffsetY,
                    setValue: value => Config.IconOffsetY = value
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_SearchBarOffsetX_Name"),
                    getValue: () => Config.SearchBarOffsetX,
                    setValue: value => Config.SearchBarOffsetX = value
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_SearchBarOffsetY_Name"),
                    getValue: () => Config.SearchBarOffsetY,
                    setValue: value => Config.SearchBarOffsetY = value
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_SortButtonOffsetX_Name"),
                    getValue: () => Config.SortButtonOffsetX,
                    setValue: value => Config.SortButtonOffsetX = value
                );

                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => SHelper.Translation.Get("GMCM_Option_SortButtonOffsetY_Name"),
                    getValue: () => Config.SortButtonOffsetY,
                    setValue: value => Config.SortButtonOffsetY = value
                );
            }

            IQuickSaveAPI quickSaveAPI = SHelper.ModRegistry.GetApi<IQuickSaveAPI>(IDs.QuickSave);
            if (quickSaveAPI is not null)
            {
                quickSaveAPI.SavingEvent += (o, _) =>
                {
                    GameLoop_Saving(o, new SavingEventArgs());
                    SharedResourceManager.GameLoop_Saving(o, new SavingEventArgs());
                };

                quickSaveAPI.LoadedEvent += (o, _) =>
                {
                    GameLoop_SaveLoaded(o, new SaveLoadedEventArgs());
                    SharedResourceManager.GameLoop_SaveLoaded(o, new SaveLoadedEventArgs());
                };
            }
        }

        public static void Content_AssetRequested(object sender, AssetRequestedEventArgs args)
        {
            if(args.NameWithoutLocale.IsEquivalentTo(resourceIconKey))
            {
                args.LoadFrom(() =>
                {
                    Texture2D texture = new(Game1.graphics.GraphicsDevice, 22, 22);
                    Color[] data = new Color[22 * 22];
                    Game1.mouseCursors.GetData(0, new Rectangle(116, 442, 22, 22), data, 0, 22 * 22);
                    texture.SetData(data);
                    return texture;
                }, AssetLoadPriority.Low);
            }
        }

        public static void Content_AssetsInvalidated(object sender, AssetsInvalidatedEventArgs args)
        {
            if(args.NamesWithoutLocale.Any(name => name.IsEquivalentTo(Game1.mouseCursorsName)))
            {
                SHelper.GameContent.InvalidateCache(resourceIconKey);
            }
        }
    }
}