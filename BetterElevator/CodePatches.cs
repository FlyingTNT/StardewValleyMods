using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using xTile.Dimensions;
using xTile.Tiles;

namespace BetterElevator
{
    public partial class ModEntry
    {
        public static bool GameLocation_performAction_Prefix(GameLocation __instance, string[] action, Farmer who, ref bool __result)
        {
            if (!Config.ModEnabled || action is null || !who.IsLocalPlayer || !SHelper.Input.IsDown(Config.ModKey))
                return true;
            
            if (!Config.Unrestricted && MineShaft.lowestLevelReached < (who.currentLocation.Name == "SkullCave" ? 121 : 1))
            {
                return true;
            }

            string text = action[0];
            if (text == "SkullDoor")
            {
                if (!who.hasSkullKey || !who.hasUnlockedSkullDoor)
                    return true;
            }
            else if (text == "Mine" && action.Length > 1 && action[1] == "77377")
            {
                return true;
            }
            else if (text != "Mine")
            {
                return true;
            }
            Game1.activeClickableMenu = new BetterElevatorMenu();
            __result = true;
            return false;
        }

        public static bool MineShaft_checkAction_Prefix(MineShaft __instance, Location tileLocation, Farmer who, ref bool __result)
        {
            if (!Config.ModEnabled || !who.IsLocalPlayer)
                return true;

            int tileIndex = __instance.map.GetTileIndexAt(tileLocation, "Buildings");
            if (tileIndex == 115) // Up ladder
            {
                if (!SHelper.Input.IsDown(Config.ModKey))
                    return true;
                if (__instance.mineLevel == 77377)
                    return true;
                Game1.activeClickableMenu = new BetterElevatorMenu();
                __result = true;
                return false;
            }
            if (tileIndex == 173) // Down ladder
            {
                if (__instance.mineLevel == 77376) // 77377 is the level for the quarry mine shaft.
                {
                    Game1.enterMine(__instance.mineLevel + 2);
                    __instance.playSound("stairsdown");
                    __result = true;
                    return false;
                }
                if (__instance.mineLevel == int.MaxValue)
                {
                    Game1.enterMine(__instance.mineLevel);
                    __instance.playSound("stairsdown");
                    __result = true;
                    return false;
                }
            }
            return true;
        }
        public static void MineShaft_shouldCreateLadderOnThisLevel_Postfix(MineShaft __instance, ref bool __result)
        {
            if (!Config.ModEnabled)
                return;
            if (__instance.mineLevel == int.MaxValue)
                __result = false;
        }
    }
}