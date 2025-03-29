using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
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
        public static void ReloadWaterTiles(GameLocation gameLocation)
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
                SMonitor.Log($"{gameLocation.Name} has no water tiles");
                gameLocation.waterTiles = null;
            }
            else
            {
                SMonitor.Log($"Gave {gameLocation.Name} water tiles");
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
            int treasureNo = (Game1.random.Next(Config.MinOceanChests, Config.MaxOceanChests));

            foreach (Vector2 v in GetRandom(spots, treasureNo))
            {
                List<Item> treasures = null;
                try
                {
                    treasures = GenerateChestTreasure();
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Filed in {nameof(GenerateChestTreasure)}: {ex}");
                }

                if(treasures is null)
                {
                    continue;
                }

                // Remove any invalid items that GenerateChestTreasure may have added
                treasures.RemoveAll(item => item is null || item.Name == Item.ErrorItemName);

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

        /// <summary>
        /// This is ripped from <see cref="FishingRod.openTreasureMenuEndFunction(int)"/> with minor tweaks.
        /// </summary>
        public static List<Item> GenerateChestTreasure()
        {
            int clearWaterDistance = Game1.random.Next(1, 7);
            Farmer who = Game1.player;
            float chance = 1;
            List<Item> treasures = new();
            while (Game1.random.NextDouble() <= (double)chance)
            {
                chance *= 0.4f;
                while (Utility.tryRollMysteryBox(0.08 + Game1.player.team.AverageDailyLuck() / 5.0))
                {
                    treasures.Add(ItemRegistry.Create((Game1.player.stats.Get(StatKeys.Mastery(2)) != 0) ? "(O)GoldenMysteryBox" : "(O)MysteryBox"));
                }
                if (Game1.player.stats.Get(StatKeys.Mastery(0)) != 0 && Game1.random.NextDouble() < 0.05)
                {
                    treasures.Add(ItemRegistry.Create("(O)GoldenAnimalCracker"));
                }
                if (Game1.random.NextDouble() < 0.5)
                {
                    switch (Game1.random.Next(13))
                    {
                        case 0:
                            treasures.Add(ItemRegistry.Create("(O)337", Game1.random.Next(1, 6)));
                            break;
                        case 1:
                            treasures.Add(ItemRegistry.Create("(O)SkillBook_" + Game1.random.Next(5)));
                            break;
                        case 2:
                            treasures.Add(Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, Game1.random, 8));
                            break;
                        case 3:
                            treasures.Add(ItemRegistry.Create("(O)213"));
                            break;
                        case 4:
                            treasures.Add(ItemRegistry.Create("(O)872", Game1.random.Next(3, 6)));
                            break;
                        case 5:
                            treasures.Add(ItemRegistry.Create("(O)687"));
                            break;
                        case 6:
                            treasures.Add(ItemRegistry.Create("(O)ChallengeBait", Game1.random.Next(3, 6)));
                            break;
                        case 7:
                            treasures.Add(ItemRegistry.Create("(O)703", Game1.random.Next(3, 6)));
                            break;
                        case 8:
                            treasures.Add(ItemRegistry.Create("(O)StardropTea"));
                            break;
                        case 9:
                            treasures.Add(ItemRegistry.Create("(O)797"));
                            break;
                        case 10:
                            treasures.Add(ItemRegistry.Create("(O)733"));
                            break;
                        case 11:
                            treasures.Add(ItemRegistry.Create("(O)728"));
                            break;
                        case 12:
                            treasures.Add(ItemRegistry.Create("(O)SonarBobber"));
                            break;
                    }
                    continue;
                }
                switch (Game1.random.Next(4))
                {
                    case 0:
                        {
                            if (clearWaterDistance >= 5 && Game1.random.NextDouble() < 0.03)
                            {
                                treasures.Add(new Object("386", Game1.random.Next(1, 3)));
                                break;
                            }
                            List<int> possibles = new List<int>();
                            if (clearWaterDistance >= 4)
                            {
                                possibles.Add(384);
                            }
                            if (clearWaterDistance >= 3 && (possibles.Count == 0 || Game1.random.NextDouble() < 0.6))
                            {
                                possibles.Add(380);
                            }
                            if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                            {
                                possibles.Add(378);
                            }
                            if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                            {
                                possibles.Add(388);
                            }
                            if (possibles.Count == 0 || Game1.random.NextDouble() < 0.6)
                            {
                                possibles.Add(390);
                            }
                            possibles.Add(382);
                            Item treasure = ItemRegistry.Create(Game1.random.ChooseFrom(possibles).ToString(), Game1.random.Next(2, 7) * ((!(Game1.random.NextDouble() < 0.05 + (double)who.luckLevel.Value * 0.015)) ? 1 : 2));
                            if (Game1.random.NextDouble() < 0.05 + (double)who.LuckLevel * 0.03)
                            {
                                treasure.Stack *= 2;
                            }
                            treasures.Add(treasure);
                            break;
                        }
                    case 1:
                        if (clearWaterDistance >= 4 && Game1.random.NextDouble() < 0.1 && who.FishingLevel >= 6)
                        {
                            treasures.Add(ItemRegistry.Create("(O)687"));
                        }
                        else if (Game1.random.NextDouble() < 0.25 && who.craftingRecipes.ContainsKey("Wild Bait"))
                        {
                            treasures.Add(ItemRegistry.Create("(O)774", 5 + ((Game1.random.NextDouble() < 0.25) ? 5 : 0)));
                        }
                        else if (Game1.random.NextDouble() < 0.11 && who.FishingLevel >= 6)
                        {
                            treasures.Add(ItemRegistry.Create("(O)SonarBobber"));
                        }
                        else if (who.FishingLevel >= 6)
                        {
                            treasures.Add(ItemRegistry.Create("(O)DeluxeBait", 5));
                        }
                        else
                        {
                            treasures.Add(ItemRegistry.Create("(O)685", 10));
                        }
                        break;
                    case 2:
                        if (Game1.random.NextDouble() < 0.1 && Game1.netWorldState.Value.LostBooksFound < 21 && who != null && who.hasOrWillReceiveMail("lostBookFound"))
                        {
                            treasures.Add(ItemRegistry.Create("(O)102"));
                        }
                        else if (who.archaeologyFound.Length > 0)
                        {
                            if (Game1.random.NextDouble() < 0.25 && who.FishingLevel > 1)
                            {
                                treasures.Add(ItemRegistry.Create("(O)" + Game1.random.Next(585, 588)));
                            }
                            else if (Game1.random.NextBool() && who.FishingLevel > 1)
                            {
                                treasures.Add(ItemRegistry.Create("(O)" + Game1.random.Next(103, 120)));
                            }
                            else
                            {
                                treasures.Add(ItemRegistry.Create("(O)535"));
                            }
                        }
                        else
                        {
                            treasures.Add(ItemRegistry.Create("(O)382", Game1.random.Next(1, 3)));
                        }
                        break;
                    case 3:
                        switch (Game1.random.Next(3))
                        {
                            case 0:
                                {
                                    Item treasure2 = ((clearWaterDistance >= 4) ? ItemRegistry.Create("(O)" + (537 + ((Game1.random.NextDouble() < 0.4) ? Game1.random.Next(-2, 0) : 0)), Game1.random.Next(1, 4)) : ((clearWaterDistance < 3) ? ItemRegistry.Create("(O)535", Game1.random.Next(1, 4)) : ItemRegistry.Create("(O)" + (536 + ((Game1.random.NextDouble() < 0.4) ? (-1) : 0)), Game1.random.Next(1, 4))));
                                    if (Game1.random.NextDouble() < 0.05 + (double)who.LuckLevel * 0.03)
                                    {
                                        treasure2.Stack *= 2;
                                    }
                                    treasures.Add(treasure2);
                                    break;
                                }
                            case 1:
                                {
                                    if (who.FishingLevel < 2)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)382", Game1.random.Next(1, 4)));
                                        break;
                                    }
                                    Item treasure3;
                                    if (clearWaterDistance >= 4)
                                    {
                                        treasures.Add(treasure3 = ItemRegistry.Create("(O)" + ((Game1.random.NextDouble() < 0.3) ? 82 : Game1.random.Choose(64, 60)), Game1.random.Next(1, 3)));
                                    }
                                    else if (clearWaterDistance >= 3)
                                    {
                                        treasures.Add(treasure3 = ItemRegistry.Create("(O)" + ((Game1.random.NextDouble() < 0.3) ? 84 : Game1.random.Choose(70, 62)), Game1.random.Next(1, 3)));
                                    }
                                    else
                                    {
                                        treasures.Add(treasure3 = ItemRegistry.Create("(O)" + ((Game1.random.NextDouble() < 0.3) ? 86 : Game1.random.Choose(66, 68)), Game1.random.Next(1, 3)));
                                    }
                                    if (Game1.random.NextDouble() < 0.028 * (double)((float)clearWaterDistance / 5f))
                                    {
                                        treasures.Add(treasure3 = ItemRegistry.Create("(O)72"));
                                    }
                                    if (Game1.random.NextDouble() < 0.05)
                                    {
                                        treasure3.Stack *= 2;
                                    }
                                    break;
                                }
                            case 2:
                                {
                                    if (who.FishingLevel < 2)
                                    {
                                        treasures.Add(new Object("770", Game1.random.Next(1, 4)));
                                        break;
                                    }
                                    float luckModifier = (1f + (float)who.DailyLuck) * ((float)clearWaterDistance / 5f);
                                    if (Game1.random.NextDouble() < 0.05 * (double)luckModifier && !who.specialItems.Contains("14"))
                                    {
                                        Item weapon2 = MeleeWeapon.attemptAddRandomInnateEnchantment(ItemRegistry.Create("(W)14"), Game1.random);
                                        weapon2.specialItem = true;
                                        treasures.Add(weapon2);
                                    }
                                    if (Game1.random.NextDouble() < 0.05 * (double)luckModifier && !who.specialItems.Contains("51"))
                                    {
                                        Item weapon = MeleeWeapon.attemptAddRandomInnateEnchantment(ItemRegistry.Create("(W)51"), Game1.random);
                                        weapon.specialItem = true;
                                        treasures.Add(weapon);
                                    }
                                    if (Game1.random.NextDouble() < 0.07 * (double)luckModifier)
                                    {
                                        switch (Game1.random.Next(3))
                                        {
                                            case 0:
                                                treasures.Add(new Ring((516 + ((Game1.random.NextDouble() < (double)((float)who.LuckLevel / 11f)) ? 1 : 0)).ToString()));
                                                break;
                                            case 1:
                                                treasures.Add(new Ring((518 + ((Game1.random.NextDouble() < (double)((float)who.LuckLevel / 11f)) ? 1 : 0)).ToString()));
                                                break;
                                            case 2:
                                                treasures.Add(new Ring(Game1.random.Next(529, 535).ToString()));
                                                break;
                                        }
                                    }
                                    if (Game1.random.NextDouble() < 0.02 * (double)luckModifier)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)166"));
                                    }
                                    if (who.FishingLevel > 5 && Game1.random.NextDouble() < 0.001 * (double)luckModifier)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)74"));
                                    }
                                    if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)127"));
                                    }
                                    if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)126"));
                                    }
                                    if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                    {
                                        treasures.Add(new Ring("527"));
                                    }
                                    if (Game1.random.NextDouble() < 0.01 * (double)luckModifier)
                                    {
                                        treasures.Add(ItemRegistry.Create("(B)" + Game1.random.Next(504, 514)));
                                    }
                                    if (treasures.Count == 1)
                                    {
                                        treasures.Add(ItemRegistry.Create("(O)72"));
                                    }
                                    if (Game1.player.stats.Get("FishingTreasures") > 3)
                                    {
                                        Random r = Utility.CreateRandom(Game1.player.stats.Get("FishingTreasures") * 27973, Game1.uniqueIDForThisGame);
                                        if (r.NextDouble() < 0.05 * (double)luckModifier)
                                        {
                                            treasures.Add(ItemRegistry.Create("(O)SkillBook_" + r.Next(5)));
                                            chance = 0f;
                                        }
                                    }
                                    break;
                                }
                        }
                        break;
                }
            }
            if (treasures.Count == 0)
            {
                treasures.Add(ItemRegistry.Create("(O)685", Game1.random.Next(1, 4) * 5));
            }

            return treasures;
        }

        /// <summary>
        /// Method to test <see cref="GenerateChestTreasure"/> to check what items it is producing and at what rates.
        /// </summary>
        public static void TestGenerateChestTreasure()
        {
            Dictionary<string, int> counts = new();
            int total = 0;
            for (int i = 0; i < 1000000; i++)
            {
                foreach (Item item in GenerateChestTreasure())
                {
                    if (counts.ContainsKey(item.QualifiedItemId))
                    {
                        counts[item.QualifiedItemId]++;
                    }
                    else
                    {
                        counts[item.QualifiedItemId] = 1;
                    }

                    total++;
                }
            }

            List<string> ids = counts.Keys.ToList();
            ids.Sort((a, b) => counts[b].CompareTo(counts[a]));
            foreach (string id in ids)
            {
                SMonitor.Log($"{ItemRegistry.Create(id).DisplayName} ({id}): {counts[id]} -> {counts[id] / (float)total}%");
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
