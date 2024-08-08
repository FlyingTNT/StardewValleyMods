using HarmonyLib;
using Netcode;
using StardewModdingAPI.Utilities;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace LongerSeasons
{
            

    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
 
        private static bool WorldDate_TotalDays_Getter_Prefix(WorldDate __instance, ref int __result)
        {
            __result = ((__instance.Year - 1) * (Config.MonthsPerSpring + Config.MonthsPerSummer + Config.MonthsPerFall + Config.MonthsPerWinter) + __instance.SeasonIndex) * Config.DaysPerMonth + (__instance.DayOfMonth - 1);
            return false;
        }
        private static bool WorldDate_TotalDays_Setter_Prefix(WorldDate __instance, ref int value)
        {
            int SpringMaxIndex = Config.MonthsPerSpring;
            int SummerMaxIndex = Config.MonthsPerSummer + Config.MonthsPerSpring;
            int FallMaxIndex = Config.MonthsPerFall + Config.MonthsPerSummer + Config.MonthsPerSpring;
            int WinterMaxIndex = Config.MonthsPerWinter + Config.MonthsPerFall + Config.MonthsPerSummer + Config.MonthsPerSpring;
            int totalMonthsinYear = Config.MonthsPerWinter + Config.MonthsPerFall + Config.MonthsPerSummer + Config.MonthsPerSpring;
            int totalMonths = value / Config.DaysPerMonth;
            __instance.DayOfMonth = value % Config.DaysPerMonth + 1;
            
            if (totalMonths % totalMonthsinYear < SpringMaxIndex) {
                __instance.Season = Season.Spring;
            }
            else if (totalMonths % totalMonthsinYear < SummerMaxIndex) {
                __instance.Season = Season.Summer;
            }
            else if (totalMonths % totalMonthsinYear < FallMaxIndex) {
                __instance.Season = Season.Fall;
            }
            else if (totalMonths % totalMonthsinYear < WinterMaxIndex) {
                __instance.Season = Season.Winter;
            }
            
            __instance.Year = (totalMonths / totalMonthsinYear) + 1;
            return false;

        }
    }
}