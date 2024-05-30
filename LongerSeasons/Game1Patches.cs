using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace LongerSeasons
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        /// <summary>
        /// Replaces the part of Game1._newDayAfterFade that changes the season if the day is over 28
        /// </summary>
        public static IEnumerable<CodeInstruction> Game1__newDayAfterFade_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log($"Transpiling Game1._newDayAfterFade");

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldsfld && (FieldInfo)codes[i].operand == typeof(Game1).GetField(nameof(Game1.dayOfMonth), BindingFlags.Public | BindingFlags.Static) && codes[i + 1].opcode == OpCodes.Ldc_I4_S && (sbyte)codes[i + 1].operand == 28)
                {
                    // Replaces the number 28 with a call to GetDaysPerMonth()
                    SMonitor.Log($"Changing days per month");
                    codes[i + 1].opcode = OpCodes.Call;
                    codes[i + 1].operand = AccessTools.Method(typeof(Utilities), nameof(Utilities.GetDaysPerMonth));
                    break;
                }
            }

            return codes.AsEnumerable();
        }

        private static void Game1__newDayAfterFade_Prefix()
        {
            SMonitor.Log($"dom {Game1.dayOfMonth}, year {Game1.year}, season {Game1.currentSeason}");
        }
    }
}