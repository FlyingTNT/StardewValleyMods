
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
        public bool UseOldDateCalculations { get; set; } = false;
        public bool GetNumbersFromBillboard { get; set; } = false;
        public int BillboardNumberWidth { get; set; } = 7;
        public int BillboardNumberHeight { get; set; } = 11;
        public int[] BillboardNumberOffestsX { get; set; } = new int[] {-1, 0, 0, 1, 0, -1, 0, 0, 0, 1};
        public int[] BillboardNumberOffestsY { get; set; } = new int[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0};

    }
}
