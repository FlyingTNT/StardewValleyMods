using StardewValley;

namespace LongerSeasons
{
    public class LongerSeasonsAPI : ILongerSeasonsAPI
    {
        public int? GetCurrentSeasonMonth()
        {
            return ModEntry.currentSeasonMonth?.IsReady ?? false ? ModEntry.CurrentSeasonMonth : null;
        }

        public int? GetDaysPerMonth()
        {
            return ModEntry.daysPerMonth?.IsReady ?? false ? ModEntry.DaysPerMonth : null;
        }

        public int GetLocalDaysPerMonth()
        {
            return ModEntry.Config.DaysPerMonth;
        }
        public int? GetMonthsInSeason(Season season)
        {
            return season switch
            {
                Season.Spring => ModEntry.monthsPerSpring?.IsReady ?? false ? ModEntry.MonthsPerSpring : null,
                Season.Summer => ModEntry.monthsPerSummer?.IsReady ?? false ? ModEntry.MonthsPerSummer : null,
                Season.Fall => ModEntry.monthsPerFall?.IsReady ?? false ? ModEntry.MonthsPerFall : null,
                Season.Winter => ModEntry.monthsPerWinter?.IsReady ?? false ? ModEntry.MonthsPerWinter : null,
                _ => ModEntry.MonthsPerSpring
            };
        }

        public int GetLocalMonthsInSeason(Season season)
        {
            return season switch
            {
                Season.Spring => ModEntry.Config.MonthsPerSpring,
                Season.Summer => ModEntry.Config.MonthsPerSummer,
                Season.Fall => ModEntry.Config.MonthsPerFall,
                Season.Winter => ModEntry.Config.MonthsPerWinter,
                _ => ModEntry.Config.MonthsPerSpring
            };
        }
    }
}
