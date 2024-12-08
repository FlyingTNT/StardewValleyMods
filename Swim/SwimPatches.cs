﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using xTile.Dimensions;

namespace Swim
{
    internal class SwimPatches
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        internal static void GameLocation_StartEvent_Postfix()
        {
            try
            {
                if (Game1.player.swimming.Value)
                {
                    Game1.player.swimming.Value = false;
                    if (!Config.SwimSuitAlways)
                        Game1.player.changeOutOfSwimSuit();
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_StartEvent_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
        internal static void Event_exitEvent_Postfix(Event __instance)
        {
            try
            {
                SMonitor.Log($"exiting event");
                if (__instance.exitLocation != null && __instance.exitLocation.Location.waterTiles != null && __instance.exitLocation.Location.isTileOnMap(Game1.player.positionBeforeEvent) && __instance.exitLocation.Location.waterTiles[(int)(Game1.player.positionBeforeEvent.X),(int)(Game1.player.positionBeforeEvent.Y)])
                {
                    SMonitor.Log($"swimming again");

                    #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    ChangeAfterEvent();
                    #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_exitEvent_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        private static async Task ChangeAfterEvent()
        {
            await Task.Delay(1500);
            Game1.player.changeIntoSwimsuit();
            Game1.player.swimming.Value = true;
        }


        public static void Farmer_updateCommon_Prefix(Farmer __instance)
        {
            try
            {
                if (__instance.swimming.Value && (Config.SwimRestoresVitals || ModEntry.locationIsPool.Value) && __instance.timerSinceLastMovement > 0 && !Game1.eventUp && (Game1.activeClickableMenu == null || Game1.IsMultiplayer) && !Game1.paused)
                {
                    if (__instance.timerSinceLastMovement > 800)
                    {
                        __instance.currentEyes = 1;
                    }
                    else if (__instance.timerSinceLastMovement > 700)
                    {
                        __instance.currentEyes = 4;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_updateCommon_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void Farmer_updateCommon_Postfix(Farmer __instance)
        {
            try
            {
                if (__instance.swimming.Value && (Config.SwimRestoresVitals || ModEntry.locationIsPool.Value) && __instance.timerSinceLastMovement > 0 && !Game1.eventUp && (Game1.activeClickableMenu == null || Game1.IsMultiplayer) && !Game1.paused)
                {
                    if (__instance.swimTimer < 0)
                    {
                        __instance.swimTimer = 100;
                        if (__instance.stamina < __instance.MaxStamina)
                        {
                            __instance.stamina++;
                        }
                        if (__instance.health < __instance.maxHealth)
                        {
                            __instance.health++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_updateCommon_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Cuts the swim restoring health and stamina logic out of updateCommon (it is re-added it the above pre- and post- fixes).
        /// </summary>
        public static IEnumerable<CodeInstruction> Farmer_updateCommon_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            try
            {
                bool startLooking = false;
                int start = -1;
                int end = -1;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (startLooking)
                    {
                        if(start == -1 && codes[i].opcode == OpCodes.Ldfld && codes[i].operand as FieldInfo == typeof(Farmer).GetField("timerSinceLastMovement"))
                        {
                            start = i - 1;
                            SMonitor.Log($"start at {start}");
                        }
                        if (codes[i].opcode == OpCodes.Stfld && codes[i].operand as FieldInfo == typeof(Farmer).GetField("health"))
                        {
                            end = i + 1;
                            SMonitor.Log($"end at {end}");
                        }
                    }
                    else if (codes[i].operand as string == "slosh")
                    {
                        startLooking = true;
                    }
                }
                if(start > -1 && end > start)
                {
                    codes.RemoveRange(start, end - start);
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_updateCommon_Transpiler)}:\n{ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        public static void Farmer_changeIntoSwimsuit_Postfix(Farmer __instance)
        {
            try
            {
                if(Config.AllowActionsWhileInSwimsuit)
                    __instance.canOnlyWalk = false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_changeIntoSwimsuit_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
        
        public static void Farmer_setRunning_Prefix(Farmer __instance, ref bool __state)
        {
            try
            {
                __state = __instance.bathingClothes.Value;

                if (__instance.bathingClothes.Value && Config.AllowRunningWhileInSwimsuit)
                {
                    __instance.bathingClothes.Value = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_setRunning_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void Farmer_setRunning_Postfix(Farmer __instance, bool __state)
        {
            try
            {
                __instance.bathingClothes.Value = __state;

                if (__instance.swimming.Value)
                {
                    __instance.speed = __instance.running ? Config.SwimRunSpeed : Config.SwimSpeed;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_setRunning_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static bool Toolbar_draw_Prefix()
        {
            try
            {
                if (Game1.player.currentLocation?.Name == "AbigailCave")
                    return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Toolbar_draw_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }


        public static void Wand_DoFunction_Prefix(Farmer who, ref bool __state)
        {
            if (who.bathingClothes.Value)
            {
                who.bathingClothes.Value = false;
                __state = true;
            }
        }
        public static void Wand_DoFunction_Postfix(Farmer who, bool __state)
        {
            if(__state)
            {
                who.bathingClothes.Value = true;
            }
        }

        public static void Utility_playerCanPlaceItemHere_Prefix(Farmer f, ref bool __state)
        {
            try
            {
                if (Config.AllowActionsWhileInSwimsuit)
                {
                    f.bathingClothes.Value = false;
                    __state = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Utility_playerCanPlaceItemHere_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void Utility_playerCanPlaceItemHere_Postfix(Farmer f, bool __state)
        {
            try
            {
                if (__state)
                {
                    f.bathingClothes.Value = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Utility_playerCanPlaceItemHere_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void GameLocation_resetForPlayerEntry_Prefix(GameLocation __instance)
        {
            try
            {
                if(__instance.Name == "Custom_ScubaCrystalCave")
                {
                    if (Game1.player.mailReceived.Contains("SwimMod_Mariner_Completed"))
                    {
                        __instance.mapPath.Value = "Maps\\CrystalCaveDark";
                    }
                    else
                    {
                        __instance.mapPath.Value = "Maps\\CrystalCave";
                        ModEntry.oldMariner = new NPC(new AnimatedSprite("Characters\\Mariner", 0, 16, 32), new Vector2(10f, 7f) * 64f, 2, "Old Mariner", null);
                    }
                    //__instance.updateMap();
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_resetForPlayerEntry_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void GameLocation_draw_Prefix(GameLocation __instance, SpriteBatch b)
        {
            try
            {
                if(__instance.Name == "Custom_ScubaCrystalCave")
                {
                    if (!Game1.player.mailReceived.Contains("SwimMod_Mariner_Completed"))
                    {
                        ModEntry.oldMariner.draw(b);
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_draw_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static bool GameLocation_isCollidingPosition_Prefix(GameLocation __instance, Microsoft.Xna.Framework.Rectangle position, ref bool __result)
        {
            try
            {
                if(__instance.Name == "Custom_ScubaCrystalCave")
                {
                    if (!Game1.player.mailReceived.Contains("SwimMod_Mariner_Completed") && ModEntry.oldMariner != null && position.Intersects(ModEntry.oldMariner.GetBoundingBox()))
                    {
                        __result = true;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_isCollidingPosition_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }
        public static void GameLocation_UpdateWhenCurrentLocation_Postfix(GameLocation __instance, GameTime time)
        {
            try
            {
                if (__instance.Name == "Custom_ScubaCrystalCave")
                {
                    if (!Game1.player.mailReceived.Contains("SwimMod_Mariner_Completed"))
                    {
                        ModEntry.oldMariner?.update(time, __instance);
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_UpdateWhenCurrentLocation_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void GameLocation_checkAction_Prefix(GameLocation __instance, Location tileLocation, xTile.Dimensions.Rectangle viewport, Farmer who)
        {
            try
            {
                if (__instance.Name == "Custom_ScubaCrystalCave")
                {
                    if (!who.mailReceived.Contains("SwimMod_Mariner_Completed"))
                    {
                        if (ModEntry.oldMariner != null && ModEntry.oldMariner.Tile.X == tileLocation.X && ModEntry.oldMariner.Tile.Y == tileLocation.Y)
                        {
                            string playerTerm = Game1.content.LoadString("Strings\\Locations:Beach_Mariner_Player_" + (who.IsMale ? "Male" : "Female"));

                            if (ModEntry.marinerQuestionsWrongToday.Value)
                            {
                                SwimDialog.TryGetTranslation("SwimMod_Mariner_Wrong_Today", out string preface);
                                Game1.drawObjectDialogue(string.Format(preface, playerTerm));
                            }
                            else
                            {
                                Response[] answers = new Response[]
                                {
                                new Response("SwimMod_Mariner_Questions_Yes", Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_Yes")),
                                new Response("SwimMod_Mariner_Questions_No", Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_No"))
                                };
                                SwimDialog.TryGetTranslation(Game1.player.mailReceived.Contains("SwimMod_Mariner_Already") ? "SwimMod_Mariner_Questions_Old" : "SwimMod_Mariner_Questions", out string preface);
                                __instance.createQuestionDialogue(Game1.parseText(string.Format(preface, playerTerm)), answers, "SwimMod_Mariner_Questions");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void GameLocation_isCollidingPosition_Postfix(GameLocation __instance, ref bool __result, Microsoft.Xna.Framework.Rectangle position, bool isFarmer, Character character)
        {
            try
            {
                if (__result == false || !isFarmer || character?.Equals(Game1.player) != true || !Game1.player.swimming.Value || ModEntry.isUnderwater.Value || ModEntry.locationIsPool.Value)
                    return;

                int count = 0;

                if(__instance.doesTileHaveProperty(position.Left / 64, position.Top / 64, "Water", "Back") != null)
                {
                    count++;
                }
                if(__instance.doesTileHaveProperty(position.Left / 64, position.Bottom / 64, "Water", "Back") != null)
                {
                    count++;
                }
                if (__instance.doesTileHaveProperty(position.Right / 64, position.Top / 64, "Water", "Back") != null)
                {
                    count++;
                }
                if (__instance.doesTileHaveProperty(position.Right / 64, position.Bottom / 64, "Water", "Back") != null)
                {
                    count++;
                }

                // If most corners are water tiles, ignore collision
                if(count >= 3)
                {
                    __result = false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_isCollidingPosition_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
        public static void GameLocation_sinkDebris_Postfix(GameLocation __instance, bool __result, Debris debris, Vector2 chunkTile, Vector2 chunkPosition)
        {
            try
            {
                if (__result == false || !Game1.IsMasterGame || !SwimUtils.DebrisIsAnItem(debris))
                    return;

                if(debris.item != null)
                    SMonitor.Log($"Sinking debris: {debris.itemId.Value} ({debris.item.Name})");

                if (ModEntry.diveMaps.ContainsKey(__instance.Name) && ModEntry.diveMaps[__instance.Name].DiveLocations.Count > 0)
                {
                    Point pos = new Point((int)chunkTile.X, (int)chunkTile.Y);
                    Location loc = new Location(pos.X, pos.Y);

                    DiveMap dm = ModEntry.diveMaps[__instance.Name];
                    DiveLocation diveLocation = null;
                    foreach (DiveLocation dl in dm.DiveLocations)
                    {
                        if (dl.GetRectangle().X == -1 || dl.GetRectangle().Contains(loc))
                        {
                            diveLocation = dl;
                            break;
                        }
                    }

                    if (diveLocation == null)
                    {
                        SMonitor.Log($"sink debris: No dive destination for this point on this map");
                        return;
                    }

                    if (Game1.getLocationFromName(diveLocation.OtherMapName) == null)
                    {
                        SMonitor.Log($"sink debris: Can't find destination map named {diveLocation.OtherMapName}", LogLevel.Warn);
                        return;
                    }

                    
                    foreach(Chunk chunk in debris.Chunks)
                    {

                        if(chunk.position.Value == chunkPosition)
                        {
                            SMonitor.Log($"sink debris: creating copy of debris {debris.debrisType} item {debris.item != null} on {diveLocation.OtherMapName}");

                            if (debris.debrisType.Value != Debris.DebrisType.ARCHAEOLOGY && debris.debrisType.Value != Debris.DebrisType.OBJECT && chunk.randomOffset % 2 != 0)
                            {
                                SMonitor.Log($"sink debris: non-item debris");
                                break;
                            }

                            Debris newDebris;
                            Vector2 newTile = diveLocation.OtherMapPos == null ? chunkTile : new Vector2(diveLocation.OtherMapPos.X, diveLocation.OtherMapPos.Y);
                            Vector2 newPos = new Vector2(newTile.X * Game1.tileSize, newTile.Y * Game1.tileSize);
                            if (debris.item != null)
                            {
                                newDebris = Game1.createItemDebris(debris.item, newPos, Game1.random.Next(4), Game1.getLocationFromName(diveLocation.OtherMapName));
                            }
                            else
                            {
                                Game1.createItemDebris(ItemRegistry.Create(debris.itemId.Value, 1, debris.itemQuality, false), newPos, Game1.random.Next(4), Game1.getLocationFromName(diveLocation.OtherMapName));
                            }
                            break; 
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(GameLocation_sinkDebris_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}