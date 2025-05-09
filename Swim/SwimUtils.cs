﻿using Common.Integrations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using Swim.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xTile;
using xTile.Dimensions;
using xTile.Layers;
using xTile.Tiles;

namespace Swim
{
    public static class SwimUtils
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;

        public static Dictionary<string, string> seaMonsterSounds = new Dictionary<string, string>() {
            {"A","dialogueCharacter"},
            {"B","grunt"},
            {"C","throwDownITem"},
            {"D","stoneStep"},
            {"E","thudStep"},
            {"F","toolSwap"},
            {"G","bob"},
            {"H","dwoop"},
            {"I","ow"},
            {"J","breathin"},
            {"K","boop"},
            {"L","flute"},
            {"M","backpackIN"},
            {"N","croak"},
            {"O","flybuzzing"},
            {"P","skeletonStep"},
            {"Q","dustMeep"},
            {"R","throw"},
            {"S","shadowHit"},
            {"T","slingshot"},
            {"U","dwop"},
            {"V","fishingRodBend"},
            {"W","Cowboy_Footstep"},
            {"X","junimoMeep1"},
            {"Y","fallDown"},
            {"Z","harvest"},
        };

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        #region Warping
        public static Point GetEdgeWarpDestination(int idxPos, EdgeWarp edge)
        {
            try
            {
                int idx = 1 + idxPos - edge.FirstTile;
                int length = 1 + edge.LastTile - edge.FirstTile;
                int otherLength = 1 + edge.OtherMapLastTile - edge.OtherMapFirstTile;
                int otherIdx = (int)Math.Round((idx / (float)length) * otherLength);
                int tileIdx = edge.OtherMapFirstTile - 1 + otherIdx;
                if (edge.DestinationHorizontal == true)
                {
                    SMonitor.Log($"idx {idx} length {length} otherIdx {otherIdx} tileIdx {tileIdx} warp point: {tileIdx},{edge.OtherMapIndex}");
                    return new Point(tileIdx, edge.OtherMapIndex);
                }
                else
                {
                    SMonitor.Log($"warp point: {edge.OtherMapIndex},{tileIdx}");
                    return new Point(edge.OtherMapIndex, tileIdx);
                }
            }
            catch
            {

            }
            return Point.Zero;
        }
        #endregion

        #region Diving
        public static void DiveTo(DiveLocation diveLocation)
        {
            DivePosition dp = diveLocation.OtherMapPos;
            if (dp is null)
            {
                SMonitor.Log($"Diving to existing tile position");
                Point pos = Game1.player.TilePoint;
                dp = new DivePosition()
                {
                    X = pos.X,
                    Y = pos.Y
                };
            }

            if (Game1.getLocationFromName(diveLocation.OtherMapName) is not GameLocation location || !IsValidDiveLocation(location, new Vector2(dp.X, dp.Y)))
            {
                SMonitor.Log($"Invalid dive location: {diveLocation.OtherMapName} ({dp.X}, {dp.Y})");
                return;
            }

            if (!IsMapUnderwater(Game1.player.currentLocation.Name))
            {
                SwimHelperEvents.bubbles.Value.Clear();
            }
            else
            {
                Game1.changeMusicTrack("none", false, StardewValley.GameData.MusicContext.Default);
            }

            Game1.playSound("pullItemFromWater");
            Game1.warpFarmer(diveLocation.OtherMapName, dp.X, dp.Y, false);
        }

        public static bool IsValidDiveLocation(GameLocation map, Vector2 location)
        {
            return map.isTileOnMap(location) && (!map.IsTileBlockedBy(location, CollisionMask.Buildings, CollisionMask.Buildings) || IsWaterTile(location, map)) && map.getTileIndexAt((int)location.X, (int)location.Y, "Back") != -1;
        }

        public static bool IsMapUnderwater(string name)
        {
            return ModEntry.diveMaps.ContainsKey(name) && ModEntry.diveMaps[name].Features.Contains("Underwater");
        }
        #endregion

        #region Oxygen
        public static int MaxOxygen()
        {
            return Game1.player.MaxStamina * Math.Max(1, Config.OxygenMult);
        }

        public static void UpdateOxygenValue()
        {
            if (Game1.activeClickableMenu is not null || !Context.IsPlayerFree || Game1.player.freezePause > 0)
            {
                return;
            }

            if (ModEntry.isUnderwater.Value)
            {
                if (ModEntry.Oxygen >= 0)
                {
                    if (!IsWearingScubaGear())
                    {
                        ModEntry.Oxygen--;
                    }
                    else
                    {
                        RegenerateOxygen();
                    }
                }
                if (ModEntry.Oxygen < 0 && !ModEntry.surfacing.Value)
                {
                    ModEntry.surfacing.Value = true;
                    Game1.playSound("pullItemFromWater");
                    DiveLocation diveLocation = ModEntry.diveMaps[Game1.player.currentLocation.Name].DiveLocations.Last();
                    DiveTo(diveLocation);
                }
            }
            else
            {
                ModEntry.surfacing.Value = false;
                RegenerateOxygen();
            }
        }

        private static void RegenerateOxygen()
        {
            if (ModEntry.Oxygen < MaxOxygen())
            {
                ModEntry.Oxygen++;

                if (ModEntry.Oxygen < MaxOxygen())
                {
                    ModEntry.Oxygen++;
                }
            }
        }

        /// <summary>
        /// Draws the oxygen bar in the bottom right, by the HP and stamina bars. 
        /// 
        /// Most of this is taken from Game1.drawHUD()
        /// </summary>
        public static void DrawOxygenBar()
        {
            // Note that oxygen is a function of stamina, so some references to stamina are used where it is easier
            int oxygen = ModEntry.Oxygen;
            int maxOxygen = MaxOxygen();
            const float staminaModifier = 0.625f;
            float modifier = staminaModifier / Config.OxygenMult;
            Vector2 topOfBar = new(Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea().Right - 48 - 64, Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea().Bottom - 224 - 16 - (int)((Game1.player.MaxStamina - 270) * staminaModifier));
            topOfBar = OffsetOxygenBarPosition(topOfBar);
            if (Game1.isOutdoorMapSmallerThanViewport())
            {
                topOfBar.X = Math.Min(topOfBar.X, -Game1.viewport.X + Game1.currentLocation.map.Layers[0].LayerWidth * 64 - 48);
            }
            Game1.spriteBatch.Draw(ModEntry.OxygenBarTexture, topOfBar, new(0, 0, 12, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            Game1.spriteBatch.Draw(ModEntry.OxygenBarTexture, new Microsoft.Xna.Framework.Rectangle((int)topOfBar.X, (int)(topOfBar.Y + 64f), 48, Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea().Bottom - 64 - 16 - (int)(topOfBar.Y + 64f - 8f)), new(0, 16, 12, 16), Color.White);
            Game1.spriteBatch.Draw(ModEntry.OxygenBarTexture, new Vector2(topOfBar.X, topOfBar.Y + 224f + ((Game1.player.MaxStamina - 270) * staminaModifier) - 64f), new(0, 40, 12, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);
            Microsoft.Xna.Framework.Rectangle r = new((int)topOfBar.X + 12, (int)topOfBar.Y + 16 + 32 + (int)(maxOxygen * modifier) - (int)(Math.Max(0f, oxygen) * modifier), 24, (int)(oxygen * modifier) - 1);
            Color c = GetBlueToGrayLerpColor(oxygen / (float)maxOxygen);
            Game1.spriteBatch.Draw(Game1.staminaRect, r, c);
            r.Height = 4;
            c.R = (byte)Math.Max(0, c.R - 50);
            c.G = (byte)Math.Max(0, c.G - 50);
            Game1.spriteBatch.Draw(Game1.staminaRect, r, c);

            float mouseXDiff = Game1.getOldMouseX() - topOfBar.X;
            float mouseYDiff = Game1.getOldMouseY() - topOfBar.Y;
            if (mouseXDiff >= 0 && mouseYDiff >= 0 && mouseXDiff < 48 && mouseYDiff < 224f + ((Game1.player.MaxStamina - 270) * staminaModifier))
            {
                Game1.drawWithBorder((int)Math.Max(0f, oxygen) + "/" + maxOxygen, Color.Black * 0f, Color.White, topOfBar + new Vector2(0f - Game1.dialogueFont.MeasureString("999/999").X - 16f - (float)(Game1.showingHealth ? 64 : 0), 64f));
            }
        }

        /// <summary>
        /// Offsets the oxygen bar position for mod compatibility. 
        /// </summary>
        private static Vector2 OffsetOxygenBarPosition(Vector2 topLeftCorner)
        {
            topLeftCorner.X += Config.OxygenBarXOffset;
            if (Config.OxygenBarYOffset < 0) // Putting it below where it is messes up the math and I don't care to make it work
            {
                topLeftCorner.Y += Config.OxygenBarYOffset;
            }

            if (Game1.showingHealthBar || Game1.showingHealth)
            {
                topLeftCorner.X -= 56;
            }

            if (SHelper.ModRegistry.IsLoaded(IDs.Survivalistic))
            {
                // Survivalistic adds two bars, so we move the oxygen bar left two bars' width
                topLeftCorner.X -= 55 + 60;
            }

            return topLeftCorner;
        }

        private static Color GetBlueToGrayLerpColor(float power)
        {
            return new Color(50, 50, (int)((power >= 0.5f) ? 255 : 50 + (power * (2 * (255 - 50)) + 50)));
        }
        #endregion

        #region Jumping
        public static bool IsSafeToTryJump()
        {
            // Null checks
            if (Game1.player?.currentLocation?.waterTiles == null)
            {
                return false;
            }

            // Player state checks
            if (!Context.IsPlayerFree || !Context.CanPlayerMove || Game1.player.isRidingHorse())
            {
                return false;
            }

            // Modded player state checks
            if (Game1.player.millisecondsPlayed - SwimHelperEvents.lastJump.Value < 250 || IsMapUnderwater(Game1.player.currentLocation.Name))
            {
                return false;
            }

            // Player input checks
            if (!((Game1.player.isMoving() && Config.ReadyToSwim) || (Config.ManualJumpButton.IsDown() && Config.EnableClickToSwim)) || Config.PreventJumpButton.IsDown())
            {
                return false;
            }

            // Don't let them jump into water in the night market (still let them leave the water tho)
            if(Game1.player.currentLocation is BeachNightMarket && !Game1.player.swimming.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets whether the player can jump to the given position.
        /// </summary>
        public static bool IsValidJumpLocation(Vector2 position, GameLocation location = null)
        {
            location ??= Game1.player.currentLocation;

            if (!location.isTileOnMap(position))
            {
                return false;
            }

            bool jumpToLand = Game1.player.swimming.Value;
            bool isWater = IsWaterTile(position);
            if (jumpToLand == isWater)
            {
                return false;
            }

            if (jumpToLand)
            {
                return IsTilePassable(location, position);
            }
            else
            {
                Tile tile = location.map?.GetLayer("Buildings")?.PickTile(new Location((int)position.X * Game1.tileSize, (int)position.Y * Game1.tileSize), Game1.viewport.Size);
                return tile is null || tile.TileIndex == 76 || (!location.isTilePassable(position) && AreAdjascentTilesWater(position, location));
            }
        }
        #endregion

        /// <summary>
        /// Checks whether all the tiles adjascent to the given position are water tiles (excluding diagonals).
        /// </summary>
        public static bool AreAdjascentTilesWater(Vector2 position, GameLocation location)
        {
            foreach(Vector2 directionVector in Utility.DirectionsTileVectors)
            {
                if (!IsWaterTile(position + directionVector, location))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasAnyBuildingsAtPosition(GameLocation location, int x, int y)
        {
            if(!location.isTileOnMap(x, y))
            {
                return false;
            }

            Map map = location.map;
            Layer layer = map.GetLayer("Buildings");
            if(layer is null)
            {
                return false;
            }

            if(layer.Tiles[x, y] is not null)
            {
                return true;
            }

            int depth = 1;
            bool wasLayer = true;

            while (wasLayer)
            {
                wasLayer = false;

                layer = map.GetLayer($"Buildings{depth}");
                if(layer is not null)
                {
                    wasLayer = true;

                    if (layer.Tiles[x, y] is not null)
                    {
                        return true;
                    }
                }

                layer = map.GetLayer($"Buildings-{depth}");
                if (layer is not null)
                {
                    wasLayer = true;

                    if (layer.Tiles[x, y] is not null)
                    {
                        return true;
                    }
                }

                depth++;
            }

            return false;
        }

        #region Unused
        public static int CheckForBuriedItem(Farmer who)
        {
            int objectIndex = 330; // Clay
            if (Game1.random.NextDouble() < 0.1)
            {
                if (Game1.random.NextDouble() < 0.75)
                {
                    switch (Game1.random.Next(5))
                    {
                        case 0:
                            objectIndex = 96; // Dwarf Scroll I
                            break;
                        case 1:
                            objectIndex = (who.hasOrWillReceiveMail("lostBookFound") ? ((Game1.netWorldState.Value.LostBooksFound < 21) ? 102 : 770) : 770); // Lost book / mixed seeds
                            break;
                        case 2:
                            objectIndex = 110; // Rusty Spoon
                            break;
                        case 3:
                            objectIndex = 112; // Rusty Cog
                            break;
                        case 4:
                            objectIndex = 585; // Skeletal Tail
                            break;
                    }
                }
                else if (Game1.random.NextDouble() < 0.75)
                {
                    var r = Game1.random.NextDouble();

                    if (r < 0.75)
                    {
                        objectIndex = ((Game1.random.NextDouble() < 0.5) ? 121 : 97); // Dwarf hem / Dwarf scroll II
                    }
                    else if (r < 0.80)
                    {
                        objectIndex = 99; // Dwarf scroll IV
                    }
                    else
                    {
                        objectIndex = ((Game1.random.NextDouble() < 0.5) ? 122 : 336); // Dwarf Gadget / Gold Bar
                    }
                }
                else
                {
                    objectIndex = ((Game1.random.NextDouble() < 0.5) ? 126 : 127); // Strange Doll / Strange Doll
                }
            }
            else
            {
                if (Game1.random.NextDouble() < 0.5)
                {
                    objectIndex = 330; // Clay
                }
                else
                {
                    if (Game1.random.NextDouble() < 0.25)
                    {
                        objectIndex = 749; // Omni Geode
                    }
                    else if (Game1.random.NextDouble() < 0.5)
                    {
                        var r = Game1.random.NextDouble();
                        if (r < 0.7)
                        {
                            objectIndex = 535; // Geode
                        }
                        else if (r < 0.85)
                        {
                            objectIndex = 537; // Magma Geode
                        }
                        else
                        {
                            objectIndex = 536; // Frozen Geode
                        }
                    }
                }
            }
            return objectIndex;
        }
        public static async void SeaMonsterSay(string speech)
        {
            foreach (char c in speech)
            {
                string s = c.ToString().ToUpper();
                if (seaMonsterSounds.ContainsKey(s))
                {
                    Game1.playSound("junimoMeep1", (seaMonsterSounds.Keys.ToList().IndexOf(s) / 26) * 2 - 1);
                }
                await Task.Delay(100);
            }
        }
        #endregion

        #region Player State
        public static bool IsWearingScubaGear()
        {
            bool tank = Game1.player.shirtItem.Value?.ItemId == ModEntry.scubaTankID;
            bool mask = Game1.player.hat.Value?.ItemId == ModEntry.scubaMaskID;

            return tank && mask;
        }

        public static bool IsInWater()
        {
            WaterTiles tiles = Game1.player.currentLocation.waterTiles;
            Point p = Game1.player.TilePoint;

            // If they're not swimming, passable buildings (bridges) should not be in water, but if they are, they should.
            if (!Game1.player.swimming.Value && (Game1.player.currentLocation.doesTileHaveProperty(p.X, p.Y, "Passable", "Buildings") != null))
            {
                return false;
            }

            bool output = IsMapUnderwater(Game1.player.currentLocation.Name)
                ||
                (
                    tiles != null
                    &&
                    (
                        (p.X >= 0 && p.Y >= 0 && tiles.waterTiles.GetLength(0) > p.X && tiles.waterTiles.GetLength(1) > p.Y && tiles[p.X, p.Y])
                        ||
                        (
                            Game1.player.swimming.Value
                            &&
                            (p.X < 0 || p.Y < 0 || tiles.waterTiles.GetLength(0) <= p.X || tiles.waterTiles.GetLength(1) <= p.Y)
                        )
                    )
                );

            return output;
        }
        #endregion

        #region Location State
        public static bool IsAllowedSwimLocation(GameLocation location)
        {
            return location is not (VolcanoDungeon or BoatTunnel or BathHousePool or Caldera) && !(location is MineShaft mineshaft && mineshaft.mineLevel == 100);
        }

        public static bool CanSwimHere()
        {
            GameLocation location = Game1.player.currentLocation;
            if (!IsAllowedSwimLocation(location) || ModEntry.LocationProhibitsSwimming)
            {
                return false;
            }

            Point playerPosition = Game1.player.TilePoint;

            string property = location.doesTileHaveProperty(playerPosition.X, playerPosition.Y, "TouchAction", "Back");

            if (property == "PoolEntrance" || property == "ChangeIntoSwimsuit")
            {
                SMonitor.Log("The current tile is a pool entrance! Disabling swimming in this location.");
                ModEntry.locationIsPool.Value = true;
                return false;
            }

            return true;
        }

        public static bool IsWaterTile(Vector2 tilePos)
        {
            return IsWaterTile(tilePos, Game1.player.currentLocation);
        }

        public static bool IsWaterTile(Vector2 tilePos, GameLocation location)
        {
            if (location != null && location.waterTiles != null && tilePos.X >= 0 && tilePos.Y >= 0 && location.waterTiles.waterTiles.GetLength(0) > tilePos.X && location.waterTiles.waterTiles.GetLength(1) > tilePos.Y)
            {
                return location.waterTiles[(int)tilePos.X, (int)tilePos.Y];
            }
            return false;
        }

        public static bool IsWaterTile(Point tilePos)
        {
            return IsWaterTile(tilePos, Game1.player.currentLocation);
        }

        public static bool IsWaterTile(Point tilePos, GameLocation location)
        {
            if (location != null && location.waterTiles != null && tilePos.X >= 0 && tilePos.Y >= 0 && location.waterTiles.waterTiles.GetLength(0) > tilePos.X && location.waterTiles.waterTiles.GetLength(1) > tilePos.Y)
            {
                return location.waterTiles[tilePos.X, tilePos.Y];
            }
            return false;
        }

        public static bool IsTilePassable(GameLocation location, Vector2 tileLocation)
        {
            return location.isTilePassable(tileLocation) && !location.IsTileOccupiedBy(tileLocation, CollisionMask.TerrainFeatures | CollisionMask.Objects, CollisionMask.All);
        }
        #endregion

        #region Mod-Specific Utilities
        /// <summary>
        /// Draws a bar across the top of the screen (the old oxygen bar)
        /// </summary>
        public static void DrawProgressBar(SpriteBatch b, int current, int max)
        {
            Texture2D texture = new Texture2D(Game1.graphics.GraphicsDevice, (int)Math.Round(Game1.viewport.Width * 0.74f), 30);
            Color[] data = new Color[texture.Width * texture.Height];
            texture.GetData(data);
            for (int i = 0; i < data.Length; i++)
            {
                if (i <= texture.Width || i % texture.Width == texture.Width - 1)
                {
                    data[i] = new Color(0.5f, 1f, 0.5f);
                }
                else if (data.Length - i < texture.Width || i % texture.Width == 0)
                {
                    data[i] = new Color(0, 0.5f, 0);
                }
                else if ((i % texture.Width) / (float)texture.Width < (float)current / (float)max)
                {
                    data[i] = Color.GhostWhite;
                }
                else
                {
                    data[i] = Color.Black;
                }
            }
            texture.SetData(data);
            b.Draw(texture, new Vector2((int)Math.Round(Game1.viewport.Width * 0.13f), 100), Color.White);
        }

        public static void ReadDiveMapData(DiveMapData data)
        {
            foreach (DiveMap map in data.Maps)
            {
                if (!ModEntry.diveMaps.ContainsKey(map.Name))
                {
                    ModEntry.diveMaps.Add(map.Name, map);
                    SMonitor.Log($"added dive map info for {map.Name}", LogLevel.Debug);
                }
                else
                {
                    SMonitor.Log($"dive map info already exists for {map.Name}", LogLevel.Trace);
                }
            }
        }

        /// <summary>
        /// Gets a key -> translated string dictionary of all the strings in out i18n
        /// </summary>
        /// <remarks>
        /// We use this to allow our i18n strings to be patchable by other mods.
        /// </remarks>
        public static Dictionary<string, string> Geti18nDict()
        {
            IEnumerable<Translation> translations = SHelper.Translation.GetTranslations();

            Dictionary<string, string> i18nDict = new Dictionary<string, string>();

            foreach (Translation translation in translations)
            {
                i18nDict.Add(translation.Key, translation);
            }

            return i18nDict;
        }

        public static string GetTranslation(string key)
        {
            return SwimDialog.TryGetTranslation(key, out string translation) ? translation : key;
        }

        public static SoundEffect LoadBreatheSound()
        {
            string filePath = Path.Combine(SHelper.DirectoryPath, "assets", "breathe.wav");
            if (File.Exists(filePath))
            {
                return SoundEffect.FromStream(new FileStream(filePath, FileMode.Open));
            }

            return null;
        }

        public static void VerifySwimForageAsset(string assetName)
        {
            #if DEBUG
            SMonitor.Log(assetName);
            #endif
            List<SwimForageData> data = SHelper.GameContent.Load<List<SwimForageData>>(assetName);
            foreach(SwimForageData entry in data)
            {
                ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(entry.ItemId);
                if (itemData.IsErrorItem)
                {
                    SMonitor.Log($"Error item with id {entry.ItemId} found in {assetName}", LogLevel.Debug);
                }
                else
                {
                    #if DEBUG
                    SMonitor.Log(itemData.DisplayName);
                    #endif
                }
            }
        }
#endregion

        #region Misc
        public static bool IsMouseButtonDown(KeybindList keybindList)
        {
            if(keybindList.GetKeybindCurrentlyDown() is not Keybind keybind)
            {
                return false;
            }

            // I'm not the biggest fan of this but I don't know how I would do any better.
            return keybind.Buttons.Any(button => button is SButton.MouseLeft or SButton.MouseRight or SButton.MouseMiddle or SButton.MouseX1 or SButton.MouseX2);
        }
        public static bool DebrisIsAnItem(Debris debris)
        {
            return debris.debrisType.Value == Debris.DebrisType.OBJECT || debris.debrisType.Value == Debris.DebrisType.ARCHAEOLOGY || debris.debrisType.Value == Debris.DebrisType.RESOURCE || debris.item != null;
        }

        /// <summary>
        /// Gets the direction of one point relative to another.
        /// 
        /// Point 1 is the starting point, and point 2 the endpoint.
        /// Returns a cardinal direction using Stardew Valley's direction system (0 is up, 1 right, 2 down, and 3 left)
        /// </summary>
        /// <param name="x1">The x coordinate of the first point.</param>
        /// <param name="y1">The y coordinate of the first point.</param>
        /// <param name="x2">The x coordinate of the second point.</param>
        /// <param name="y2">The y coordinate of the second point.</param>
        /// <returns>A cardinal direction using Stardew Valley's direction system (0 is up, 1 right, 2 down, and 3 left)</returns>
        public static int GetDirection(float x1, float y1, float x2, float y2)
        {
            if (Math.Abs(x1 - x2) > Math.Abs(y1 - y2))
            {
                if (x2 - x1 > 0)
                {
                    return Game1.right;
                }
                else
                {
                    return Game1.left;
                }
            }
            else
            {
                if (y2 - y1 > 0)
                {
                    return Game1.down;
                }
                else
                {
                    return Game1.up;
                }
            }
        }
        #endregion
    }
}