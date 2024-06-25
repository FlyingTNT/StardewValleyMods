
namespace LongerSeasons
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public bool ExtendBerry { get; set; } = true;
        public bool ExtendBirthdays { get; set; } = true;
        public bool AvoidBirthdayOverlaps { get; set; } = true;
        public int DaysPerMonth { get; set; } = 28;
        public int MonthsPerSeason { get; set; } = 1;
    }
}
