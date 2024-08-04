using StardewValley;
using StardewModdingAPI;
using StardewValley.Locations;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace FreeLove
{
    public class PathFindControllerPatches
    {
        private static IMonitor Monitor;
        private static ModConfig Config;
        private static IModHelper Helper;
        public static IKissingAPI kissingAPI;

        // call this method from your Entry class
        public static void Initialize(IMonitor monitor, ModConfig config, IModHelper helper)
        {
            Monitor = monitor;
            Config = config;
            Helper = helper;
        }
        public static void PathFindController_Prefix(Character c, GameLocation location, ref Point endPoint, ref int finalFacingDirection)
        {
            try
            {
                if (!Config.EnableMod || c is not NPC npc || !npc.IsVillager || !npc.isMarried() || location is not FarmHouse house || endPoint == house.getEntryLocation())
                    return;

                if (ModEntry.IsInBed(house, new Rectangle(endPoint.X * 64, endPoint.Y * 64, 64, 64)))
                {
                    Point point = ModEntry.GetSpouseBedEndPoint(house, c.Name);
                    if(point.X < 0 || point.Y < 0)
                    {
                        Monitor.Log($"Error setting bed endpoint for {c.Name}", LogLevel.Warn);
                    }
                    else
                    {
                        endPoint = point;
                        Monitor.Log($"Moved {c.Name} bed endpoint to {endPoint}");
                    }
                }
                else if (IsColliding(c, location, endPoint))
                {
                    var pointDirection = ModEntry.GetRandomGoodSpotInFarmhouse(house);
                    if(pointDirection.Spot != Vector2.Zero)
                    {
                        endPoint = pointDirection.Spot.ToPoint();
                        finalFacingDirection = pointDirection.Direction;
                        Monitor.Log($"Moved {c.Name} endpoint to random point {endPoint}");
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed in {nameof(PathFindController_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        private static bool IsColliding(Character c, GameLocation location, Point endPoint)
        {
            Monitor.Log($"Checking {c.Name} endpoint in farmhouse");

            foreach(Character character in location.characters)
            {
                if (character != c)
                {
                    if (character.TilePoint == endPoint || (character is NPC && (character as NPC).controller?.endPoint == endPoint))
                    {
                        Monitor.Log($"{c.Name} endpoint {endPoint} collides with {character.Name}");
                        return true;
                    }
                }
            }

            return false;
        }
    }
}