
namespace LongerSeasons
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public bool ExtendBerry { get; set; } = true;
        public bool ExtendBirthdays { get; set; } = true;
        public bool AvoidBirthdayOverlaps { get; set; } = true;
        public int DaysPerMonth { get; set; } = 28;
        public int MonthsPerSpring { get; set; } = 1;
        public int MonthsPerSummer { get; set; } = 1;
        public int MonthsPerFall { get; set; } = 1;
        public int MonthsPerWinter { get; set; } = 1;

    }
}
