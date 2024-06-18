using xTile.ObjectModel;
using xTile;

namespace MultipleFloorFarmhouse
{
    internal class Utilities
    {
        public static void AddWarp(Map map, string targetLocation, int thisX, int thisY, int targetX, int targetY)
        {
            // The Warp property should be a series of items in this format, separated by spaces
            string warpString = $"{thisX} {thisY} {targetLocation} {targetX} {targetY}";
            
            if (map.Properties.TryGetValue("Warp", out PropertyValue property))
            {
                map.Properties["Warp"] = property + " " + warpString;
                return;
            }

            map.Properties["Warp"] = warpString;
            return;
        }

        public static void ClearWarps(Map map)
        {
            if(map.Properties.ContainsKey("Warp"))
            {
                map.Properties.Remove("Warp");
            }
        }
    }
}
