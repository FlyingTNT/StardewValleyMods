using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiStoryFarmhouse
{
    public class CodePatches
    {
        public static void FarmHouse_resetLocalState_Prefix(ref Vector2 __state)
        {
            __state = new Vector2(-1, -1);
            if (Game1.isWarping && Game1.player.previousLocationName == "MultipleFloors0")
            {
                __state = new Vector2(Game1.xLocationAfterWarp, Game1.yLocationAfterWarp);
            }
        }

        public static void FarmHouse_resetLocalState_Postfix(Vector2 __state)
        {
            if (__state.X >= 0)
            {
                Game1.player.Position = __state * 64f;
                Game1.xLocationAfterWarp = Game1.player.TilePoint.X;
                Game1.yLocationAfterWarp = Game1.player.TilePoint.Y;
            }
        }

        public static void SaveGame_loadDataToLocations_Prefix()
        {
            try
            {
                ModEntry.SMonitor.Log($"Checking save for multiple floors");

                List<string> possibleFloors = ModEntry.GetPossibleFloors();

                for (int i = 0; i < possibleFloors.Count; i++)
                {
                    string floorName = possibleFloors[i];
                    DecoratableLocation location = (DecoratableLocation)Game1.locations.FirstOrDefault(l => l.Name == $"MultipleFloors{i}");
                    if (location == null)
                    {
                        ModEntry.SMonitor.Log($"adding floor MultipleFloors{i}");
                        location = new DecoratableLocation($"Maps/MultipleFloorsMap{i}", $"MultipleFloors{i}");

                        Game1.locations.Add(location);
                    }
                    else
                        ModEntry.SMonitor.Log($"Game already has floor MultipleFloors{i}");
                }
            }
            catch(Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed in {nameof(SaveGame_loadDataToLocations_Prefix)} {ex}", StardewModdingAPI.LogLevel.Error);
            }
        }

        public static bool DecorableLocation_getFloors_Prefix(DecoratableLocation __instance, ref List<Rectangle> __result)
        {
            if (!__instance.Name.StartsWith("MultipleFloors"))
                return true;

            if(!ModEntry.TryGetFloor(__instance.Name, out Floor floor))
            {
                ModEntry.SMonitor.Log($"Could not get floor {__instance.Name} for flooring!", StardewModdingAPI.LogLevel.Debug);
                return true;
            }

            __result = floor.floors;

            return false;
        }

        public static bool GameLocation_getWalls_Prefix(DecoratableLocation __instance, ref List<Rectangle> __result)
        {
            if (!__instance.Name.StartsWith("MultipleFloors"))
                return true;

            if (!ModEntry.TryGetFloor(__instance.Name, out Floor floor))
            {
                ModEntry.SMonitor.Log($"Could not get floor {__instance.Name} for walls!", StardewModdingAPI.LogLevel.Debug);
                return true;
            }

            __result = floor.walls;

            return false;
        }
        
        public static bool GameLocation_CanPlaceThisFurnitureHere_Prefix(GameLocation __instance, ref bool __result, Furniture furniture)
        {
            if (!__instance.Name.StartsWith("MultipleFloors") || furniture is null)
                return true;

            __result = true;
            return false;
        }

        /// <summary>
        /// Makes the ambient lighting in the extra floors the same as the FarmHouse. This method is just copied from the FarmHouse class.
        /// </summary>
        public static bool GameLocation__updateAmbientLighting_Prefix(GameLocation __instance)
        {
            try
            {
                if (!__instance.Name.StartsWith("MultipleFloors"))
                {
                    return true;
                }

                float lightLevel = ModEntry.SHelper.Reflection.GetField<NetFloat>(__instance, "lightLevel").GetValue().Value;

                Color rainLightingColor;
                Color nightLightingColor;

                if (Utility.getHomeOfFarmer(Game1.player) is FarmHouse farmHouse)
                {
                    rainLightingColor = ModEntry.SHelper.Reflection.GetField<Color>(farmHouse, "rainLightingColor").GetValue();
                    nightLightingColor = ModEntry.SHelper.Reflection.GetField<Color>(farmHouse, "nightLightingColor").GetValue();
                }
                else
                {
                    // This is just how they're defined in the FarmHouse class. We only try to get them with reflection in case a mod changes them or something.
                    nightLightingColor = new Color(180, 180, 0);
                    rainLightingColor = new Color(90, 90, 0);
                }

                if (Game1.isStartingToGetDarkOut(__instance) || lightLevel > 0f)
                {
                    int time = Game1.timeOfDay + Game1.gameTimeInterval / (Game1.realMilliSecondsPerGameMinute + __instance.ExtraMillisecondsPerInGameMinute);
                    float lerp = 1f - Utility.Clamp((float)Utility.CalculateMinutesBetweenTimes(time, Game1.getTrulyDarkTime(__instance)) / 120f, 0f, 1f);
                    Game1.ambientLight = new Color((byte)Utility.Lerp(Game1.isRaining ? rainLightingColor.R : 0, nightLightingColor.R, lerp), (byte)Utility.Lerp(Game1.isRaining ? rainLightingColor.G : 0, nightLightingColor.G, lerp), (byte)Utility.Lerp(0f, nightLightingColor.B, lerp));
                }
                else
                {
                    Game1.ambientLight = (Game1.isRaining ? rainLightingColor : Color.White);
                }

                // Prevent the base method from running
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed in {nameof(GameLocation__updateAmbientLighting_Prefix)} {ex}", StardewModdingAPI.LogLevel.Error);
                return true;
            }
        }
    }
}