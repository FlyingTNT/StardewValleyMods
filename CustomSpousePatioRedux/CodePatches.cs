using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using xTile.Layers;
using xTile.Tiles;

namespace CustomSpousePatioRedux
{
    public partial class ModEntry
    {
        public static bool Farm_CacheOffBasePatioArea_Prefix(Farm __instance)
        {
            if (!Config.EnableMod)
                return true;

            try {
                baseSpouseAreaTiles.Clear();
                CacheOffBasePatioArea("default", __instance, __instance.GetSpouseOutdoorAreaCorner());

                if (OutdoorAreas.Count == 0)
                    return false;

                foreach(var data in OutdoorAreas)
                {
                    if(!TryCacheOffBasePatioArea(data.Key))
                    {
                        SMonitor.Log($"Failed to cache patio for {data.Key} in {data.Value.location}");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farm_CacheOffBasePatioArea_Prefix)}:\n{ex}", LogLevel.Error);
                return true; // run original logic
            }
            
        }

        public static bool Farm_ReapplyBasePatioArea_Prefix()
        {
            if (!Config.EnableMod || OutdoorAreas.Count == 0)
                return true;
            if (addingExtraAreas)
                return false;

            foreach (var kvp in baseSpouseAreaTiles)
            {
                ReapplyBasePatioArea(kvp.Key);
            }
            return false;
        }
        public static IEnumerable<CodeInstruction> Farm_addSpouseOutdoorArea_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log($"Transpiling Farm.addSpouseOutdoorArea");

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (i > 0 && codes[i - 1].opcode == OpCodes.Ldarg_0 && codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(Farm), nameof(Farm.GetSpouseOutdoorAreaCorner)))
                {
                    SMonitor.Log("Overriding Farm.GetSpouseOutdoorAreaCorner");
                    codes[i - 1] = new CodeInstruction(OpCodes.Ldarg_1);
                    codes[i] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(ModEntry.GetSpouseOutdoorAreaCorner)));
                }
                else if (i < codes.Count - 15 && codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo && (MethodInfo)codes[i].operand == AccessTools.Method(typeof(GameLocation), nameof(GameLocation.ApplyMapOverride), new System.Type[] {typeof(string), typeof(string), typeof(Rectangle?), typeof(Rectangle?) }))
                {
                    SMonitor.Log("Overriding GameLocation.ApplyMapOverride");
                    codes[i - 15].opcode = OpCodes.Ldarg_1; // Replace the Farm instance with the name of the spouse
                    codes[i].operand = AccessTools.Method(typeof(ModEntry), nameof(ModEntry.ApplyMapOverride)); // All args are same except it isn't an instance method and the first arg is the spouse name
                }
            }

            return codes.AsEnumerable();
        }

        private static bool addingExtraAreas = false;

        public static void Farm_addSpouseOutdoorArea_Postfix(Farm __instance, string spouseName)
        {
            try
            {
                if (!Config.EnableMod || OutdoorAreas.Count == 0 || spouseName == "" || spouseName == null)
                    return;
                spousePositions[spouseName] = __instance.spousePatioSpot;
                if (addingExtraAreas)
                    return;
                addingExtraAreas = true;
                foreach (var name in OutdoorAreas.Keys)
                {
                    if (name != spouseName)
                        __instance.addSpouseOutdoorArea(name);
                }
                addingExtraAreas = false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farm_addSpouseOutdoorArea_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        
        public static bool NPC_setUpForOutdoorPatioActivity_Prefix(NPC __instance)
        {
            if (!Config.EnableMod || OutdoorAreas.Count == 0 || !OutdoorAreas.ContainsKey(__instance.Name))
            {
                if(Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Sat") && Game1.MasterPlayer.spouse != __instance.Name)
                {
                    SMonitor.Log($"preventing {__instance.Name} from going to spouse patio");
                    return false;
                }
                return true;
            }

            try
            {
                Vector2 patio_location = __instance.GetSpousePatioPosition();
                if (NPC.checkTileOccupancyForSpouse(Game1.getLocationFromName(OutdoorAreas[__instance.Name].location), patio_location, ""))
                {
                    return false;
                }
                Game1.warpCharacter(__instance, OutdoorAreas[__instance.Name].location, patio_location);
                __instance.popOffAnyNonEssentialItems();
                __instance.currentMarriageDialogue.Clear();
                __instance.addMarriageDialogue("MarriageDialogue", "patio_" + __instance.Name, false);
                __instance.followSchedule = false; // Not in default method
                __instance.setTilePosition((int)patio_location.X, (int)patio_location.Y);
                __instance.shouldPlaySpousePatioAnimation.Value = true;

                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_setUpForOutdoorPatioActivity_Prefix)}:\n{ex}", LogLevel.Error);
                return true; // run original logic
            }
            
        }
        public static bool NPC_GetSpousePatioPosition_Prefix(NPC __instance, ref Vector2 __result)
        {
            if(!Config.EnableMod)
            {
                return true;
            }

            try
            {
                if (OutdoorAreas.Count == 0 || !spousePositions.ContainsKey(__instance.Name))
                {
                    return true;
                }
                    
                __result = spousePositions[__instance.Name].ToVector2();
                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_GetSpousePatioPosition_Prefix)}:\n{ex}", LogLevel.Error);
                return true; // run original logic
            }
        }

        /// <summary>
        /// This ensures that if a map asset is invalidated, the patios will be re-added when it is reloaded.
        /// This method is called in the map asset invalidaion pipeline (specifically, when propagating core assets, and all maps seem to be core assets).
        /// </summary>
        private static void GameLocation_MakeMapModifications_Postifx(GameLocation __instance, HashSet<string> ____appliedMapOverrides)
        {
            // The farm already reloads the patios automatically
            if (!Config.EnableMod || __instance is Farm) 
            {
                return;
            }

            List<string> patiosToReapply = new();

            foreach(var kvp in OutdoorAreas)
            {
                // If the instance is not the patio's location, or the instance already has this spouse's patio applied, continue
                if (kvp.Value.location != __instance.Name || ____appliedMapOverrides.Contains($"{kvp.Key}_spouse_patio"))
                    continue;

                if(!TryCacheOffBasePatioArea(kvp.Key))
                {
                    SMonitor.Log($"Failed to cache tiles for {kvp.Key} in MakeMapModifications!");
                    continue;
                }

                patiosToReapply.Add(kvp.Key);
            }

            // We cache all of the tiles and then reapply just in case there is some overlap or something.
            foreach(string spouse in patiosToReapply)
            {
                SMonitor.Log($"Reapplying map overrides for {spouse}");
                PlaceSpousePatio(spouse);
            }
        }
    }
}