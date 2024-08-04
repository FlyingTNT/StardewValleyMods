using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace FreeLove
{
    public static class UIPatches
    {
        private static IMonitor SMonitor;
        private static IModHelper SHelper;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }
        public static void SocialPage_drawNPCSlot_prefix(SocialPage __instance, int i)
        {
            try
            {
                SocialPage.SocialEntry entry = __instance.GetSocialEntry(i);
                if (entry.IsChild)
                {
                    if (entry.DisplayName.EndsWith(")"))
                    {
                        AccessTools.FieldRefAccess<SocialPage.SocialEntry, string>(entry, "DisplayName") = string.Join(" ", entry.DisplayName.Split(' ').Reverse().Skip(1).Reverse());
                        __instance.SocialEntries[i] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(SocialPage_drawNPCSlot_prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// The first time this method is called, it caches the result into ___CachedIsMarriedToAnyone.
        /// Subsequent times, the cached value is used.
        /// </summary>
        public static void SocialPage_isMarriedToAnyone_Prefix(SocialPage.SocialEntry __instance, ref bool? ___CachedIsMarriedToAnyone)
        {
            try
            {
                if (___CachedIsMarriedToAnyone.HasValue)
                    return;

                foreach (Farmer farmer in Game1.getAllFarmers())
                {
                    if (ModEntry.GetSpouses(farmer, true).ContainsKey(__instance.DisplayName))
                    {
                        ___CachedIsMarriedToAnyone = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(SocialPage_isMarriedToAnyone_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// In the SocialPage draw slot methods (drawNPCSlot and drawFarmerSlot), the game draws "(girlfriend)" under the names if they are dating the player *and* the player is not.
        /// married. This transpiler applies to both methods and removes that call to Game1.player.IsMarriedOrRoommates(), instead replacing it with false, so it is 'if the character is
        /// dating the player and not false'.
        /// </summary>
        public static IEnumerable<CodeInstruction> SocialPage_drawSlot_transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log("Transpiling SocialPage.draw_____Slot");

            List<CodeInstruction> codes = instructions.ToList();
            if (SHelper.ModRegistry.IsLoaded("SG.Partners"))
            {
                SMonitor.Log("Keep Your Partners mod is loaded, not patching social page.");
                return codes.AsEnumerable();
            }
            try
            {
                MethodInfo m_IsMarriedOrRoommates = AccessTools.Method(typeof(Farmer), nameof(Farmer.isMarriedOrRoommates));
                MethodInfo m_Game1GetPlayer = AccessTools.PropertyGetter(typeof(Game1), nameof(Game1.player));

                for (int i = 1; i < codes.Count; i++)
                {
                    if (codes[i-1].opcode == OpCodes.Call && codes[i-1].operand is MethodInfo method1 && method1 == m_Game1GetPlayer && // Load Game1.player
                        codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo method2 && method2 == m_IsMarriedOrRoommates) // Call .IsMarriedOrRoommates()
                    {
                        SMonitor.Log("Removing Game1.player.IsMarriedOrRoommates() call");

                        (codes[i - 1].opcode, codes[i - 1].operand) = (OpCodes.Nop, null); // Do nothing
                        (codes[i].opcode, codes[i].operand) = (OpCodes.Ldc_I4_0, null); // Load false onto the stack

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(SocialPage_drawSlot_transpiler)}:\n{ex}", LogLevel.Error);
            }
            return codes.AsEnumerable();
        }

        public static void DialogueBox_Prefix(ref List<string> dialogues)
        {
            try
            {
                if (dialogues == null || dialogues.Count < 2)
                    return;

                if (dialogues[1] == Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1826"))
                {
                    List<string> newDialogues = new List<string>()
                    {
                        dialogues[0]
                    };



                    List<NPC> spouses = ModEntry.GetSpouses(Game1.player,true).Values.OrderBy(o => Game1.player.friendshipData[o.Name].Points).Reverse().Take(4).ToList();

                    List<int> which = new List<int>{ 0, 1, 2, 3 };

                    ModEntry.ShuffleList(ref which);

                    List<int> myWhich = new List<int>(which).Take(spouses.Count).ToList();

                    for(int i = 0; i < spouses.Count; i++)
                    {
                        switch (which[i])
                        {
                            case 0:
                                newDialogues.Add(Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1827", spouses[i].displayName));
                                break;
                            case 1:
                                newDialogues.Add(((spouses[i].Gender == 0) ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1832") : Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1834")) + " " + ((spouses[i].Gender == 0) ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1837", spouses[i].displayName[0]) : Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1838", spouses[i].displayName[0])));
                                break;
                            case 2:
                                newDialogues.Add(Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1843", spouses[i].displayName));
                                break;
                            case 3:
                                newDialogues.Add(((spouses[i].Gender == 0) ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1831") : Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1833")) + " " + ((spouses[i].Gender == 0) ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1837", spouses[i].displayName[0]) : Game1.content.LoadString("Strings\\StringsFromCSFiles:Event.cs.1838", spouses[i].displayName[i])));
                                break;
                        }
                    }
                    dialogues = new List<string>(newDialogues);
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(DialogueBox_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }
    }
}