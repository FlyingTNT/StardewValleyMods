using Common.Utilities;
using HarmonyLib;
using StardewModdingAPI;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace LongerSeasons
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {

        private static void Utility_getSeasonNameFromNumber_Postfix(ref string __result)
        {
            // Check IsWorldReady b/c the month is stored in the save file, and so we don't know it until the world is ready (basically save is loaded)
            if(Config.MonthsPerSeason > 1 && Context.IsWorldReady)
            {
                __result += $" {CurrentSeasonMonth}";
            }
        }
        public static IEnumerable<CodeInstruction> Utility_getDateStringFor_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log($"Transpiling Utility.getDateStringFor");

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S && (sbyte)codes[i].operand == 28)
                {
                    SMonitor.Log($"Changing days per month");
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = AccessTools.Method(typeof(Utilities), nameof(Utilities.GetDaysPerMonth));
                }
            }

            return codes.AsEnumerable();
        }
    }
}