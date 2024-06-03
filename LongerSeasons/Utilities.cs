using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LongerSeasons
{
    internal class Utilities
    {
        private static ModConfig Config;
        public static readonly Season[] SeasonsByIndex = new Season[]{Season.Spring, Season.Summer, Season.Fall, Season.Winter};
        public static void Initialize(ModConfig config)
        {
            Config = config;
        }

        public static int GetDaysPerMonth()
        {
            return Config.DaysPerMonth;
        }
    }
}
