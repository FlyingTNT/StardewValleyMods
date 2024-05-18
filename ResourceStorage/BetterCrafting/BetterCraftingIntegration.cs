using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
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
        public static readonly PerScreen<ResourceStorageInventory> ThisPlayerStorage = new PerScreen<ResourceStorageInventory>(()=>null);
        public static IBetterCrafting BetterCraftingAPI { get; private set; }

        public static void Initialize(IMonitor monitor, IModHelper helper, ModConfig config)
        {
            // Make sure this isn't run twice
            if(IsBetterCraftingLoaded)
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

            BetterCraftingAPI.MenuPopulateContainers += BetterCrafting_MenuPopulateContainers;
            BetterCraftingAPI.PostCraft += BetterCrafting_PostCraft;

            SHelper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            SHelper.Events.Display.MenuChanged += Display_MenuChanged;
        }

        public static void BetterCrafting_MenuPopulateContainers(IPopulateContainersEvent e)
        {
            IsBetterCraftingMenuOpen.Value = true;
            ThisPlayerStorage.Value.ReloadFromFarmerResources();
            e.Containers.Add(new Tuple<object, GameLocation>(ThisPlayerStorage.Value, null));
        }

        public static void BetterCrafting_PostCraft(IPostCraftEvent e)
        {
            ThisPlayerStorage.Value.SquareWithFarmerResources(e.Recipe);
        }

        public static void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs args)
        {
            ResourceStorageInventoryProvider.playerInventory.Value = new(Game1.player);
            ThisPlayerStorage.Value = ResourceStorageInventoryProvider.playerInventory.Value;
        }

        public static void Display_MenuChanged(object sender, MenuChangedEventArgs args)
        {
            if(args.NewMenu is not IBetterCraftingMenu && BetterCraftingAPI.GetActiveMenu() is null)
            {
                IsBetterCraftingMenuOpen.Value = false;
            }
        }

        public static void NotifyResourceChange(string itemId, int changeAmount, long playerUniqueId)
        {
            if (!(IsBetterCraftingLoaded && IsBetterCraftingMenuOpen.Value && playerUniqueId == ThisPlayerStorage.Value.OwnerId))
                return;

            ThisPlayerStorage.Value.NotifyOfChangeInResourceStorage(itemId, changeAmount);
        }
    }
}
