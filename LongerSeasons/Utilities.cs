using StardewValley;

namespace LongerSeasons
{
    internal class Utilities
    {
        public static readonly Season[] SeasonsByIndex = new Season[]{Season.Spring, Season.Summer, Season.Fall, Season.Winter};

        public static int GetDaysPerMonth()
        {
            return ModEntry.DaysPerMonth;
        }

        public static int GetMonthsInSeason(int seasonNumber)
        {
            if(ModEntry.Config is null)
            {
                return 1;
            }

            return seasonNumber switch
            {
                0 => ModEntry.MonthsPerSpring,
                1 => ModEntry.MonthsPerSummer,
                2 => ModEntry.MonthsPerFall,
                3 => ModEntry.MonthsPerWinter,
                _ => 1
            };
        }

        public static int GetTotalDays(WorldDate date, int month)
        {
            return ((date.Year - 1) * (ModEntry.MonthsPerSpring + ModEntry.MonthsPerSummer + ModEntry.MonthsPerFall + ModEntry.MonthsPerWinter) * ModEntry.DaysPerMonth) +
                   ((date.SeasonIndex > 0 ? ModEntry.MonthsPerSpring : 0) + (date.SeasonIndex > 1 ? ModEntry.MonthsPerSummer : 0) + (date.SeasonIndex > 2 ? ModEntry.MonthsPerFall : 0)) * ModEntry.DaysPerMonth +
                   (month - 1) * ModEntry.DaysPerMonth +
                   date.DayOfMonth;
        }

        /// <summary>
        /// Calculates the number of days away the given date is. Assumes the earliest month that would still place the date in the future if there is any such month, or the latest month that would place it 
        /// in the past if not.
        /// </summary>
        public static int GetDaysAway(WorldDate date)
        {
            WorldDate now = WorldDate.Now();

            int month;

            if(date.Year > now.Year)
            {
                month = 1;
            }
            else if(date.Year < now.Year)
            {
                month = GetMonthsInSeason(date.SeasonIndex);
            }
            else
            {
                if(date.SeasonIndex < now.SeasonIndex)
                {
                    month = GetMonthsInSeason(date.SeasonIndex);
                }
                else if(date.SeasonIndex > now.SeasonIndex)
                {
                    month = 1;
                }
                else
                {
                    if (date.DayOfMonth >= now.DayOfMonth)
                    {
                        month = ModEntry.CurrentSeasonMonth;
                    }
                    else if (ModEntry.CurrentSeasonMonth < ModEntry.GetMonthsInCurrentSeason())
                    {
                        month = ModEntry.CurrentSeasonMonth + 1;
                    }
                    else
                    {
                        month = ModEntry.CurrentSeasonMonth;
                    }
                }
            }

            return GetTotalDays(date, month) - GetTotalDays(now, ModEntry.CurrentSeasonMonth);
        }
    }
}
