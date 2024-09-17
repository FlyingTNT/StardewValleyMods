using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Network;
using System.Collections.Generic;

namespace ResourceStorage.BetterCrafting
{
    internal class ResourceStorageInventoryProvider : IInventoryProvider
    {
        public static readonly PerScreen<ResourceStorageInventory> playerInventory = new PerScreen<ResourceStorageInventory>(() => null);

        public bool CanExtractItems(object obj, GameLocation location, Farmer who)
        {
            return obj is ResourceStorageInventory inventory && inventory.OwnerId == who.UniqueMultiplayerID && ModEntry.Config.AutoUse;
        }

        public bool CanInsertItems(object obj, GameLocation location, Farmer who)
        {
            return obj is ResourceStorageInventory inventory && inventory.OwnerId == who.UniqueMultiplayerID;
        }

        public void CleanInventory(object obj, GameLocation location, Farmer who)
        {
            if(obj is ResourceStorageInventory inventory)
            {
                inventory.Clean();
            }
        }

        public int GetActualCapacity(object obj, GameLocation location, Farmer who)
        {
            return int.MaxValue;
        }

        public IInventory GetInventory(object obj, GameLocation location, Farmer who)
        {
             return obj is ResourceStorageInventory inventory && inventory.OwnerId == who.UniqueMultiplayerID ? inventory : null;
        }

        public IList<Item> GetItems(object obj, GameLocation location, Farmer who)
        {
            return obj is ResourceStorageInventory inventory && inventory.OwnerId == who.UniqueMultiplayerID ? inventory : null;
        }

        public Rectangle? GetMultiTileRegion(object obj, GameLocation location, Farmer who)
        {
            return null;
        }

        public bool IsMutexRequired(object obj, GameLocation location, Farmer who)
        {
            return obj is not ResourceStorageInventory; // Mutex is not required for ResourceStorageInventories
        }

        public NetMutex GetMutex(object obj, GameLocation location, Farmer who)
        {
            return null;
        }

        public Vector2? GetTilePosition(object obj, GameLocation location, Farmer who)
        {
            return null;
        }

        public bool IsValid(object obj, GameLocation location, Farmer who)
        {
            return obj is ResourceStorageInventory inventory && inventory.OwnerId == who.UniqueMultiplayerID;
        }
    }
}
