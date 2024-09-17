using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;

namespace ResourceStorage.BetterCrafting
{
    internal class BetterCraftingIntegration
    {
        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static ModConfig Config;

        public static bool IsBetterCraftingLoaded { get; private set; } = false;
        public static readonly PerScreen<bool> IsBetterCraftingMenuOpen = new PerScreen<bool>(()=>false);
        private static ResourceStorageInventoryProvider InventoryProvider;
        public static ResourceStorageInventory ThisPlayerStorage => ResourceStorageInventoryProvider.playerInventory.Value;
        public static IBetterCrafting BetterCraftingAPI { get; private set; }

        public static void Initialize(IMonitor monitor, IModHelper helper, ModConfig config)
        {
            try
            {
                // Make sure this isn't run twice
                if (IsBetterCraftingLoaded)
                {
                    return;
                }

                SMonitor = monitor;
                SHelper = helper;
                Config = config;

                BetterCraftingAPI = SHelper.ModRegistry.GetApi<IBetterCrafting>("leclair.bettercrafting");

                if (BetterCraftingAPI is null)
                    return;

                IsBetterCraftingLoaded = true;
                InventoryProvider = new();

                BetterCraftingAPI.RegisterInventoryProvider(typeof(ResourceStorageInventory), InventoryProvider);

                BetterCraftingAPI.MenuSimplePopulateContainers += BetterCrafting_MenuPopulateContainers;
                BetterCraftingAPI.PostCraft += BetterCrafting_PostCraft;
                BetterCraftingAPI.MenuClosing += BetterCrafting_MenuClosing;

                SHelper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            }
            catch(Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Initialize)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void BetterCrafting_MenuPopulateContainers(ISimplePopulateContainersEvent e)
        {
            try
            {
                IsBetterCraftingMenuOpen.Value = true;
                ThisPlayerStorage.ReloadFromFarmerResources();
                e.Containers.Add(new Tuple<object, GameLocation>(ThisPlayerStorage, null));
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterCrafting_MenuPopulateContainers)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void BetterCrafting_PostCraft(IPostCraftEvent e)
        {
            try
            {
                ThisPlayerStorage.SquareWithFarmerResources();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterCrafting_PostCraft)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs args)
        {
            try
            {
                ResourceStorageInventoryProvider.playerInventory.Value = new(Game1.player);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLoop_SaveLoaded)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void BetterCrafting_MenuClosing(object sender)
        {
            try
            {
                IsBetterCraftingMenuOpen.Value = false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterCrafting_MenuClosing)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void NotifyResourceChange(string itemId, int changeAmount, long playerUniqueId)
        {
            try
            {
                if (!(IsBetterCraftingLoaded && IsBetterCraftingMenuOpen.Value && playerUniqueId == ThisPlayerStorage.OwnerId))
                    return;

                ThisPlayerStorage.NotifyOfChangeInResourceStorage(itemId, changeAmount);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NotifyResourceChange)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}
