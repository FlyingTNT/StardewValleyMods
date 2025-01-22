using Newtonsoft.Json.Linq;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace LongerSeasons
{
            

    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        private static int TotalMonthsInYear => MonthsPerWinter + MonthsPerFall + MonthsPerSummer + MonthsPerSpring;

        private static bool WorldDate_TotalDays_Getter_Prefix(WorldDate __instance, ref int __result)
        {
            if(!Config.UseOldDateCalculations)
            {
                __result = ((__instance.Year - 1) * 4 + __instance.SeasonIndex) * DaysPerMonth + (__instance.DayOfMonth - 1);
                return false;
            }

            __result = ((__instance.Year - 1) * (MonthsPerSpring + MonthsPerSummer + MonthsPerFall + MonthsPerWinter) + __instance.SeasonIndex) * DaysPerMonth + (__instance.DayOfMonth - 1);
            return false;
        }
        private static bool WorldDate_TotalDays_Setter_Prefix(WorldDate __instance, ref int value)
        {
            int totalMonths;
            if(!Config.UseOldDateCalculations)
            {
                int daysAfterToday = value - WorldDate.Now().TotalDays;
                if(daysAfterToday > 0)
                {
                    // Get the date that is daysAfterToday days after today.
                    ApplyDayOffset(WorldDate.Now(), CurrentSeasonMonth, daysAfterToday, __instance);
                    return false;
                }

                totalMonths = value / DaysPerMonth;
                __instance.DayOfMonth = value % DaysPerMonth + 1;
                __instance.Season = (Season)(totalMonths % 4);
                __instance.Year = totalMonths / 4 + 1;

                return false;
            }

            int SpringMaxIndex = MonthsPerSpring;
            int SummerMaxIndex = MonthsPerSummer + MonthsPerSpring;
            int FallMaxIndex = MonthsPerFall + MonthsPerSummer + MonthsPerSpring;
            int WinterMaxIndex = MonthsPerWinter + MonthsPerFall + MonthsPerSummer + MonthsPerSpring;
            totalMonths = value / DaysPerMonth;
            __instance.DayOfMonth = value % DaysPerMonth + 1;
            
            if (totalMonths % TotalMonthsInYear < SpringMaxIndex) {
                __instance.Season = Season.Spring;
            }
            else if (totalMonths % TotalMonthsInYear < SummerMaxIndex) {
                __instance.Season = Season.Summer;
            }
            else if (totalMonths % TotalMonthsInYear < FallMaxIndex) {
                __instance.Season = Season.Fall;
            }
            else if (totalMonths % TotalMonthsInYear < WinterMaxIndex) {
                __instance.Season = Season.Winter;
            }
            
            __instance.Year = (totalMonths / TotalMonthsInYear) + 1;
            return false;
        }

        private static void ApplyDayOffset(WorldDate startingDay, int startingSeasonMonth, int daysAfter, WorldDate newDay)
        {
            newDay.Year = startingDay.Year;
            daysAfter += startingDay.DayOfMonth;

            while (daysAfter > TotalMonthsInYear * DaysPerMonth)
            {
                newDay.Year++;
                daysAfter -= TotalMonthsInYear * DaysPerMonth;
            }

            int season = (int)startingDay.Season;

            while(daysAfter > DaysPerMonth)
            {
                daysAfter -= DaysPerMonth;
                if(startingSeasonMonth < GetMonthsInSeason((Season)season))
                {
                    startingSeasonMonth++;
                    continue;
                }

                season++;
                startingSeasonMonth = 1;
            }

            newDay.Season = (Season)season;

            newDay.DayOfMonth = daysAfter;
        }
    }
}