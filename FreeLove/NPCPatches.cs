using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Audio;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Object = StardewValley.Object;

namespace FreeLove
{
    public static class NPCPatches
    {
        private static IMonitor SMonitor;
        private static ModConfig Config;
        private static IModHelper SHelper;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            SMonitor = monitor;
            Config = config;
            SHelper = helper;
        }

        /// <summary>
        /// Prevent Emily from talking about how you're married to Haley and not her if you are married to both. (there are probably other cases too)
        /// </summary>
        internal static bool NPC_tryToRetrieveDialogue_Prefix(NPC __instance, ref Dialogue __result, string appendToEnd)
        {
            try
            {
                if (appendToEnd.Contains("_inlaw_") && Game1.player.friendshipData.ContainsKey(__instance.Name) && Game1.player.friendshipData[__instance.Name].IsMarried())
                {
                    __result = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_tryToRetrieveDialogue_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        internal static void NPC_GetDispositionModifiedString_Prefix(NPC __instance, ref bool __state)
        {
            try
            {
                if (Game1.player.isMarriedOrRoommates() && Game1.player.friendshipData.ContainsKey(__instance.Name) && Game1.player.friendshipData[__instance.Name].IsMarried() && Game1.player.spouse != __instance.Name)
                {
                    ModEntry.TempOfficialSpouse = __instance;
                    __state = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_GetDispositionModifiedString_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        internal static void NPC_GetDispositionModifiedString_Postfix(bool __state)
        {
            try
            {
                if (__state)
                {
                    ModEntry.TempOfficialSpouse = null;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_GetDispositionModifiedString_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /*
         * marriageDuties info:
         * Called in Npc.OnDayStarted()
         * Sets NPC.shouldSayMarriageDialogue to true
         * Sets their DefaultMap to their spouse's home
         * If they aren't home, warps them home
         * Sets their marriage dialogue to a basic dialogue
         * Checks for specific house positions/dialogue options
         * 
         * Position/Dialogue options (in order of execution):
         * All if statements return if met unless noted otherwise
         * 
         * If they give birth today, moves them to the kitchen and clears dialogue
         * If they gave birth recently, moves to kitchen and sets dialouge based on # of kids IF ONLY ONE OR TWO KODS
         * Moves them to the kitchen unconditionally
         * If they did not sleep in a bed, gives them dialogue about that
         * If they have specific marriage dialogue for this season+day, sets that
         * If their schedule has a specific thing in their schedule for this DoW (like marriage_Sat), sets their funLeave dialogue
         * If their schedule is marriageJob, sets their jobLeave dialogue
         * If it's not raining or winter, and it's Saturday, moves them to their patio
         * If their number of hearts is less than 12 (or 11/10 if they were kissed/gifted yesterday), rendomly places them in front of a furniture/in bed with sad dialogue
         * If they gave birth <7 days ago, has a changce to put them in the kitchen with relevant dialogue
         * If they have one or two kids, has a chance to put them in the kitchen with relevant dialogue
         * If they were going to give the player breakfast but the kitchen spot is blocked, move them to the bed and say they chose not to make breakfast. Does not return
         * If it's not raining and they can stand on the porch, waters the pets, and randomly waters crops/feeds animals/repairs fences, with the former being the most likely and the latter the least. Also has relevant dialogue.
         * Has a chance to teleport to a random spot and put a new furniture/wallpaper/flooring
         * If it's raining and they have <11 hearts, has a chance to stare out the window with sad dialogue
         * Has a chance to put them in their spouse room
         * Puts them in the kitchen spot
         * 
         * Things we need to fix:
         * The checks for only one or two kids => transpiler change it to >= 2
         * The check for if the porch is occupied (ignores other NPCs)
         * Placing the spouse in the kitchen spot (can put multiple NPCs there) => maybe use alternate kitchen spots if the main one is occupied (like in front of a fridge or even a random spot or smth)
         */
        public static void NPC_marriageDuties_Prefix(NPC __instance)
        {
            try
            {
                if (ModEntry.GetSpouses(Game1.player, false).ContainsKey(__instance.Name))
                {
                    ModEntry.TempOfficialSpouse = __instance;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_marriageDuties_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void NPC_marriageDuties_Postfix(NPC __instance)
        {
            try
            {
                if (ModEntry.TempOfficialSpouse == __instance)
                {
                    ModEntry.TempOfficialSpouse = null;
                }

                if(!Config.UseLegacySpousePlacement && ModEntry.IsTileOccupiedByCharacterOtherThan(__instance, __instance.currentLocation, __instance.Tile))
                {
                    SMonitor.Log($"Moving {__instance.Name} from ({__instance.Tile.X}, {__instance.Tile.Y}) in the {__instance.currentLocation.Name}.");

                    ModEntry.MoveToNewStandingSpot(__instance);
                }

                return;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_marriageDuties_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static bool NPC_engagementResponse_Prefix(NPC __instance, Farmer who, ref bool asRoommate)
        {
            SMonitor.Log($"engagement response for {__instance.Name}");
            if (asRoommate)
            {
                SMonitor.Log($"{__instance.Name} is roomate");
                if (ModEntry.Config.RoommateRomance)
                    asRoommate = false;
                return true;
            }
            if (!who.friendshipData.ContainsKey(__instance.Name))
            {
                SMonitor.Log($"{who.Name} has no friendship data for {__instance.Name}", LogLevel.Error);
                return false;
            }
            return true;
        }
        public static bool NPC_setUpForOutdoorPatioActivity_Prefix(NPC __instance)
        {
            if (!SHelper.ModRegistry.IsLoaded("aedenthorn.CustomSpousePatioRedux") && Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Sat") && Game1.MasterPlayer.spouse != __instance.Name)
            {
                SMonitor.Log($"preventing {__instance.Name} from going to spouse patio");
                return false;
            }
            return true;
        }
        public static void NPC_engagementResponse_Postfix(Farmer who)
        {
            ModEntry.ResetSpouses(who);
        }

        /// <summary>
        /// Base method doc: return true if spouse encountered obstacle. if force == true then the obstacle check will be ignored and spouse will absolutely be put into bed.
        /// </summary>
        public static bool NPC_spouseObstacleCheck_Prefix(NPC __instance, GameLocation currentLocation, ref bool __result)
        {
            if (!Config.EnableMod || currentLocation is not FarmHouse)
                return true;
            if (NPC.checkTileOccupancyForSpouse(currentLocation, __instance.Tile, __instance.Name))
            {
                Game1.warpCharacter(__instance, __instance.DefaultMap, (Game1.getLocationFromName(__instance.DefaultMap) as FarmHouse).getSpouseBedSpot(__instance.Name));
                __instance.faceDirection(1);
                __result = true;
            }
            return false;
        }

        public static bool NPC_isRoommate_Prefix(NPC __instance, ref bool __result)
        {
            try
            {

                if (!__instance.IsVillager)
                {
                    __result = false;
                    return false;
                }
                foreach (Farmer f in Game1.getAllFarmers())
                {
                    if (f.isRoommate(__instance.Name))
                    {
                        __result = true;
                        return false;
                    }
                }
                __result = false;
                return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_isRoommate_Prefix)}:\n{ex}", LogLevel.Error);
                return true; // run original logic
            }
        }

        public static bool NPC_getSpouse_Prefix(NPC __instance, ref Farmer __result)
        {
            // A lot of methods check npc.getSpouse() == Game1.player, so prioritizing Game1.player reduces the number of patches we have to do.
            if(Game1.player.friendshipData.ContainsKey(__instance.Name) && Game1.player.friendshipData[__instance.Name].IsMarried())
            {
                __result = Game1.player;
                return false;
            }

            foreach (Farmer f in Game1.getAllFarmers())
            {
                if (f.friendshipData.ContainsKey(__instance.Name) && f.friendshipData[__instance.Name].IsMarried())
                {
                    __result = f;
                    return false;
                }
            }
            return true;
        }

        public static bool NPC_isMarried_Prefix(NPC __instance, ref bool __result)
        {
            __result = false;
            if (!__instance.IsVillager)
            {
                return false;
            }
            foreach (Farmer f in Game1.getAllFarmers())
            {
                if (f.friendshipData.ContainsKey(__instance.Name) && f.friendshipData[__instance.Name].IsMarried())
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }

        public static bool NPC_isMarriedOrEngaged_Prefix(NPC __instance, ref bool __result)
        {
            __result = false;
            if (!__instance.IsVillager)
            {
                return false;
            }
            foreach (Farmer f in Game1.getAllFarmers())
            {
                if (f.friendshipData.ContainsKey(__instance.Name) && (f.friendshipData[__instance.Name].IsMarried() || f.friendshipData[__instance.Name].IsEngaged()))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }


        internal static void NPC_loadCurrentDialogue_Prefix(NPC __instance)
        {
            try
            {
                if (ModEntry.GetSpouses(Game1.player, false).ContainsKey(__instance.Name))
                {
                    ModEntry.TempOfficialSpouse = __instance;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_loadCurrentDialogue_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }


        public static void NPC_loadCurrentDialogue_Postfix()
        {
            try
            {
                ModEntry.TempOfficialSpouse = null;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_loadCurrentDialogue_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Temporarily change who's official spouse to __instance (if they are married) so that the base method will check for kissing
        /// </summary>
        public static void NPC_checkAction_Prefix(NPC __instance, Farmer who, ref string __state)
        {
            if (!Config.EnableMod)
                return;

            try
            {
                ModEntry.ResetSpouses(who);
                if (ModEntry.GetSpouses(who, false).ContainsKey(__instance.Name))
                {
                    ModEntry.TempOfficialSpouse = __instance;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_checkAction_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return;
        }

        public static void NPC_checkAction_Postfix(Farmer who, string __state)
        {
            try
            {
                ModEntry.TempOfficialSpouse = null;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_checkAction_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        private static bool WasAbleToDoMovieTheatreTranspiler = false;

        public static bool NPC_tryToReceiveActiveObject_Prefix(NPC __instance, Farmer who, bool probe, ref bool __result)
        {
            try
            {
                who.friendshipData.TryGetValue(__instance.Name, out Friendship friendship);

                if (who.ActiveObject.HasContextTag(ItemContextTagManager.SanitizeContextTag("propose_roommate_" + __instance.Name)))
                {
                    if (!probe)
                    {
                        SMonitor.Log($"Roommate proposal item {who.ActiveObject.Name} to {__instance.Name}");
                        HandleRoommateProposal(__instance, who);
                    }

                    __result = true;
                    return false;
                }
                else if (who.ActiveObject.QualifiedItemId == "(O)458")
                {
                    if (!probe)
                    {
                        SMonitor.Log($"Try give bouquet to {__instance.Name}");
                        HandleBouquet(__instance, who);
                    }

                    __result = true;
                    return false;
                }
                else if (who.ActiveObject.QualifiedItemId == "(O)460")
                {
                    if (!probe)
                    {
                        SMonitor.Log($"Try give pendant to {__instance.Name}");
                        HandlePendant(__instance, who);
                    }

                    __result = true;
                    return false;
                }
                else if (!WasAbleToDoMovieTheatreTranspiler && who.ActiveObject.QualifiedItemId == "(O)809")
                {
                    // Because this requires so many checks and we only change one call, I do this change in a transpiler, but I have this as a fallback in case the method changes and the transpiler breaks.
                    if (!Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccMovieTheater") ||
                       (__instance.SpeaksDwarvish() && !who.canUnderstandDwarves) ||
                       (__instance.Name == "Krobus" && Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Fri") ||
                       (__instance.Name == "Leo" && !Game1.MasterPlayer.mailReceived.Contains("leoMoved")) ||
                       (who.lastSeenMovieWeek.Value >= Game1.Date.TotalWeeks) ||
                       (Utility.isFestivalDay()) ||
                       (Game1.timeOfDay > 2100) ||
                       (who.team.movieInvitations.Any(invitation => (invitation.farmer == who || invitation.invitedNPC == __instance))) ||
                       (__instance.lastSeenMovieWeek.Value >= Game1.Date.TotalWeeks) ||
                       MovieTheater.GetResponseForMovie(__instance) == "reject")
                    {
                        return true;
                    }


                    SMonitor.Log($"Trying to give movie ticket to {__instance.Name}");

                    if (!probe)
                    {
                        HandleMovieTicket(__instance, who);
                    }

                    __result = true;
                    return false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_tryToReceiveActiveObject_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        public static void HandleRoommateProposal(NPC npc, Farmer who)
        {
            if (who.getFriendshipHeartLevelForNPC(npc.Name) >= 10 && who.HouseUpgradeLevel >= 1)
            {
                SMonitor.Log($"proposal success!");
                DoEngagementResponse(npc, who, true);
            }
            else if (npc.Name != "Krobus")
            {
                Game1.drawObjectDialogue(Game1.parseText(Game1.content.LoadString("Strings\\Characters:MovieInvite_NoTheater", npc.displayName)));
            }
        }

        public static void HandleBouquet(NPC npc, Farmer who)
        {
            // If they are already married, change them to the official spouse
            if (ModEntry.GetSpouses(who, true).ContainsKey(npc.Name))
            {
                who.spouse = npc.Name;
                ModEntry.ResetSpouses(who);
                Game1.currentLocation.playSound("dwop", null, null, SoundContext.NPC);
                if (ModEntry.CustomSpouseRoomsAPI == null)
                {
                    FarmHouse fh = Utility.getHomeOfFarmer(who);
                    fh.showSpouseRoom();
                    SHelper.Reflection.GetMethod(fh, "resetLocalState").Invoke();
                }

                SMonitor.Log($"{npc.Name} is the new official spouse!");

                return;
            }

            who.friendshipData.TryGetValue(npc.Name, out Friendship friendship);

            if (!npc.datable.Value)
            {
                if (Game1.random.NextBool())
                {
                    Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\StringsFromCSFiles:NPC.cs.3955", npc.displayName));
                }
                else
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectBouquet_NotDatable") ?? npc.TryGetDialogue("RejectBouquet") ?? (Game1.random.NextBool() ? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs.3956") : new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs.3957", isGendered: true)));
                    Game1.drawDialogue(npc);
                }
            }
            else
            {
                friendship ??= (who.friendshipData[npc.Name] = new Friendship());

                if (friendship.IsDating())
                {
                    Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\UI:AlreadyDatingBouquet", npc.displayName));
                }
                else if (friendship.IsDivorced())
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectBouquet_Divorced") ?? npc.TryGetDialogue("RejectBouquet") ?? new Dialogue(npc, "Strings\\Characters:Divorced_bouquet"));
                    Game1.drawDialogue(npc);
                }
                else if (friendship.Points < Config.MinPointsToDate / 2f)
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectBouquet_VeryLowHearts") ?? npc.TryGetDialogue("RejectBouquet") ?? (Game1.random.NextBool() ? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs.3958") : new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs.3959", isGendered: true)));
                    Game1.drawDialogue(npc);
                }
                else if (friendship.Points < Config.MinPointsToDate)
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectBouquet_LowHearts") ?? npc.TryGetDialogue("RejectBouquet") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + Game1.random.Choose("3960", "3961")));
                    Game1.drawDialogue(npc);
                }
                else
                {
                    friendship.Status = FriendshipStatus.Dating;
                    Game1.Multiplayer.globalChatInfoMessage("Dating", Game1.player.Name, npc.GetTokenizedDisplayName());
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("AcceptBouquet") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + Game1.random.Choose("3962", "3963"), isGendered: true));
                    who.autoGenerateActiveDialogueEvent("dating_" + npc.Name);
                    who.autoGenerateActiveDialogueEvent("dating");
                    who.changeFriendship(25, npc);
                    who.reduceActiveItemByOne();
                    who.completelyStopAnimatingOrDoingAction();
                    npc.doEmote(20);
                    Game1.drawDialogue(npc);
                }
            }
        }

        public static void HandlePendant(NPC npc, Farmer who)
        {
            who.friendshipData.TryGetValue(npc.Name, out Friendship friendship);

            bool isDivorced = friendship?.IsDivorced() ?? false;
            if (who.isEngaged())
            {
                SMonitor.Log($"Tried to give pendant while engaged");

                npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectMermaidPendant") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + Game1.random.Choose("3965", "3966"), isGendered: true));
                Game1.drawDialogue(npc);
            }
            else if (!npc.datable.Value || isDivorced || (friendship != null && friendship.Points < Config.MinPointsToMarry * 0.6f))
            {
                SMonitor.Log($"Tried to give pendant to someone not datable ({!npc.datable.Value}) or divorced ({isDivorced}), or with too low friendship ({friendship?.Points})");

                if (Game1.random.NextBool())
                {
                    Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\StringsFromCSFiles:NPC.cs.3969", npc.displayName));
                }
                else
                {
                    npc.CurrentDialogue.Push(((!npc.datable.Value) ? npc.TryGetDialogue("RejectMermaidPendant_NotDatable") : null) ?? (isDivorced ? npc.TryGetDialogue("RejectMermaidPendant_Divorced") : null) ?? ((npc.datable.Value && friendship != null && friendship.Points < Config.MinPointsToMarry * 0.6f) ? npc.TryGetDialogue("RejectMermaidPendant_Under8Hearts") : null) ?? npc.TryGetDialogue("RejectMermaidPendant") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + ((npc.Gender == Gender.Female) ? "3970" : "3971")));
                    Game1.drawDialogue(npc);
                }
            }
            else if (npc.datable.Value && friendship != null && friendship.Points < Config.MinPointsToMarry)
            {
                SMonitor.Log($"Tried to give pendant with too little friendship ({friendship.Points} < {Config.MinPointsToMarry})");

                if (!friendship.ProposalRejected)
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectMermaidPendant_Under10Hearts") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + Game1.random.Choose("3972", "3973")));
                    Game1.drawDialogue(npc);
                    who.changeFriendship(-20, npc);
                    friendship.ProposalRejected = true;
                }
                else
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectMermaidPendant_Under10Hearts_AskedAgain") ?? npc.TryGetDialogue("RejectMermaidPendant_Under10Hearts") ?? npc.TryGetDialogue("RejectMermaidPendant") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs." + Game1.random.Choose("3974", "3975"), isGendered: true));
                    Game1.drawDialogue(npc);
                    who.changeFriendship(-50, npc);
                }
            }
            else if (npc.datable.Value && who.HouseUpgradeLevel < 1)
            {
                SMonitor.Log($"Tried to give pendant with an un-upgraded house ({who.HouseUpgradeLevel})");

                if (Game1.random.NextBool())
                {
                    Game1.drawObjectDialogue(Game1.content.LoadString("Strings\\StringsFromCSFiles:NPC.cs.3969", npc.displayName));
                }
                else
                {
                    npc.CurrentDialogue.Push(npc.TryGetDialogue("RejectMermaidPendant_NeedHouseUpgrade") ?? npc.TryGetDialogue("RejectMermaidPendant") ?? new Dialogue(npc, "Strings\\StringsFromCSFiles:NPC.cs.3972"));
                    Game1.drawDialogue(npc);
                }
            }
            else
            {
                SMonitor.Log($"Tried to give pendant to someone marriable (successful)");
                DoEngagementResponse(npc, who);
            }
        }

        public static void HandleMovieTicket(NPC npc, Farmer who)
        {
            npc.CurrentDialogue.Push((ModEntry.IsMarried(npc, who) ? Dialogue.TryGetDialogue(npc, "Strings\\Characters:MovieInvite_Spouse_" + npc.Name) : null) ?? npc.TryGetDialogue("MovieInvitation") ?? new Dialogue(npc, "Strings\\Characters:MovieInvite_Invited", npc.GetDispositionModifiedString("Strings\\Characters:MovieInvite_Invited")));
            Game1.drawDialogue(npc);
            who.reduceActiveItemByOne();
            who.completelyStopAnimatingOrDoingAction();
            who.currentLocation.localSound("give_gift");
            MovieTheater.Invite(who, npc);
            if (who == Game1.player)
            {
                Game1.Multiplayer.globalChatInfoMessage("MovieInviteAccept", Game1.player.displayName, npc.GetTokenizedDisplayName());
            }
        }

        private static void DoEngagementResponse(NPC npc, Farmer who, bool asRoommate = false)
        {
            AccessTools.Method(typeof(NPC), "engagementResponse").Invoke(npc, new object[] { who, asRoommate});
        }

        public static IEnumerable<CodeInstruction> NPC_tryToReceiveActiveObject_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log("Transpiling NPC.tryToRecieveActiveObject");

            // Changes this.getSpouse() == who to ModEntry.IsMarried(this, who)
            var codes = new List<CodeInstruction>(instructions);

            try
            {
                bool startLooking = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (startLooking)
                    {
                        if (codes[i].opcode == OpCodes.Ldarg_0 && // Loads this (NPC)
                            codes[i + 1].opcode == OpCodes.Call && codes[i + 1].operand is MethodInfo info && info == AccessTools.Method(typeof(NPC), nameof(NPC.getSpouse)) && // Calls getSpouse()
                            codes[i + 2].opcode == OpCodes.Ldloc_0 && // Loads who (Farmer)
                            codes[i + 3].opcode == OpCodes.Ldfld && // Also loads who (both are necessary)
                            codes[i + 4].opcode == OpCodes.Beq_S) // Breaks if getSpouse() == who
                        {
                            SMonitor.Log($"Found getSpouse(). Replacing!");

                            // Swap the order of the codes so the Call to NPC.getSpouse() is in the back.
                            (codes[i], codes[i + 1], codes[i + 2], codes[i + 3]) = (codes[i], codes[i + 2], codes[i + 3], codes[i + 1]);

                            // Replace NPC.getSpouse() with ModEntry.IsMarried()
                            codes[i + 3].operand = AccessTools.Method(typeof(ModEntry), nameof(ModEntry.IsMarried), new Type[] { typeof(NPC), typeof(Farmer) });

                            // Replace the breq with brtrue
                            codes[i + 4].opcode = OpCodes.Brtrue_S;

                            WasAbleToDoMovieTheatreTranspiler = true;
                            break;
                        }
                        else if ((codes[i].operand as string) == "(O)458")
                        {
                            SMonitor.Log("Didn't find NPC.getSpouse()");
                            break;
                        }
                    }
                    else if ((codes[i].operand as string) == "RejectMovieTicket_DontWantToSeeThatMovie")
                    {
                        SMonitor.Log($"Got movie rejection string!");
                        startLooking = true;
                    }
                }
            }
            catch(Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_tryToReceiveActiveObject_Transpiler)}:\n{ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        public static bool NPC_playSleepingAnimation_Prefix(NPC __instance, bool ___isPlayingSleepingAnimation)
        {
            try
            {
                if (___isPlayingSleepingAnimation)
                    return true;
                Dictionary<string, string> animationDescriptions = Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions");
                if (animationDescriptions.TryGetValue(__instance.Name.ToLower() + "_sleep", out string sleepString) && !int.TryParse(sleepString.Split('/')[0], out int sleep_frame))
                    return false;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_playSleepingAnimation_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        public static void NPC_playSleepingAnimation_Postfix(NPC __instance)
        {
            try
            {
                Dictionary<string, string> animationDescriptions = DataLoader.AnimationDescriptions(Game1.content);
                if (!animationDescriptions.ContainsKey(__instance.Name.ToLower() + "_sleep") && animationDescriptions.TryGetValue(__instance.Name + "_Sleep", out string animationData))
                {
                    if (!int.TryParse(animationData.Split('/')[0], out int sleep_frame))
                        return;

                    __instance.Sprite.ClearAnimation();
                    __instance.Sprite.AddFrame(new FarmerSprite.AnimationFrame(sleep_frame, 100, secondaryArm: false, flip: false));
                    __instance.Sprite.loop = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_playSleepingAnimation_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }
        
        public static void Character_displayName_Getter_Postfix(ref Character __instance, ref string __result)
        {
            try
            {
                if (__instance.Name is null || __instance is not Child || !Config.ShowParentNames || !__instance.modData.TryGetValue("aedenthorn.FreeLove/OtherParent", out string parentName))
                    return;

                string displayName = Game1.getCharacterFromName(parentName)?.displayName ?? parentName;

                __result = $"{__result} ({displayName})";
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Character_displayName_Getter_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static IEnumerable<CodeInstruction> NPC_isAdoptionSpouse_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log("Transpiling NPC.isAdoptionSpouse");

            var codes = new List<CodeInstruction>(instructions);

            // Finds the place where it checks "this.Gender == spouse.Gender" and adds an "|| AllowGayPregnancies()" to the end of it
            try
            {
                var gender = AccessTools.PropertyGetter(typeof(Character), nameof(Character.Gender));

                for (int i = 4; i < codes.Count; i++)
                {
                    if (codes[i - 2].opcode == OpCodes.Callvirt && codes[i - 2].operand is MethodInfo info1 && info1 == gender && // Gets NPC gender
                        codes[i - 4].opcode == OpCodes.Callvirt && codes[i - 4].operand is MethodInfo info2 && info2 == gender && // Gets spouse gender
                        codes[i - 1].opcode == OpCodes.Ceq) // Checks equality
                    {
                        SMonitor.Log("Adding AllowGayPregnancies check.");

                        // Note that these are added in reverse order
                        codes.Insert(i, new CodeInstruction(OpCodes.Or));
                        codes.Insert(i, CodeInstruction.Call(typeof(NPCPatches), nameof(NPCPatches.AllowGayPregnancies)));
                    }
                }
            }
            catch(Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_isAdoptionSpouse_Transpiler)}: {ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        /// <summary>
        /// A method used in the isAdoptionSpouse transpiler that safely gets the value for Config.GayPregnancies
        /// </summary>
        public static bool AllowGayPregnancies() => ModEntry.Config?.GayPregnancies ?? false;

        /// <summary>
        /// Adds a call to <see cref="NPC.checkTileOccupancyForSpouse(GameLocation, Vector2, string)"/> that checks if the tile is occuped
        /// by any character other than the NPC instance (the base method ignores all characters, presumably so it can't hit itself)
        /// 
        /// This could be a postfix except that the underlying method is one line of code and thus very likely to be inlined.
        /// 
        /// This should be patched with all of the base parameter types specified so that it fails if the argument order changes
        /// 
        /// CURRENTLY UNUSED
        /// </summary>
        public static IEnumerable<CodeInstruction> NPC_checkTileOccupancyForSpouse_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log("Transpiling NPC.CheckTileOccupancyForSpouse");

            var codes = new List<CodeInstruction>(instructions);

            try
            {
                for (int i = codes.Count - 1; i > 0; i--)
                {
                    if (codes[i].opcode != OpCodes.Ret)
                        continue;

                    SMonitor.Log("Adding IsTileOccupiedByCharacterOtherThan call.");

                    codes.Insert(i, new CodeInstruction(OpCodes.Or));
                    codes.Insert(i, CodeInstruction.Call(typeof(ModEntry), nameof(ModEntry.IsTileOccupiedByCharacterOtherThan)));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_2)); // Arg 2 is the Vector2
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1)); // Arg 1 is the GameLocation
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0)); // Arg 0 is the NPC

                    break;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(NPC_checkTileOccupancyForSpouse_Transpiler)}: {ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }
    }
}
