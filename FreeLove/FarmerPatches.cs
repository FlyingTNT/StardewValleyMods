using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FreeLove
{
    public static class FarmerPatches
    {
        private static IMonitor SMonitor;
        private static IModHelper SHelper;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        /// <summary>
        /// Sets TempOfficialSpouse to the spouse to be divorced
        /// </summary>
        public static bool Farmer_doDivorce_Prefix(Farmer __instance)
        {
            try
            {
                SMonitor.Log("Trying to divorce");

                if (ModEntry.SpouseToDivorce is null)
                {
                    SMonitor.Log("Tried to divorce but no spouse to divorce!");
                    return false;
                }

                NPC divorcee = Game1.getCharacterFromName(ModEntry.SpouseToDivorce);

                if(divorcee is null)
                {
                    SMonitor.Log($"Could not get NPC {ModEntry.SpouseToDivorce} for divorce!");
                    return false;
                }

                // We set this so that this will be the spouse being divorced
                ModEntry.TempOfficialSpouse = divorcee;

                return true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_doDivorce_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        /// <summary>
        /// Applies Config.DivorceHeartsLost, updates the current spouse dicts, and resets TempOfficialSpouse.
        /// </summary>
        public static void Farmer_doDivorce_Postfix(Farmer __instance)
        {
            try
            {
                ModEntry.TempOfficialSpouse = null;

                string name = ModEntry.SpouseToDivorce;

                int points = 2000;
                if (ModEntry.DivorceHeartsLost < 0)
                {
                    points = 0;
                }
                else
                {
                    points -= ModEntry.DivorceHeartsLost * 250;
                }

                if (__instance.friendshipData.ContainsKey(name))
                {
                    SMonitor.Log($"Divorced {name}");
                    __instance.friendshipData[name].Points = Math.Min(2000, Math.Max(0, points));
                    SMonitor.Log($"Resulting points: {__instance.friendshipData[name].Points}");
                }

                if (__instance.spouse == name)
                {
                    __instance.spouse = null;
                }
                ModEntry.currentSpouses.Remove(__instance.UniqueMultiplayerID);
                ModEntry.currentUnofficialSpouses.Remove(__instance.UniqueMultiplayerID);
                ModEntry.ResetSpouses(__instance);

                SMonitor.Log($"New spouse: {__instance.spouse}, married {__instance.isMarriedOrRoommates()}");
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_doDivorce_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static bool Farmer_isMarried_Prefix(Farmer __instance, ref bool __result)
        {
            try
            {
                __result = __instance.team.IsMarried(__instance.UniqueMultiplayerID) || ModEntry.GetSpouses(__instance, true).Count > 0;
                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_isMarried_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        /// <summary>
        /// Removes IsMarriedOrRoommates() checks from giving a wedding ring to another player.
        /// </summary>
        public static bool Farmer_checkAction_Prefix(Farmer __instance, Farmer who, GameLocation location, ref bool __result)
        {
            try
            {
                if (__instance.hidden.Value || (Game1.CurrentEvent is not null))
                {
                    return true;
                } 

                // (O)801 is wedding ring
                if (who.CurrentItem is not null && who.CurrentItem.QualifiedItemId == "(O)801" && !__instance.isEngaged() && !who.isEngaged()) 
                { 
                    who.Halt();
                    who.faceGeneralDirection(__instance.getStandingPosition(), 0, false);
                    string question2 = Game1.content.LoadString("Strings\\UI:AskToMarry_" + (__instance.IsMale ? "Male" : "Female"), __instance.Name);
                    location.createQuestionDialogue(question2, location.createYesNoResponses(), delegate (Farmer _, string answer)
                    {
                        if (answer == "Yes")
                        {
                            who.team.SendProposal(__instance, ProposalType.Marriage, who.CurrentItem.getOne());
                            Game1.activeClickableMenu = new PendingProposalDialog();
                        }
                    }, null);
                    __result = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        public static bool skipSpouse = false;
        public static void Farmer_spouse_Postfix(Farmer __instance, ref string __result)
        {
            if (skipSpouse)
                return;
            try
            {
                skipSpouse = true;
                if (ModEntry.TempOfficialSpouse != null && __instance.friendshipData.ContainsKey(ModEntry.TempOfficialSpouse.Name) && __instance.friendshipData[ModEntry.TempOfficialSpouse.Name].IsMarried())
                {
                    __result = ModEntry.TempOfficialSpouse.Name;
                }
                else
                {
                    var spouses = ModEntry.GetSpouses(__instance, true);
                    string aspouse = null;
                    foreach(var spouse in spouses)
                    {
                        if (aspouse is null)
                            aspouse = spouse.Key;
                        if (__instance.friendshipData.TryGetValue(spouse.Key, out var f) && f.IsEngaged())
                        {
                            __result = spouse.Key;
                            break;
                        }
                    }
                    if(__result is null && aspouse is not null)
                    {
                        __result = aspouse;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_spouse_Postfix)}:\n{ex}", LogLevel.Error);
            }
            skipSpouse = false;
        }

        public static void Farmer_getSpouse_Postfix(Farmer __instance, ref NPC __result)
        {
            try
            {

                if (ModEntry.TempOfficialSpouse is not null && __instance.friendshipData.ContainsKey(ModEntry.TempOfficialSpouse.Name) && __instance.friendshipData[ModEntry.TempOfficialSpouse.Name].IsMarried())
                {
                    __result = ModEntry.TempOfficialSpouse;
                }
                else
                {
                    var spouses = ModEntry.GetSpouses(__instance, true);
                    NPC aspouse = null;
                    foreach (var spouse in spouses)
                    {
                        aspouse ??= spouse.Value;

                        if (__instance.friendshipData[spouse.Key].IsEngaged())
                        {
                            __result = spouse.Value;
                            break;
                        }
                    }
                    if (__result is null && aspouse is not null)
                    {
                        __result = aspouse;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_getSpouse_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Prevents the method from prioritizing multiplayer farmer spouses over NPC spouses.
        /// </summary>
        public static bool Farmer_GetSpouseFriendship_Prefix(Farmer __instance, ref Friendship __result)
        {
            try
            {
                if (__instance.spouse is null || !__instance.friendshipData.TryGetValue(__instance.spouse, out Friendship friendship))
                    return true;

                __result = friendship;
                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_GetSpouseFriendship_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        /// <summary>
        /// In Event.LoadActors, there's a foreach loop where each npc in the event spawns in all their children near them.
        /// They use NPC.getSpouse().getChildren() to get the children.
        /// This prefix modifies the farmer's children in that case to only include the children with the npc in question.
        /// It does this by storing the name of the last character got with Game1.getCharacterFromName, which is called at the beginning of each iteration of the foreach loop.
        /// The flag EventPatches.startingLoadActors is set when Event.LoadActors is called.
        /// I am going to replace this with a transpiler because it is super messy, but I'll leave the code here in case that transpiler ever breaks or something.
        /// </summary>
        internal static bool Farmer_getChildren_Prefix(Farmer __instance, ref List<Child> __result)
        {
            try
            {

                if (EventPatches.startingLoadActors && Environment.StackTrace.Contains("command_loadActors") && !Environment.StackTrace.Contains("addActor") && !Environment.StackTrace.Contains("Dialogue") && !Environment.StackTrace.Contains("checkForSpecialCharacters") && Game1Patches.lastGotCharacter != null && __instance != null)
                {
                    __result = Utility.getHomeOfFarmer(__instance)?.getChildren()?.FindAll(c => ModEntry.TryGetNPCParent(c, out string npcParent) && npcParent == Game1Patches.lastGotCharacter) ?? new List<Child>();
                    return false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Farmer_getChildren_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }
    }
}
