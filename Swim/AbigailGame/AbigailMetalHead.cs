using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using System.Collections.Generic;

namespace Swim.AbigailGame
{
    public class AbigailMetalHead : MetalHead
    {
        public AbigailMetalHead() : base()
        {
        }

        public AbigailMetalHead(Vector2 position, int mineArea) : base(position, mineArea)
        {
            DamageToFarmer = 100000;
            Health = 1;
            moveTowardPlayerThreshold.Value = 50;
            objectsToDrop.Clear();
        }

        public override void shedChunks(int number, float scale)
        {

        }
        public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb, double addedPrecision, Farmer who)
        {
            objectsToDrop.Clear();
            if (currentLocation.characters.Contains(this))
            {
                currentLocation.characters.Remove(this);
            }
            return 1000;
        }
        public override List<Item> getExtraDropItems()
        {
            return new List<Item>();
        }

        public override void Removed()
        {
        }
        public override void onDealContactDamage(Farmer who)
        {
            Game1.playSound("cowboy_dead");
            SwimAbigailGame.abigailTicks.Value = -1;
        }
    }
}
