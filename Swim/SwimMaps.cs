using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile;
using xTile.Dimensions;
using xTile.Layers;
using xTile.Tiles;
using Object = StardewValley.Object;

namespace Swim
{
    public class SwimMaps
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        public static Object SpawnForageItem(GameLocation location, Vector2 position, string itemID)
        {
            Object item = ItemRegistry.Create<Object>(ItemRegistry.ManuallyQualifyItemId(itemID, "(O)"));
            SMonitor.Log($"Spawning forage {item.Name} at ({position.X}, {position.Y})");
            location.numberOfSpawnedObjectsOnMap++;
            location.objects[position] = item;
            item.IsSpawnedObject = true;
            item.CanBeGrabbed = true;
            return item;
        }

        public static Object SpawnWorldItem(GameLocation location, Vector2 position, string itemID)
        {
            Object item = ItemRegistry.Create<Object>(ItemRegistry.ManuallyQualifyItemId(itemID, "(O)"));
            SMonitor.Log($"Spawning world item {item.Name} at ({position.X}, {position.Y})");
            location.objects[position] = item;
            return item;
        }

        public static void AddScubaChest(GameLocation gameLocation, Vector2 pos, string which)
        {
            if (which == "ScubaTank" && !Game1.player.mailReceived.Contains(which))
            {
                gameLocation.overlayObjects[pos] = new Chest(new List<Item>() { new Clothing(ModEntry.scubaTankID) }, pos, false, 0);
            }
            else if (which == "ScubaMask" && !Game1.player.mailReceived.Contains(which))
            {
                gameLocation.overlayObjects[pos] = new Chest(new List<Item>() { new Hat(ModEntry.scubaMaskID) }, pos, false, 0);
            }
            else if (which == "ScubaFins" && !Game1.player.mailReceived.Contains(which))
            {
                gameLocation.overlayObjects[pos] = new Chest(new List<Item>() { new Boots(ModEntry.scubaFinsID) }, pos, false, 0);
            }
        }
        public static void AddWaterTiles(GameLocation gameLocation)
        {
            gameLocation.waterTiles = new WaterTiles(new bool[gameLocation.map.Layers[0].LayerWidth, gameLocation.map.Layers[0].LayerHeight]);
            bool foundAnyWater = false;
            for (int x = 0; x < gameLocation.map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < gameLocation.map.Layers[0].LayerHeight; y++)
                {
                    string waterProperty = gameLocation.doesTileHaveProperty(x, y, "Water", "Back");

                    if (waterProperty != null)
                    {
                        foundAnyWater = true;
                        if (waterProperty == "I")
                        {
                            gameLocation.waterTiles.waterTiles[x, y] = new WaterTiles.WaterTileData(is_water: true, is_visible: false);
                        }
                        else
                        {
                            gameLocation.waterTiles[x, y] = true;
                        }
                    }
                }
            }
            if (!foundAnyWater)
            {
                SMonitor.Log($"{Game1.player.currentLocation.Name} has no water tiles");
                gameLocation.waterTiles = null;
            }
            else
            {
                SMonitor.Log($"Gave {Game1.player.currentLocation.Name} water tiles");
            }
        }

        public static void AddMinerals(GameLocation l)
        {
            List<Vector2> spots = GetValidSpawnSpots(l);
            int mineralNo = (int)Math.Round(Game1.random.Next(Config.MineralPerThousandMin, Config.MineralPerThousandMax) / 1000f * spots.Count);

            // FlyingTNT.Swim/Minerals is a list of ((id, hp), weight) pairs
            ((string, int), int)[] mineralData = SHelper.GameContent.Load<List<((string, int), int)>>("FlyingTNT.Swim/Minerals").ToArray();

            ((string, int), float)[] weightedMinerals = GenerateWeitghedThreshholds(mineralData);

            foreach (Vector2 v in GetRandom(spots, mineralNo))
            {
                (string id, int hp) = GetRandom(weightedMinerals);
               
                if(hp is -1)
                {
                    SpawnForageItem(l, v, id);
                }
                else
                {
                    SpawnWorldItem(l, v, id).MinutesUntilReady = hp;
                }
            }
        }

        public static void AddCrabs(GameLocation l)
        {
            if (!Config.AddCrabs)
            {
                return;
            }

            List<Vector2> spots = GetValidSpawnSpots(l);
            ShuffleLast(spots, spots.Count);
            int crabs = (int)(Game1.random.Next(Config.CrabsPerThousandMin, Config.CrabsPerThousandMax) / 1000f * spots.Count);
            for (int i = 0; i < crabs; i++)
            {
                int idx = Game1.random.Next(spots.Count);
                l.characters.Add(new SeaCrab(new Vector2(spots[idx].X * Game1.tileSize, spots[idx].Y * Game1.tileSize)));
            }
        }

        public static void AddFishies(GameLocation l, bool smol = true)
        {
            if (!Config.AddFishies)
            {
                return;
            }

            List<Vector2> spots = GetValidSpawnSpots(l);
            if (spots.Count == 0)
            {
                SMonitor.Log($"No spots for fishies in map {l.Name}", LogLevel.Warn);
                return;
            }
            ShuffleLast(spots, spots.Count);
            if (smol)
            {
                int fishes = Game1.random.Next(Config.MinSmolFishies, Config.MaxSmolFishies);
                for (int i = 0; i < fishes; i++)
                {
                    int idx = Game1.random.Next(spots.Count);
                    l.characters.Add(new Fishie(new Vector2(spots[idx].X * Game1.tileSize, spots[idx].Y * Game1.tileSize)));
                }
            }
            else
            {
                int bigFishes = (int)(Game1.random.Next(Config.BigFishiesPerThousandMin, Config.BigFishiesPerThousandMax) / 1000f * spots.Count);
                for (int i = 0; i < bigFishes; i++)
                {
                    int idx = Game1.random.Next(spots.Count);
                    l.characters.Add(new BigFishie(new Vector2(spots[idx].X * Game1.tileSize, spots[idx].Y * Game1.tileSize)));
                }
            }
        }

        private static List<Vector2> GetValidSpawnSpots(GameLocation l)
        {
            // I tested this on the underwater beach map, and on average it took 0.068ms to manually count all the valid spots, so while this could be made faster by 
            // estimating the number of spots, imo it's not worth the complexity.
            List<Vector2> spots = new();
            for (int x = 0; x < l.map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < l.map.Layers[0].LayerHeight; y++)
                {
                    if (CanForageBePhysicallyPlacedHere(l, x, y))
                    {
                        spots.Add(new Vector2(x, y));
                    }
                }
            }
            return spots;
        }

        /// <summary>
        /// Shuffles the last count elements in the given list. It will pull elements from the whole list, but only the last elements are guaranteed to be shuffled.
        /// </summary>
        /// <param name="list">The list to shuffle.</param>
        /// <param name="count">The number of elements to shuffle.</param>
        /// <returns>The given list instance.</returns>
        private static List<T> ShuffleLast<T>(List<T> list, int count)
        {
            count = count > list.Count ? list.Count : count;
            int n = list.Count;
            while (n > list.Count - count)
            {
                n--;
                int k = Game1.random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            return list;
        }

        /// <summary>
        /// Randomly selects a number of elements from the given list. Won't repeat elements.
        /// 
        /// Will modify the list.
        /// </summary>
        /// <param name="list">The list to get elements from.</param>
        /// <param name="count">The number of elements to get.</param>
        /// <returns>An IEnumerable containing count random elements from the list. </returns>
        private static IEnumerable<T> GetRandom<T>(List<T> list, int count)
        {
            return ShuffleLast(list, count).TakeLast(count > list.Count ? list.Count : count);
        }

        private static (T, float)[] GenerateWeitghedThreshholds<T>((T, int)[] weightedPairs)
        {
            float total = weightedPairs.Select(pair => pair.Item2).Sum();
            (T, float)[] output = new (T, float)[weightedPairs.Length];

            float accumulated = 0;

            for(int i = 0; i < weightedPairs.Length; i++)
            {
                accumulated += weightedPairs[i].Item2 / total;
                output[i] = (weightedPairs[i].Item1, accumulated);
            }

            output[^1].Item2 = 1; // In case any precision errors would cause it to be less than 1

            return output;
        }

        private static T GetRandom<T>((T, float)[] itemsWithThreshholds)
        {
            float random = Game1.random.NextSingle();
            for(int i = 0; i < itemsWithThreshholds.Length; i++)
            {
                if(random <= itemsWithThreshholds[i].Item2)
                {
                    return itemsWithThreshholds[i].Item1;
                }
            }

            // Should not be possible to reach here if weightedThreshholds is set up right, but I want to be safe.
            return itemsWithThreshholds[^1].Item1;
        }

        /// <summary>
        /// Spawns ocean-themed forage in the given location.
        /// </summary>
        public static void AddOceanForage(GameLocation l)
        {
            List<Vector2> spots = GetValidSpawnSpots(l);
            int forageNo = (int)(Game1.random.Next(Config.OceanForagePerThousandMin, Config.OceanForagePerThousandMax) / 1000f * spots.Count);

            (string, float)[] weightedForage = GenerateWeitghedThreshholds(SHelper.GameContent.Load<List<(string, int)>>("FlyingTNT.Swim/OceanForage").ToArray());

            foreach (Vector2 v in GetRandom(spots, forageNo))
            {
                SpawnForageItem(l, v, GetRandom(weightedForage));
            }
        }

        /// <summary>
        /// Spawns forage based on the location's forage data.
        /// </summary>
        /// <remarks>
        /// This is useful because the base game's forage spawn method (<see cref="GameLocation.spawnObjects"/>) considers a tile an invalid spawn location if it is a water tile, so it will never be spawned
        /// in the underwater maps because all of their tiles have the Water property. Imo, this is not a big enough issue to justify the patches that would be necessary to fix it. 
        /// </remarks>
        public static void AddForage(GameLocation l)
        {
            // Much of this was taken from GameLocation.spawnObjects()
            Random r = Utility.CreateDaySaveRandom();
            LocationData data = l.GetData();
            if (data == null || l.numberOfSpawnedObjectsOnMap >= data.MaxSpawnedForageAtOnce)
            {
                return;
            }    
            List<SpawnForageData> possibleForage = new();
            foreach (SpawnForageData spawn in data.Forage)
            {
                if ((spawn.Condition == null || GameStateQuery.CheckConditions(spawn.Condition, l, null, null, null, r)) && (!spawn.Season.HasValue || spawn.Season == l.GetSeason()))
                {
                    possibleForage.Add(spawn);
                }
            }
            if(!possibleForage.Any())
            {
                return;
            }

            int numberToSpawn = r.Next(data.MinDailyForageSpawn, data.MaxDailyForageSpawn + 1);
            numberToSpawn = Math.Min(numberToSpawn, data.MaxSpawnedForageAtOnce - l.numberOfSpawnedObjectsOnMap);
            ItemQueryContext itemQueryContext = new(l, null, r, "get forage for Swim map");
            foreach(Vector2 spot in GetRandom(GetValidSpawnSpots(l), numberToSpawn))
            {
                SpawnForageData forage = r.ChooseFrom(possibleForage);
                if (!r.NextBool(forage.Chance))
                {
                    continue;
                }
                Item forageItem = ItemQueryResolver.TryResolveRandomItem(forage, itemQueryContext);
                if (forageItem == null)
                {
                    continue;
                }
                SpawnForageItem(l, spot, forageItem.QualifiedItemId);
            }
        }

        /// <summary>
        /// Spawns artifact spots in the location.
        /// </summary>
        /// <remarks>
        /// This is useful because the base game's artifact spawn method (<see cref="GameLocation.spawnObjects"/>) considers a tile an invalid spawn location if it is a water tile, so it will never be spawned
        /// in the underwater maps because all of their tiles have the Water property. Imo, this is not a big enough issue to justify the patches that would be necessary to fix it. 
        /// </remarks>
        public static void AddArtifactSpots(GameLocation l)
        {
            // Much of this was taken from GameLocation.spawnObjects()

            Random r = Utility.CreateDaySaveRandom(7); // The random needs to have a different seed than the one used in AddForage or they'd generate the same points

            List<Vector2> positionOfArtifactSpots = new();
            foreach ((Vector2 k, Object v) in l.objects.Pairs)
            {
                if (v.QualifiedItemId == "(O)590")
                {
                    positionOfArtifactSpots.Add(k);
                }
            }
            for (int i = positionOfArtifactSpots.Count - 1; i >= 0; i--)
            {
                if (r.NextBool(0.15))
                {
                    l.objects.Remove(positionOfArtifactSpots[i]);
                    positionOfArtifactSpots.RemoveAt(i);
                }
            }
            if (positionOfArtifactSpots.Count > 4)
            {
                return;
            }
            double chanceForNewArtifactAttempt = 1.0;
            while (r.NextDouble() < chanceForNewArtifactAttempt)
            {
                int x = r.Next(l.map.DisplayWidth / 64);
                int y = r.Next(l.map.DisplayHeight / 64);
                Vector2 location = new(x, y);
                if (CanForageBePhysicallyPlacedHere(l, x, y))
                {
                    l.objects.Add(location, ItemRegistry.Create<Object>("(O)590"));
                }
                chanceForNewArtifactAttempt *= 0.75;
            }
        }

        /// <summary>
        /// Checks whether a forage item would be intersecting another object or the map if placed here.
        /// </summary>
        /// <remarks>
        /// It might be good to check for other things like the Spawnable property in addition to calling this method.
        /// </remarks>
        private static bool CanForageBePhysicallyPlacedHere(GameLocation l, int x, int y)
        {
            if(l.getTileIndexAt(x, y, "Back") == -1)
            {
                return false;
            }

            if(l.getTileIndexAt(x, y, "Buildings") != -1 ||
               l.getTileIndexAt(x, y, "Front") != -1 ||
               l.getTileIndexAt(x, y, "AlwaysFront") != -1 ||
               l.getTileIndexAt(x, y, "AlwaysFront1") != -1 ||
               l.getTileIndexAt(x, y, "AlwaysFront2") != -1 ||
               l.getTileIndexAt(x, y, "AlwaysFront3") != -1)
            {
                return false;
            }

            Vector2 vector = new(x, y);

            if (l.objects.ContainsKey(vector) || l.overlayObjects.ContainsKey(vector))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Spawns a treasure chest containing ocean-themed treasure.
        /// </summary>
        /// <param name="l"></param>
        public static void AddOceanTreasure(GameLocation l)
        {
            List<Vector2> spots = GetValidSpawnSpots(l);
            int treasureNo = (int)(Game1.random.Next(Config.MinOceanChests, Config.MaxOceanChests));

            foreach (Vector2 v in GetRandom(spots, treasureNo))
            {
                List<Item> treasures = new List<Item>();
                float chance = 1f;
                while (Game1.random.NextDouble() <= (double)chance)
                {
                    chance *= 0.4f;
                    if (Game1.random.NextDouble() < 0.5)
                    {
                        treasures.Add(new Object("774", 2 + ((Game1.random.NextDouble() < 0.25) ? 2 : 0), false, -1, 0));
                    }
                    switch (Game1.random.Next(4))
                    {
                        case 0:
                            if (Game1.random.NextDouble() < 0.03)
                            {
                                treasures.Add(new Object("386", Game1.random.Next(1, 3), false, -1, 0));
                            }
                            else
                            {
                                List<string> possibles = new List<string>();
                                possibles.Add("384");
                                if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                                {
                                    possibles.Add("380");
                                }
                                if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                                {
                                    possibles.Add("378");
                                }
                                if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                                {
                                    possibles.Add("388");
                                }
                                if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                                {
                                    possibles.Add("390");
                                }
                                possibles.Add("382");
                                treasures.Add(new Object(possibles.ElementAt(Game1.random.Next(possibles.Count)), Game1.random.Next(2, 7) * ((Game1.random.NextDouble() < 0.05 + (double)Game1.player.luckLevel.Value * 0.015) ? 2 : 1), false, -1, 0));
                                if (Game1.random.NextDouble() < 0.05 + (double)Game1.player.LuckLevel * 0.03)
                                {
                                    treasures.Last().Stack *= 2;
                                }
                            }
                            break;
                        case 1:
                            if (Game1.random.NextDouble() < 0.1)
                            {
                                treasures.Add(new Object("687", 1, false, -1, 0));
                            }
                            else if (Game1.random.NextDouble() < 0.25 && Game1.player.craftingRecipes.ContainsKey("Wild Bait"))
                            {
                                treasures.Add(new Object("774", 5 + ((Game1.random.NextDouble() < 0.25) ? 5 : 0), false, -1, 0));
                            }
                            else
                            {
                                treasures.Add(new Object("685", 10, false, -1, 0));
                            }
                            break;
                        case 2:
                            if (Game1.random.NextDouble() < 0.1 && Game1.netWorldState.Value.LostBooksFound < 21 && Game1.player.hasOrWillReceiveMail("lostBookFound"))
                            {
                                treasures.Add(new Object("102", 1, false, -1, 0));
                            }
                            else if (Game1.player.archaeologyFound.Count() > 0)
                            {
                                if (Game1.random.NextDouble() < 0.125)
                                {
                                    treasures.Add(new Object("585", 1, false, -1, 0));
                                }
                                else if (Game1.random.NextDouble() < 0.25)
                                {
                                    treasures.Add(new Object("588", 1, false, -1, 0));
                                }
                                else if (Game1.random.NextDouble() < 0.5)
                                {
                                    treasures.Add(new Object("103", 1, false, -1, 0));
                                }
                                if (Game1.random.NextDouble() < 0.5)
                                {
                                    treasures.Add(new Object("120", 1, false, -1, 0));
                                }

                                else
                                {
                                    treasures.Add(new Object("535", 1, false, -1, 0));
                                }
                            }
                            else
                            {
                                treasures.Add(new Object("382", Game1.random.Next(1, 3), false, -1, 0));
                            }
                            break;
                        case 3:
                            switch (Game1.random.Next(3))
                            {
                                case 0:
                                    switch (Game1.random.Next(3))
                                    {
                                        case 0:
                                            treasures.Add(new Object((537 + ((Game1.random.NextDouble() < 0.4) ? Game1.random.Next(-2, 0) : 0)).ToString(), Game1.random.Next(1, 4), false, -1, 0));
                                            break;
                                        case 1:
                                            treasures.Add(new Object((536 + ((Game1.random.NextDouble() < 0.4) ? -1 : 0)).ToString(), Game1.random.Next(1, 4), false, -1, 0));
                                            break;
                                        case 2:
                                            treasures.Add(new Object("535", Game1.random.Next(1, 4), false, -1, 0));
                                            break;
                                    }
                                    if (Game1.random.NextDouble() < 0.05 + (double)Game1.player.LuckLevel * 0.03)
                                    {
                                        treasures.Last().Stack *= 2;
                                    }
                                    break;
                                case 1:
                                    switch (Game1.random.Next(4))
                                    {
                                        case 0:
                                            treasures.Add(new Object("382", Game1.random.Next(1, 4), false, -1, 0));
                                            break;
                                        case 1:
                                            treasures.Add(new Object(((Game1.random.NextDouble() < 0.3) ? 82 : ((Game1.random.NextDouble() < 0.5) ? 64 : 60)).ToString(), Game1.random.Next(1, 3), false, -1, 0));
                                            break;
                                        case 2:
                                            treasures.Add(new Object(((Game1.random.NextDouble() < 0.3) ? 84 : ((Game1.random.NextDouble() < 0.5) ? 70 : 62)).ToString(), Game1.random.Next(1, 3), false, -1, 0));
                                            break;
                                        case 3:
                                            treasures.Add(new Object(((Game1.random.NextDouble() < 0.3) ? 86 : ((Game1.random.NextDouble() < 0.5) ? 66 : 68)).ToString(), Game1.random.Next(1, 3), false, -1, 0));
                                            break;
                                    }
                                    if (Game1.random.NextDouble() < 0.05)
                                    {
                                        treasures.Add(new Object("72", 1, false, -1, 0));
                                    }
                                    if (Game1.random.NextDouble() < 0.05)
                                    {
                                        treasures.Last().Stack *= 2;
                                    }
                                    break;
                                case 2:
                                    if (Game1.player.FishingLevel < 2)
                                    {
                                        treasures.Add(new Object("770", Game1.random.Next(1, 4), false, -1, 0));
                                    }
                                    else
                                    {
                                        float luckModifier = (1f + (float)Game1.player.DailyLuck);
                                        if (Game1.random.NextDouble() < 0.05 * (double)luckModifier && !Game1.player.specialItems.Contains("14"))
                                        {
                                            treasures.Add(new MeleeWeapon("14")
                                            {
                                                specialItem = true
                                            });
                                        }
                                        if (Game1.random.NextDouble() < 0.05 * (double)luckModifier && !Game1.player.specialItems.Contains("51"))
                                        {
                                            treasures.Add(new MeleeWeapon("51")
                                            {
                                                specialItem = true
                                            });
                                        }
                                        if (Game1.random.NextDouble() < 0.07 * (double)luckModifier)
                                        {
                                            switch (Game1.random.Next(3))
                                            {
                                                case 0:
                                                    treasures.Add(new Ring("516" + ((Game1.random.NextDouble() < (double)((float)Game1.player.LuckLevel / 11f)) ? 1 : 0)));
                                                    break;
                                                case 1:
                                                    treasures.Add(new Ring("518" + ((Game1.random.NextDouble() < (double)((float)Game1.player.LuckLevel / 11f)) ? 1 : 0)));
                                                    break;
                                                case 2:
                                                    treasures.Add(new Ring(""+Game1.random.Next(529, 535)));
                                                    break;
                                            }
                                        }
                                        if (Game1.random.NextDouble() < 0.02 * (double)luckModifier)
                                        {
                                            treasures.Add(new Object("166", 1, false, -1, 0));
                                        }
                                        if (Game1.random.NextDouble() < 0.001 * (double)luckModifier)
                                        {
                                            treasures.Add(new Object("74", 1, false, -1, 0));
                                        }
                                        if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                        {
                                            treasures.Add(new Object("127", 1, false, -1, 0));
                                        }
                                        if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                        {
                                            treasures.Add(new Object("126", 1, false, -1, 0));
                                        }
                                        if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                        {
                                            treasures.Add(new Ring("527"));
                                        }
                                        if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                        {
                                            treasures.Add(new Boots("" + Game1.random.Next(504, 514)));
                                        }
                                        if (treasures.Count == 1)
                                        {
                                            treasures.Add(new Object("72", 1, false, -1, 0));
                                        }
                                    }
                                    break;
                            }
                            break;
                    }
                }
                if (treasures.Count == 0)
                {
                    treasures.Add(new Object("685", Game1.random.Next(1, 4) * 5, false, -1, 0));
                }
                if (treasures.Count > 0)
                {
                    Color tint = Color.White;
                    l.overlayObjects[v] = new Chest( new List<Item>() { treasures[ModEntry.myRand.Value.Next(treasures.Count)] }, v, false, 0)
                    {
                        Tint = tint
                    };
                }
                foreach (var obj in treasures)
                {
                    SMonitor.Log($"Treasures: {obj.QualifiedItemId} {obj.DisplayName}");
                }
            }
        }

        public static void RemoveWaterTiles(GameLocation l)
        {
            if (l?.map is null)
            {
                return;
            }
            Map map = l.map;
            //Layer back = map.RequireLayer("Back");
            for (int x = 0; x < map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < map.Layers[0].LayerHeight; y++)
                {
                    if (l.doesTileHaveProperty(x, y, "Water", "Back") != null)
                    {
                        l.removeTileProperty(x, y, "Back", "Water");
                    }

                    // This function basically has no effect because it doesn't remove the tile index properties, but I'm going to leave it out out
                    // of fear that it would break something if it did.
                    //back.Tiles[x, y]?.TileIndexProperties.Remove("Water"); <- not in the method originally
                }
            }
        }

        public static void SwitchToWaterTiles(GameLocation location)
        {
            string mapName = location.Name;

            Map map = location.Map;
            for (int x = 0; x < map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < map.Layers[0].LayerHeight; y++)
                {
                    if (location.doesTileHaveProperty(x, y, "Water", "Back") != null)
                    {
                        Tile tile = map.GetLayer("Back").PickTile(new Location(x, y) * Game1.tileSize, Game1.viewport.Size);
                        if (tile != null)
                        {
                            if (tile.TileIndexProperties.ContainsKey("Passable"))
                            {
                                tile.TileIndexProperties["Passable"] = "T";
                            }
                        }
                        tile = map.GetLayer("Buildings").PickTile(new Location(x, y) * Game1.tileSize, Game1.viewport.Size);
                        if (tile != null)
                        {
                            if (tile.TileIndexProperties.ContainsKey("Passable"))
                            {
                                tile.TileIndexProperties["Passable"] = "T";
                            }
                            else
                            {
                                tile.TileIndexProperties.Add("Passable", "T");
                            }
                        }
                    }
                }
            }
        }
        public static void SwitchToLandTiles(GameLocation location)
        {
            string mapName = location.Name;

            Map map = location.Map;
            for (int x = 0; x < map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < map.Layers[0].LayerHeight; y++)
                {
                    if (location.doesTileHaveProperty(x, y, "Water", "Back") != null)
                    {
                        Tile tile = map.GetLayer("Back").PickTile(new Location(x, y) * Game1.tileSize, Game1.viewport.Size);
                        if (tile != null)
                        {
                            if (tile.TileIndexProperties.ContainsKey("Passable"))
                            {
                                tile.TileIndexProperties["Passable"] = "F";
                            }
                        }
                        tile = map.GetLayer("Buildings").PickTile(new Location(x, y) * Game1.tileSize, Game1.viewport.Size);
                        if (tile != null)
                        {
                            if (tile.TileIndexProperties.ContainsKey("Passable"))
                            {
                                tile.TileIndexProperties["Passable"] = "F";
                            }
                            else
                            {
                                tile.TileIndexProperties.Add("Passable", "F");
                            }
                        }
                    }
                }
            }
        }
    }
}
