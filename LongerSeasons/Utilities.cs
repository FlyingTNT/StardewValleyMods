using StardewValley;

namespace LongerSeasons
{
    internal class Utilities
    {
        public static readonly Season[] SeasonsByIndex = new Season[]{Season.Spring, Season.Summer, Season.Fall, Season.Winter};

        public static int GetDaysPerMonth()
        {
            return ModEntry.Config?.DaysPerMonth ?? 28;
        }

        public static int GetMonthsInSeason(int seasonNumber)
        {
            if(ModEntry.Config is null)
            {
                return 1;
            }

            return seasonNumber switch
            {
                0 => ModEntry.Config.MonthsPerSpring,
                1 => ModEntry.Config.MonthsPerSummer,
                2 => ModEntry.Config.MonthsPerFall,
                3 => ModEntry.Config.MonthsPerWinter,
                _ => 1
            };
        }
    }
}
