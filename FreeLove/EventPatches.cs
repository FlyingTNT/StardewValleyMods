using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace FreeLove
{
    public static class EventPatches
    {
        private static IMonitor SMonitor;
        private static IModHelper SHelper;
        private static ModConfig Config;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
            Config = config;
        }
        public static bool startingLoadActors = false;

        /// <summary>
        /// In the flower dance, if the player is married to who, temporarily sets who as the official spouse so they get the marriage dance dialogue.
        /// </summary>
        public static void Event_answerDialogueQuestion_Prefix(NPC who, string answerKey)
        {
            try
            {
                if (answerKey == "danceAsk" && ModEntry.IsMarried(who, Game1.player))
                {
                    ModEntry.TempOfficialSpouse = who;
                }
            }

            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_answerDialogueQuestion_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void Event_answerDialogueQuestion_Postfix(NPC who, string answerKey)
        {
            try
            {
                if (answerKey == "danceAsk" && ModEntry.IsMarried(who, Game1.player))
                {
                    ModEntry.TempOfficialSpouse = null;
                }
            }

            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_answerDialogueQuestion_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void Event_command_loadActors_Prefix()
        {
            try
            {
                startingLoadActors = true;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_command_loadActors_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void Event_command_loadActors_Postfix()
        {
            try
            {
                startingLoadActors = false;
                Game1Patches.lastGotCharacter = null;

            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_command_loadActors_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// When loading actors for an event, the game by default spawns each NPC's spouse's children next to them.
        /// This patch changes that so that is spawns each NPC's spouse's children *with that NPC* next to them.
        /// It does that by replacing two instances of <see cref="Farmer.getChildren()"/> with <see cref="ModEntry.GetChildren(NPC)"/>
        /// </summary>
        public static IEnumerable<CodeInstruction> Event_DefaultCommands_LoadActors_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log("Transpiling Event.DefaultCommands.LoadActors");

            var codes = new List<CodeInstruction>(instructions);

            try
            {
                bool foundSetup = false;
                bool didFirstGetChildren = false;
                bool didSecondGetChildren = false;
                CodeInstruction loadNPCInstruction = null;

                for(int i = 0; i < codes.Count; i++)
                {
                    if(!foundSetup)
                    {
                        if (codes[i].operand is string s && s == "Set-Up") // The relevant code is in an if statement that checks for the string "Set-Up"
                        {
                            SMonitor.Log("Found \"Set-Up\"");
                            foundSetup = true;
                        }

                        continue;
                    }

                    if(!didFirstGetChildren)
                    {
                        if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method1 && method1 == AccessTools.Method(typeof(NPC), nameof(NPC.getSpouse)) &&
                            codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 1].operand is MethodInfo method2 && method2 == AccessTools.Method(typeof(Farmer), nameof(Farmer.getChildren)))
                        {
                            SMonitor.Log("Changing the first getChildren()");
                            (codes[i].opcode, codes[i].operand) = (OpCodes.Nop, null); // Remove the getSpouse() call
                            (codes[i + 1].opcode, codes[i + 1].operand) = (OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(ModEntry.GetChildren))); // Replace the Farmer.getChildren() call

                            didFirstGetChildren = true;


                            // We need to add an instruction that loads the npc variable. Because codes[i] calls NPC.getSpouse, codes[i-1] necessarily loads a npc.
                            // We check that it is ldloc or ldarg because if it happens to change to something like callvirt for whatever reason, we would need the instruction in front of that
                            // too or the code would crash, so we want to make sure the instruction is standalone. Currently it is ldloc 25, but this should still work if the variable number changes.
                            if (codes[i-1].IsLdloc() || codes[i-1].IsLdarg() || codes[i-1].IsLdarga())
                            {
                                SMonitor.Log("Got NPC load instruction");
                                loadNPCInstruction = codes[i - 1];
                            }
                        }
                    }
                    else
                    {
                        // If we for some reason failed to find the loadNPCInstruction earlier, we check in front of all of the other NPC.getSpouse's to try to recover it
                        if (loadNPCInstruction is null && 
                            codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method1 && method1 == AccessTools.Method(typeof(NPC), nameof(NPC.getSpouse)))
                        {
                            if (codes[i - 1].IsLdloc() || codes[i - 1].IsLdarg() || codes[i - 1].IsLdarga())
                            {
                                SMonitor.Log("Got NPC load instruction");
                                loadNPCInstruction = codes[i - 1];
                            }

                            continue;
                        }

                        if(codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method2 && method2 == AccessTools.Method(typeof(Farmer), nameof(Farmer.getChildren)))
                        {
                            SMonitor.Log("Changing the second getChildren()");

                            if(loadNPCInstruction is null)
                            {
                                SMonitor.Log("Did not find the NPC load instruction!");
                                break;
                            }

                            // Note that because we use inserts, these are in reverse order.
                            (codes[i].opcode, codes[i].operand) = (OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(ModEntry.GetChildren))); // Replace the Farmer.getChildren() call
                            codes.Insert(i, new CodeInstruction(loadNPCInstruction.opcode, loadNPCInstruction.operand)); // Load the npc for the ModEntry.GetChildren call
                            codes.Insert(i, new CodeInstruction(OpCodes.Pop)); // Pop the Farmer that would have been the subject of the Farmer.getChildren() call from the stack.

                            didSecondGetChildren = true;

                            break;
                        }
                    }
                }

                if(!didFirstGetChildren || !didSecondGetChildren)
                {
                    SMonitor.Log("Failed to apply the Event.DefaultCommands.LoadActors transpiler.");
                }
            }
            catch(Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(Event_DefaultCommands_LoadActors_Transpiler)}:\n{ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        /// <summary>
        /// Removes the 'cold shoulder' functionality from the dump event (run after the group 10 heart events)
        /// </summary>
        public static void Event_DefaultCommands_Dump_Postfix()
        {
            Game1.player.activeDialogueEvents.Remove("dumped_Guys");
            Game1.player.activeDialogueEvents.Remove("dumped_Girls");
            Game1.player.activeDialogueEvents.Remove("secondChance_Guys");
            Game1.player.activeDialogueEvents.Remove("secondChance_Girls");
        }
    }
}