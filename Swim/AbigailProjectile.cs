﻿using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Projectiles;

namespace Swim
{
    public class AbigailProjectile : BasicProjectile
    {
        private string myCollisionSound;
        private bool myExplode;

        public AbigailProjectile(int damageToFarmer, int ParentSheetIndex, int bouncesTillDestruct, int tailLength, float rotationVelocity, float xVelocity, float yVelocity, Vector2 startingPosition, string collisionSound, string bounceSound, string firingSound, bool explode, GameLocation location = null, Character firer = null, onCollisionBehavior collisionBehavior = null, string shotItemId = null) : base(damageToFarmer, ParentSheetIndex, bouncesTillDestruct, tailLength, rotationVelocity, xVelocity, yVelocity, startingPosition, collisionSound, bounceSound, firingSound, explode, true, location, firer, collisionBehavior, shotItemId)
        {
            myCollisionSound = collisionSound;
            myExplode = explode;
        }
        public override void behaviorOnCollisionWithMonster(NPC n, GameLocation location)
        {
            explosionAnimation(location);
            if (n is Monster)
            {
                location.characters.Remove(n);
                location.projectiles.Remove(this);
                return;
            }
        }
        protected override void explosionAnimation(GameLocation location)
        {
            Rectangle sourceRect = GetSourceRect();
            int whichDebris = 12;
            
            if(itemId.Value != null)
            {
                sourceRect.X += 28;
                sourceRect.Y += 28;
                sourceRect.Width = 8;
                sourceRect.Height = 8;
                switch (itemId.Value)
                {
                    case "(O)378":
                        whichDebris = 0;
                        break;
                    case "(O)380":
                        whichDebris = 2;
                        break;
                    case "(O)382":
                        whichDebris = 4;
                        break;
                    case "(O)384":
                        whichDebris = 6;
                        break;
                    case "(O)386":
                        whichDebris = 10;
                        break;
                    case "(O)390":
                        whichDebris = 14;
                        break;
                }
            }
            
            if (itemId.Value != null)
            {
                Game1.createRadialDebris(location, whichDebris, (int)(position.X + 32f) / 64, (int)(position.Y + 32f) / 64, 6, false, -1, false);
            }
            else
            {
                Game1.createRadialDebris(location, "TileSheets\\Projectiles", sourceRect, 4, (int)position.X + 32, (int)position.Y + 32, 12, (int)(position.Y / 64f) + 1);
            }
            if (!string.IsNullOrEmpty(myCollisionSound))
            {
                location.playSound(myCollisionSound);
            }
            // The game only checks destroyMe when the DoDamage function is called, and this projectile bypasses that and just removes the enemies, so this has no effect.
            destroyMe = true;
        }
    }
}