
using System.Collections.Generic;

namespace MultiStoryFarmhouse
{
    public class ModConfig
    {
        public bool EnableMod { get; set; } = true;
        public string FloorNames { get; set; } = "ManyRooms, EmptyHall";
        public bool CombineWithCellarStairs { get; set; } = true;
        public int MainFloorStairsX { get; set; } = 21;
        public int MainFloorStairsY { get; set; } = 32;
    }
}
