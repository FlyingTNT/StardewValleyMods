using StardewValley;

namespace LongerSeasons
{
    internal class Utilities
    {
        public static readonly Season[] SeasonsByIndex = new Season[]{Season.Spring, Season.Summer, Season.Fall, Season.Winter};

        public static int GetDaysPerMonth()
        {
            return ModEntry.Config.DaysPerMonth;
        }
    }
}
