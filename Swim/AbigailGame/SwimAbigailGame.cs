using StardewModdingAPI.Utilities;
using StardewValley.Objects;
using StardewValley;
using Microsoft.Xna.Framework.Graphics;
using System;
using Microsoft.Xna.Framework;
using HarmonyLib;
using StardewModdingAPI;
using System.Collections.Generic;
using System.Linq;
using StardewValley.Monsters;

namespace Swim.AbigailGame
{
    public static class SwimAbigailGame
    {
        private const int gameTicks = 80000;

        public static readonly PerScreen<ulong> lastProjectile = new PerScreen<ulong>(() => 0);
        public static readonly PerScreen<int> abigailTicks = new PerScreen<int>();

        private static IModHelper SHelper;
        private static IMonitor SMonitor;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SHelper = helper;
            SMonitor = monitor;
        }

        public static void EnterAbigailGame(GameLocation location)
        {
            abigailTicks.Value = 0;
            location.characters.Clear();
            location.projectiles.Clear();

            Game1.player.changeOutOfSwimSuit();

            // Make the pllayer wear a cowboy hat as long as they have space in their inventory to store their current hat
            if (Game1.player.hat.Value is null)
            {
                Game1.player.hat.Value = new Hat("0");
            }
            else if (Game1.player.hat.Value.ItemId != "0")
            {
                if (Game1.player.couldInventoryAcceptThisItem(Game1.player.hat.Value))
                {
                    Game1.player.addItemToInventory(Game1.player.hat.Value);
                    Game1.player.hat.Value = new Hat("0");
                }
            }

            Game1.player.doEmote(9);
            RemoveExitPool(location);
        }

        public static void DrawHud(SpriteBatch b)
        {
            if (abigailTicks.Value > 0 && abigailTicks.Value < 30 * 5)
            {
                // The Prairie King infographic
                b.Draw(Game1.mouseCursors, new Vector2(Game1.viewport.Width, Game1.viewport.Height) / 2 - new Vector2(78, 31) / 2, new Rectangle?(new Rectangle(353, 1649, 78, 31)), new Color(255, 255, 255, abigailTicks.Value > 30 * 3 ? (int)Math.Round(255 * (abigailTicks.Value - 90) / 60f) : 255), 0f, Vector2.Zero, 3f, SpriteEffects.None, 0.99f);
            }
            if (abigailTicks.Value > 0)
            {
                SwimUtils.DrawProgressBar(b, Math.Max((gameTicks / 16) - abigailTicks.Value, 0), gameTicks / 16);
            }
        }

        public static void GameTick()
        {
            if(Game1.paused || Game1.freezeControls)
            {
                return;
            }

            GameLocation location = Game1.player.currentLocation;

            Game1.player.CurrentToolIndex = Game1.player.Items.Count;

            location.characters.RemoveWhere(c => c is Monster m && m.Health <= 0);

            if (abigailTicks.Value < 0)
            {
                return;
            }
            Game1.exitActiveMenu();

            if (abigailTicks.Value == 0)
            {
                AccessTools.Field(location.characters.GetType(), "OnValueRemoved").SetValue(location.characters, null);
            }

            if (Game1.player.millisecondsPlayed - lastProjectile.Value > 350)
            {
                TryAbigailShoot();
            }

            abigailTicks.Value++;
            if (abigailTicks.Value > gameTicks / 16f)
            {
                if (location.characters.Any(c => c is Monster))
                {
                    return;
                }

                abigailTicks.Value = -1;
                Game1.stopMusicTrack(StardewValley.GameData.MusicContext.Default);

                if (!Game1.player.mailReceived.Contains("ScubaFins"))
                {
                    Game1.playSound("Cowboy_Secret");
                    SwimMaps.AddScubaChest(location, new Vector2(8, 8), "ScubaFins");
                }

                AddExitPool(location);
            }
            else
            {
                if (Game1.random.NextDouble() < 0.03)
                {
                    int which = Game1.random.Next(3); // Which tile in the opening to spawn the enemy at
                    Point p;
                    switch (Game1.random.Next(4)) // Which opening to spawn the enemy at
                    {
                        case 0:
                            p = new Point(8 + which, 1);
                            break;
                        case 1:
                            p = new Point(1, 8 + which);
                            break;
                        case 2:
                            p = new Point(8 + which, 16);
                            break;
                        case 3:
                            p = new Point(16, 8 + which);
                            break;
                        default:
                            goto case 2;
                    }
                    location.characters.Add(new AbigailMetalHead(new Vector2(p.X * Game1.tileSize, p.Y * Game1.tileSize), 0));
                }

            }
        }

        private static void TryAbigailShoot()
        {
            bool up = false, down = false, left = false, right = false;

            if (Game1.options.gamepadControls)
            {
                // Right thumb stick and dpad
                up = SHelper.Input.IsDown(SButton.RightThumbstickUp) || SHelper.Input.IsDown(SButton.DPadUp);
                down = SHelper.Input.IsDown(SButton.RightThumbstickDown) || SHelper.Input.IsDown(SButton.DPadDown);
                left = SHelper.Input.IsDown(SButton.RightThumbstickLeft) || SHelper.Input.IsDown(SButton.DPadLeft);
                right = SHelper.Input.IsDown(SButton.RightThumbstickRight) || SHelper.Input.IsDown(SButton.DPadRight);
            }
            else
            {
                if (SHelper.Input.IsDown(SButton.MouseLeft))
                {
                    // Set the direction they're clicking in
                    float x = Game1.viewport.X + Game1.getOldMouseX() - Game1.player.position.X;
                    float y = Game1.viewport.Y + Game1.getOldMouseY() - Game1.player.position.Y;
                    float dx = Math.Abs(x);
                    float dy = Math.Abs(y);
                    bool vertical = dy > dx;
                    bool u = y <= 0;
                    bool r = x >= 0;
                    bool diagonal = vertical ? (dx > dy / 2) : (dy > dx / 2);

                    if (vertical)
                    {
                        up = u;
                        down = !u;

                        if (diagonal)
                        {
                            right = r;
                            left = !r;
                        }
                    }
                    else
                    {
                        right = r;
                        left = !r;

                        if (diagonal)
                        {
                            up = u;
                            down = !u;
                        }
                    }
                }
                else
                {
                    // Arrow keys
                    up = SHelper.Input.IsDown(SButton.Up);
                    down = SHelper.Input.IsDown(SButton.Down);
                    left = SHelper.Input.IsDown(SButton.Left);
                    right = SHelper.Input.IsDown(SButton.Right);

                    // If they are pressing the action button (like 'e'), set they way they're facing
                    if (Game1.isOneOfTheseKeysDown(Game1.input.GetKeyboardState(), Game1.options.actionButton))
                    {
                        switch (Game1.player.FacingDirection)
                        {
                            case Game1.up:
                                up = true;
                                break;
                            case Game1.down:
                                down = true;
                                break;
                            case Game1.left:
                                left = true;
                                break;
                            case Game1.right:
                                right = true;
                                break;
                        }
                    }
                }
            }

            Vector2 velocity = Vector2.Zero;

            if (up || down || left || right)
            {
                float magnitude = (up || down) && (left || right) ? (float)(1 / Math.Sqrt(2)) : 1;

                velocity.Y = up ? -magnitude : down ? magnitude : 0;
                velocity.X = right ? magnitude : left ? -magnitude : 0;

                Game1.player.currentLocation.projectiles.Add(new AbigailProjectile(1, 3, 0, 0, 0, velocity.X * 6, velocity.Y * 6, new Vector2(Game1.player.StandingPixel.X - 24, Game1.player.StandingPixel.Y - 48), "Cowboy_monsterDie", null, "Cowboy_gunshot", false, Game1.player.currentLocation, Game1.player, shotItemId: "(O)382"));
                lastProjectile.Value = Game1.player.millisecondsPlayed;
                Game1.player.faceDirection(SwimUtils.GetDirection(0, 0, velocity.X, velocity.Y));
            }
        }

        private static void AddExitPool(GameLocation location)
        {
            // Creates the pool
            location.setMapTile(8, 16, 91, "Buildings", "desert-new");
            location.setMapTile(9, 16, 92, "Buildings", "desert-new");
            location.setMapTile(10, 16, 93, "Buildings", "desert-new");
            location.setMapTile(8, 17, 107, "Buildings", "desert-new");
            location.removeTile(9, 17, "Buildings");
            location.setMapTile(10, 17, 109, "Buildings", "desert-new");
            location.setMapTile(8, 18, 139, "Buildings", "desert-new");
            location.setMapTile(9, 18, 140, "Buildings", "desert-new");
            location.setMapTile(10, 18, 141, "Buildings", "desert-new");
            for(int i = 8; i <= 10; i++)
            {
                for(int j = 16; j <= 18; j++)
                {
                    location.setMapTile(i, j, 108, "Back", "desert-new");
                    location.setTileProperty(i, j, "Back", "Water", "I"); // "I" property to disable the water effect because the desert tiles don't play nice with it.
                }
            }
            SwimMaps.ReloadWaterTiles(location);
        }

        private static void RemoveExitPool(GameLocation location)
        {
            for (int i = 8; i <= 10; i++)
            {
                for (int j = 16; j <= 18; j++)
                {
                    location.removeTileProperty(i, j, "Back", "Water");
                    location.removeMapTile(i, j, "Back");
                    location.removeMapTile(i, j, "Buildings");
                }
            }
            SwimMaps.ReloadWaterTiles(location);

            location.setMapTile(8, 16, 97, "Back", "desert-new");
            location.setMapTile(9, 16, 97, "Back", "desert-new");
            location.setMapTile(10, 16, 97, "Back", "desert-new");
            location.setMapTile(8, 17, 267, "Buildings", "desert-new");
            location.setMapTile(9, 17, 267, "Buildings", "desert-new");
            location.setMapTile(10, 17, 267, "Buildings", "desert-new");
        }
    }
}
