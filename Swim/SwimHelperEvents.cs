using Common.Integrations;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile;
using xTile.Dimensions;
using xTile.Tiles;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Swim
{
    public class SwimHelperEvents
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;

        public static readonly PerScreen<bool> isJumping = new PerScreen<bool>(() => false);
        public static readonly PerScreen<Vector2> startJumpLoc = new PerScreen<Vector2>();
        public static readonly PerScreen<Vector2> endJumpLoc = new PerScreen<Vector2>();
        public static readonly PerScreen<ulong> lastJump = new PerScreen<ulong>(() => 0);
        public static readonly PerScreen<ulong> lastProjectile = new PerScreen<ulong>(() => 0);
        public static readonly PerScreen<int> abigailTicks = new PerScreen<int>();
        public static readonly PerScreen<int> ticksUnderwater = new PerScreen<int>(() => 0);
        public static readonly PerScreen<int> ticksWearingScubaGear = new PerScreen<int>(() => 0);
        public static readonly PerScreen<int> bubbleOffset = new PerScreen<int>(() => 0);
        private static readonly PerScreen<int> lastBreatheSound = new(() => 0);
        public static readonly SButton[] abigailShootButtons = new SButton[] {
            SButton.Left,
            SButton.Right,
            SButton.Up,
            SButton.Down
        };

        internal static Texture2D bubbleTexture => SHelper.GameContent.Load<Texture2D>("LooseSprites/temporary_sprites_1");

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        public static void Player_Warped(object sender, WarpedEventArgs e)
        {
            if(!e.IsLocalPlayer)
            {
                return;
            }

            if (e.NewLocation.Name == "Custom_ScubaAbigailCave")
            {
                abigailTicks.Value = 0;
                e.NewLocation.characters.Clear();


                Game1.player.changeOutOfSwimSuit();

                if(Game1.player.hat.Value is null)
                {
                    Game1.player.hat.Value = new Hat("0");
                }
                else if(Game1.player.hat.Value.ItemId != "0")
                {
                    if(Game1.player.couldInventoryAcceptThisItem(Game1.player.hat.Value))
                    {
                        Game1.player.addItemToInventory(Game1.player.hat.Value);
                        Game1.player.hat.Value = new Hat("0");
                    }
                }

                Game1.player.doEmote(9);
            }

            // We don't allow swimming in the night market, so we need to make sure the bridge is fixed so that they can't get softlocked on the right island.
            // This would happen if they swam in thru the river and then jumped out on that island
            if(e.NewLocation is BeachNightMarket market)
            {
                Beach.fixBridge(market);
            }

            ModEntry.locationIsPool.Value = false;
        }

        public static void Player_InventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            if (e.Player != Game1.player)
                return;

            if (!Game1.player.mailReceived.Contains("ScubaTank") && e.Added?.FirstOrDefault()?.ItemId == ModEntry.scubaTankID)
            {
                SMonitor.Log("Player found scuba tank");
                Game1.player.mailReceived.Add("ScubaTank");
            }
            if (!Game1.player.mailReceived.Contains("ScubaMask") && e.Added?.FirstOrDefault()?.ItemId == ModEntry.scubaMaskID)
            {
                SMonitor.Log("Player found scuba mask");
                Game1.player.mailReceived.Add("ScubaMask");
            }
            if (!Game1.player.mailReceived.Contains("ScubaFins") && e.Added?.FirstOrDefault()?.ItemId == ModEntry.scubaFinsID)
            {
                SMonitor.Log("Player found scuba fins");
                Game1.player.mailReceived.Add("ScubaFins");
            }
        }

        public static void GameLoop_Saving(object sender, SavingEventArgs e)
        {
            foreach (var l in Game1.locations)
            {
                l.characters.RemoveWhere(character => character is Fishie or BigFishie or SeaCrab or AbigailMetalHead);
            }
        }

        public static void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (!SwimUtils.IsWearingScubaGear() && Config.SwimSuitAlways && !Config.NoAutoSwimSuit)
            {
                Game1.player.changeIntoSwimsuit();
            }

            // load scuba gear ids
            if (DataLoader.Boots(Game1.content).ContainsKey("Swim_ScubaFins"))
            {
                SMonitor.Log($"Swim mod item #1 ID is {ModEntry.scubaFinsID}.");
                if (Game1.player.boots.Value != null && Game1.player.boots.Value.Name == "Scuba Fins" && Game1.player.boots.Value.ItemId != ModEntry.scubaFinsID)
                {
                    Game1.player.boots.Value = ItemRegistry.Create<Boots>(ModEntry.scubaFinsID);
                }
            }
            else
            {
                SMonitor.Log("Could not find scuba fins! Do you have the swim items content pack installed?", LogLevel.Warn);
            }

            if (DataLoader.Shirts(Game1.content).ContainsKey("Swim_ScubaTank"))
            {
                SMonitor.Log($"Swim mod item #2 ID is {ModEntry.scubaTankID}.");
                if (Game1.player.shirtItem.Value != null && Game1.player.shirtItem.Value.Name == "Scuba Tank" && Game1.player.shirtItem.Value.ItemId != ModEntry.scubaTankID)
                {
                    Game1.player.shirtItem.Value = ItemRegistry.Create<Clothing>(ModEntry.scubaTankID);
                }
            }
            else
            {
                SMonitor.Log("Could not find scuba tank! Do you have the swim items content pack installed?", LogLevel.Warn);
            }

            if (DataLoader.Hats(Game1.content).ContainsKey("Swim_ScubaMask"))
            {
                SMonitor.Log($"Swim mod item #3 ID is {ModEntry.scubaMaskID}.");
                if (Game1.player.hat.Value != null && Game1.player.hat.Value.Name == "Scuba Mask" && Game1.player.hat.Value.ItemId != ModEntry.scubaMaskID)
                {
                    Game1.player.hat.Value = ItemRegistry.Create<Hat>(ModEntry.scubaMaskID);
                }
            }
            else
            {
                SMonitor.Log("Could not find scuba mask! Do you have the swim items content pack installed?", LogLevel.Warn);
            }
        }

        public static void Display_RenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (ModEntry.isUnderwater.Value && SwimUtils.IsMapUnderwater(Game1.player.currentLocation.Name))
            {
                if ((ticksUnderwater.Value % 100 / Math.Min(100, Config.BubbleMult)) - bubbleOffset.Value == 0)
                {
                    Game1.playSound("tinyWhip");
                    ModEntry.bubbles.Value.Add(new Vector2(Game1.player.position.X + Game1.random.Next(-24, 25), Game1.player.position.Y - 96));
                    if (ModEntry.bubbles.Value.Count > 100)
                    {
                        ModEntry.bubbles.Value = ModEntry.bubbles.Value.Skip(1).ToList();
                    }
                    bubbleOffset.Value = Game1.random.Next(30 / Math.Min(100, Config.BubbleMult));
                }

                for (int k = 0; k < ModEntry.bubbles.Value.Count; k++)
                {
                    ModEntry.bubbles.Value[k] = new Vector2(ModEntry.bubbles.Value[k].X, ModEntry.bubbles.Value[k].Y - 2);
                }

                foreach (Vector2 v in ModEntry.bubbles.Value)
                {
                    e.SpriteBatch.Draw(bubbleTexture, v + new Vector2((float)Math.Sin(ticksUnderwater.Value / 20f) * 10f - Game1.viewport.X, -Game1.viewport.Y), new Rectangle?(new Rectangle(132, 20, 8, 8)), new Color(1, 1, 1, 0.5f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.001f);
                }
                ticksUnderwater.Value++;
            }
            else
            {
                ticksUnderwater.Value = 0;
            }
        }

        /// <summary>
        /// Drawing the oxygen bar as necessary. We do this in Rendering so that the base game's bars are drawn on top and this mod does not cover up their hover text.
        /// We also use high event priority to make sure that our bars are drawn under Survivalistic's so that we don't cover up their hover text.
        /// </summary>
        [EventPriority(EventPriority.High)] 
        public static void Display_RenderingHud(object sender, RenderingHudEventArgs args)
        {
            if (ModEntry.Oxygen < SwimUtils.MaxOxygen() && Config.ShowOxygenBar && Game1.displayHUD)
            {
                SwimUtils.DrawOxygenBar();
            }
        }

        public static void Display_RenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (Game1.player.currentLocation.Name == "Custom_ScubaAbigailCave")
            {
                if (abigailTicks.Value > 0 && abigailTicks.Value < 30 * 5)
                {
                    // The Prairie King infographic
                    e.SpriteBatch.Draw(Game1.mouseCursors, new Vector2(Game1.viewport.Width, Game1.viewport.Height) / 2 - new Vector2(78, 31) / 2, new Rectangle?(new Rectangle(353, 1649, 78, 31)), new Color(255, 255, 255, abigailTicks.Value > 30 * 3 ? (int)Math.Round(255 * (abigailTicks.Value - 90) / 60f) : 255), 0f, Vector2.Zero, 3f, SpriteEffects.None, 0.99f);
                }
                if (abigailTicks.Value > 0)
                {
                    SwimUtils.DrawProgressBar(e.SpriteBatch, Math.Max((80000 / 16) - abigailTicks.Value, 0), 80000 / 16);
                }
                return;
            }
        }

        public static void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var quickSaveApi = SHelper.ModRegistry.GetApi<IQuickSaveAPI>(IDs.QuickSave);
            if(quickSaveApi is not null)
            {
                quickSaveApi.SavingEvent += (o, _) => GameLoop_Saving(o, new SavingEventArgs());
            }

            // load dive maps

            foreach (IContentPack contentPack in SHelper.ContentPacks.GetOwned())
            {
                try
                {
                    SMonitor.Log($"Reading content pack: {contentPack.Manifest.Name} {contentPack.Manifest.Version} from {contentPack.DirectoryPath}");
                    DiveMapData data = contentPack.ReadJsonFile<DiveMapData>("content.json");
                    SwimUtils.ReadDiveMapData(data);
                }
                catch
                {
                    SMonitor.Log($"couldn't read content.json in content pack {contentPack.Manifest.Name}", LogLevel.Warn);
                }
            }

            SMonitor.Log($"Reading content pack from assets/swim-map-content.json");

            try
            {
                DiveMapData myData = SHelper.Data.ReadJsonFile<DiveMapData>("assets/swim-map-content.json");
                SwimUtils.ReadDiveMapData(myData);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"assets/swim-map-content.json file read error. Exception: {ex}", LogLevel.Warn);
            }
        }

        public static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            if (Game1.getLocationFromName("Custom_ScubaCave") != null && !Game1.player.mailReceived.Contains("ScubaMask"))
            {
                SwimMaps.AddScubaChest(Game1.getLocationFromName("Custom_ScubaCave"), new Vector2(10, 14), "ScubaMask");
            }
            ModEntry.marinerQuestionsWrongToday.Value = false;
            ModEntry.Oxygen = SwimUtils.MaxOxygen();

            if (!Context.IsMainPlayer)
            {
                return;
            }

            foreach (KeyValuePair<string, DiveMap> kvp in ModEntry.diveMaps)
            {
                GameLocation location = Game1.getLocationFromName(kvp.Key);
                if (location == null)
                {
                    SMonitor.Log($"GameLocation {kvp.Key} not found in day started loop");
                    continue;
                }
                if (kvp.Value.Features.Contains("OceanTreasure") || kvp.Value.Features.Contains("OceanResources") || kvp.Value.Features.Contains("Minerals"))
                {
                    SMonitor.Log($"Clearing overlay objects from GameLocation {location.Name} ");
                    location.overlayObjects.Clear();
                    location.objects.Clear();
                    location.numberOfSpawnedObjectsOnMap = 0;
                }
                if (kvp.Value.Features.Contains("OceanTreasure"))
                {
                    SMonitor.Log($"Adding ocean treasure to GameLocation {location.Name} ");
                    SwimMaps.AddOceanTreasure(location);
                }
                if (kvp.Value.Features.Contains("OceanResources"))
                {
                    SMonitor.Log($"Adding ocean forage to GameLocation {location.Name} ");
                    SwimMaps.AddOceanForage(location);
                }
                if(kvp.Value.Features.Contains("Forage"))
                {
                    SMonitor.Log($"Adding forage to GameLocation {location.Name} ");
                    SwimMaps.AddForage(location);
                }
                if (kvp.Value.Features.Contains("Minerals"))
                {
                    SMonitor.Log($"Adding minerals to GameLocation {location.Name} ");
                    SwimMaps.AddMinerals(location);
                }
                if(kvp.Value.Features.Contains("ArtifactSpots"))
                {
                    SMonitor.Log($"Adding artifact spots to GameLocation {location.Name} ");
                    SwimMaps.AddArtifactSpots(location);
                }
                if (kvp.Value.Features.Contains("SmolFishies") || kvp.Value.Features.Contains("BigFishies") || kvp.Value.Features.Contains("Crabs"))
                {
                    SMonitor.Log($"Clearing characters from GameLocation {location.Name} ");
                    location.characters.Clear();
                }
                if (kvp.Value.Features.Contains("SmolFishies"))
                {
                    SMonitor.Log($"Adding smol fishies to GameLocation {location.Name} ");
                    SwimMaps.AddFishies(location);
                }
                if (kvp.Value.Features.Contains("BigFishies"))
                {
                    SMonitor.Log($"Adding big fishies to GameLocation {location.Name} ");
                    SwimMaps.AddFishies(location, false);
                }
                if (kvp.Value.Features.Contains("Crabs"))
                {
                    SMonitor.Log($"Adding crabs to GameLocation {location.Name} ");
                    SwimMaps.AddCrabs(location);
                }
                if (kvp.Value.Features.Contains("WaterTiles"))
                {
                    SMonitor.Log($"Adding water tiles to GameLocation {location.Name} ");
                    SwimMaps.AddWaterTiles(location);
                }
                if (kvp.Value.Features.Contains("Underwater"))
                {
                    SMonitor.Log($"Removing water tiles from GameLocation {location.Name} ");
                    SwimMaps.RemoveWaterTiles(location);
                }
            }
        }

        public static void GameLoop_OneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs args)
        {
            if(Config.StaminaLossPerSecond == 0 || (Game1.player.timerSinceLastMovement > 500 )) // timerSinceLastMovement is ms, so it doesn't decrease stamina if they haven't moved in the last half second.
            {
                return;
            }

            if(Game1.player.currentLocation is BathHousePool || ModEntry.locationIsPool.Value)
            {
                return;
            }

            if(Game1.player.swimming.Value)
            {
                float staminaLoss = Config.StaminaLossPerSecond;
                if(Game1.player.running)
                {
                    staminaLoss *= 2;
                }
                if(SwimUtils.IsWearingScubaGear())
                {
                    staminaLoss *= Config.StaminaLossMultiplierWithGear;
                }

                Game1.player.stamina -= staminaLoss;
            }
        }

        public static void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Game1.player == null || Game1.player.currentLocation == null)
            {
                return;
            }

            if(!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
            {
                return;
            }

            if (Game1.activeClickableMenu != null && Game1.player.currentLocation.Name == "Custom_ScubaCrystalCave" && Game1.player.currentLocation.lastQuestionKey?.StartsWith("SwimMod_Mariner_") == true)
            {
                IClickableMenu menu = Game1.activeClickableMenu;
                if (menu == null || menu.GetType() != typeof(DialogueBox))
                    return;

                DialogueBox db = menu as DialogueBox;
                int resp = db.selectedResponse;
                List<Response> resps = db.responses.ToList();

                if (resp < 0 || resps == null || resp >= resps.Count || resps[resp] == null)
                    return;
                Game1.player.currentLocation.lastQuestionKey = "";

                SwimDialog.OldMarinerDialogue(resps[resp].responseKey);
                return;
            }
        }

        public static void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (Config.DiveKey.JustPressed() && Game1.activeClickableMenu == null && Context.IsPlayerFree && Context.CanPlayerMove && ModEntry.diveMaps.ContainsKey(Game1.player.currentLocation.Name) && ModEntry.diveMaps[Game1.player.currentLocation.Name].DiveLocations.Count > 0)
            {
                SMonitor.Log("Trying to dive!");
                Point pos = Game1.player.TilePoint;
                Location loc = new Location(pos.X, pos.Y);

                if (!SwimUtils.IsInWater())
                {
                    SMonitor.Log("Not in water");
                    return;
                }

                DiveMap dm = ModEntry.diveMaps[Game1.player.currentLocation.Name];
                DiveLocation diveLocation = null;
                foreach (DiveLocation dl in dm.DiveLocations)
                {
                    if (dl.GetRectangle().X == -1 || dl.GetRectangle().Contains(loc))
                    {
                        diveLocation = dl;
                        break;
                    }
                }

                if (diveLocation == null)
                {
                    SMonitor.Log($"No dive destination for this point on this map", LogLevel.Debug);
                    return;
                }

                if (Game1.getLocationFromName(diveLocation.OtherMapName) == null)
                {
                    SMonitor.Log($"Can't find destination map named {diveLocation.OtherMapName}", LogLevel.Warn);
                    return;
                }

                SMonitor.Log($"warping to {diveLocation.OtherMapName}", LogLevel.Debug);
                SwimUtils.DiveTo(diveLocation);
                return;
            }

            if (Config.SwimKey.JustPressed() && Game1.activeClickableMenu == null && (!Game1.player.swimming.Value || !Config.ReadyToSwim) && !isJumping.Value)
            {
                Config.ReadyToSwim = !Config.ReadyToSwim;
                SHelper.WriteConfig(Config);
                SMonitor.Log($"Ready to swim: {Config.ReadyToSwim}");
                return;
            }

            if (Config.SwimSuitKey.JustPressed() && Game1.activeClickableMenu == null)
            {
                Config.SwimSuitAlways = !Config.SwimSuitAlways;
                SHelper.WriteConfig(Config);
                if (!Game1.player.swimming.Value)
                {
                    if (!Config.SwimSuitAlways)
                        Game1.player.changeOutOfSwimSuit();
                    else
                        Game1.player.changeIntoSwimsuit();
                }
                return;
            }
        }

        public static void Content_LocaleChanged(object sender, LocaleChangedEventArgs args)
        {
            SHelper.GameContent.InvalidateCache("Mods/FlyingTNT.Swim/i18n");
        }

        public static void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (Game1.player.currentLocation is null || Game1.player is null || !Game1.displayFarmer || Game1.player.position is null)
                return;

            ModEntry.isUnderwater.Value = SwimUtils.IsMapUnderwater(Game1.player.currentLocation.Name);

            if (Game1.player.currentLocation.Name == "Custom_ScubaAbigailCave")
            {
                AbigailCaveTick();
            }

            if(e.IsMultipleOf(4))
            {
                AnimationManager.SwimShadowFrame++;
                AnimationManager.SwimShadowFrame %= 10;
            }

            SwimUtils.updateOxygenValue();

            if (SwimUtils.IsWearingScubaGear())
            {
                ticksWearingScubaGear.Value++;
                if (Config.BreatheSound && (lastBreatheSound.Value == 0 || ticksWearingScubaGear.Value - lastBreatheSound.Value > 6000 / 16))
                {
                    SMonitor.Log("Playing breathe sound");
                    lastBreatheSound.Value = ticksWearingScubaGear.Value;

                    SoundEffect breatheEffect = SwimUtils.LoadBreatheSound();

                    if(breatheEffect is null)
                    {
                        SMonitor.Log("Could not load breathe sound!");
                    }
                    else
                    {
                        breatheEffect.Play(0.5f * Game1.options.soundVolumeLevel, 0f, 0f);
                        breatheEffect.Dispose();
                    }
                }
            }
            else
            {
                lastBreatheSound.Value = 0;
                ticksWearingScubaGear.Value = 0;
            }

            if (isJumping.Value)
            {
                float difx = endJumpLoc.Value.X - startJumpLoc.Value.X;
                float dify = endJumpLoc.Value.Y - startJumpLoc.Value.Y;
                float completed = Game1.player.freezePause / (float)Config.JumpTimeInMilliseconds;
                if (Game1.player.freezePause <= 0)
                {
                    Game1.player.position.Value = endJumpLoc.Value;
                    isJumping.Value = false;
                    if (ModEntry.willSwim.Value)
                    {
                        Game1.player.currentLocation.playSound("waterSlosh");
                        Game1.player.swimming.Value = true;
                    }
                    else
                    {
                        if (!Config.SwimSuitAlways)
                        {
                            Game1.player.changeOutOfSwimSuit();
                        }
                    }
                    return;
                }
                Game1.player.position.Value = new Vector2(endJumpLoc.Value.X - (difx * completed), endJumpLoc.Value.Y - (dify * completed) - (float)Math.Sin(completed * Math.PI) * 64);
                return;
            }

            if (!SwimUtils.CanSwimHere())
            {
                return;
            }

            if (!Context.IsPlayerFree)
            {
                return;
            }

            if (Game1.player.swimming.Value && tryToWarp()) // Returns true if it is warping
                return;

            if (Game1.player.swimming.Value && !SwimUtils.IsInWater() && !isJumping.Value)
            {
                SMonitor.Log("Swimming out of water");

                Vector2 baseSpot = new(MathF.Round(Game1.player.Tile.X, MidpointRounding.AwayFromZero), MathF.Round(Game1.player.Tile.Y, MidpointRounding.AwayFromZero));

                // Jump to the current tile if valid
                if(SwimUtils.IsValidJumpLocation(baseSpot))
                {
                    //SMonitor.Log("Base");
                    DoJump(baseSpot);
                    return;
                }

                // Jump forwards if valid
                if(TryToJumpInDirection(baseSpot, Game1.player.FacingDirection, true))
                {
                    //SMonitor.Log("Front");
                    return;
                }

                // The direction to the right/left of the facing direction
                int rightDirection = (Game1.player.FacingDirection + 1) % 4;
                int leftDirection = (Game1.player.FacingDirection - 1) % 4;
                if(leftDirection < 0)
                {
                    leftDirection += 4;
                }

                // Try to jump diagonally to the right
                if (TryToJumpInDirection(Game1.player.Tile + Utility.DirectionsTileVectors[rightDirection], Game1.player.FacingDirection, true))
                {
                    //SMonitor.Log("Diag Right");
                    return;
                }

                // Try to jump diagonally to the left
                if (TryToJumpInDirection(Game1.player.Tile + Utility.DirectionsTileVectors[leftDirection], Game1.player.FacingDirection, true))
                {
                    //SMonitor.Log("Diag Left");
                    return;
                }

                // If the player isn't moving, just jump to the current tile. Could allow clipping out of bounds.
                if (Game1.player.movementDirections.Count == 0)
                {
                    //SMonitor.Log("Unmoving");
                    DoJump(baseSpot);
                    return;
                }

                // If they're moving right, try to jump to the right
                if (Game1.player.movementDirections.Contains(rightDirection))
                {
                    if (TryToJumpInDirection(baseSpot, rightDirection, true))
                    {
                        //SMonitor.Log("Right");
                        return;
                    }
                }

                // If they're moving left, try to jump to the left
                if (Game1.player.movementDirections.Contains(leftDirection))
                {
                    if (TryToJumpInDirection(baseSpot, leftDirection, true))
                    {
                        //SMonitor.Log("Left");
                        return;
                    }
                }

                // If we couldn't find a valid jump location, just jump to the current tile. This does not perform collision checks, so could allow clipping out of bounds.
                //SMonitor.Log("I give up");
                DoJump(baseSpot);
            }

            // I'm commenting out this check because it's causing problems with custom maps, and my thinking is that there's basically no way that this mod would cause the player to be in
            // the water while they should be swimming; that would be the fault of the map makers. However, with the on land and swimming check, this mod does cause that quite a bit.
            /*
            if (!Game1.player.swimming.Value && SwimUtils.IsInWater() && !isJumping.Value)
            {
                SMonitor.Log("In water not swimming");

                ModEntry.willSwim.Value = true;
                Game1.player.freezePause = Config.JumpTimeInMilliseconds;
                Game1.player.currentLocation.playSound("dwop");
                isJumping.Value = true;
                startJumpLoc.Value = Game1.player.position.Value;
                endJumpLoc.Value = Game1.player.position.Value;


                Game1.player.swimming.Value = true;
                if (!Game1.player.bathingClothes.Value && !SwimUtils.IsWearingScubaGear() && !Config.NoAutoSwimSuit)
                    Game1.player.changeIntoSwimsuit();
            }*/

            if (Game1.player.swimming.Value)
            {
                if (SwimUtils.IsWearingScubaGear() && !Config.SwimSuitAlways && SwimUtils.IsMapUnderwater(Game1.currentLocation.Name))
                {
                    if(Game1.player.bathingClothes.Value)
                    {
                        Game1.player.changeOutOfSwimSuit();
                    }
                }
                else if (!Game1.player.bathingClothes.Value && !Config.NoAutoSwimSuit)
                    Game1.player.changeIntoSwimsuit();


                if (Game1.player.boots.Value != null && Game1.player.boots.Value.ItemId == ModEntry.scubaFinsID)
                {
                    string buffId = "Swim_ScubaFinsSpeed";
                    Buff buff = Game1.player.buffs.AppliedBuffs.Values.FirstOrDefault((Buff p) => p.Equals(buffId));
                    if (buff == null)
                    {
                        buff = new Buff(buffId, "Scuba Fins", SHelper.Translation.Get("scuba-fins"), 50, Game1.content.Load<Texture2D>("TileSheets/BuffsIcons"), 9, new BuffEffects() {Speed = {Config.ScubaFinSpeed}});

                        Game1.player.applyBuff(buff);
                    }
                    else
                    {
                        buff.millisecondsDuration = 50;
                    }
                }
            }

            if(!SwimUtils.isSafeToTryJump())
            {
                return;
            }

            int direction = Game1.player.FacingDirection;

            bool didJump = TryToJumpInDirection(Game1.player.Tile, direction); // Try to jump in the direction the player is facing

            // If we didn't just jump, 
            if (!didJump && Config.ManualJumpButton.IsDown() && SwimUtils.isMouseButtonDown(Config.ManualJumpButton) && Config.EnableClickToSwim)
            {
                try
                {
                    int xTile = (int)Math.Round((Game1.viewport.X + Game1.getOldMouseX()) / 64f);
                    int yTile = (int)Math.Round((Game1.viewport.Y + Game1.getOldMouseY()) / 64f);
                    //Monitor.Log($"Click tile: ({xTile}, {yTile}), Player tile: ({Game1.player.TilePoint.X}, {Game1.player.TilePoint.Y})");
                    bool clickTileIsWater = Game1.player.currentLocation.isTileOnMap(xTile, yTile) && Game1.player.currentLocation.waterTiles is not null && Game1.player.currentLocation.waterTiles[xTile, yTile];
                    bool isClickingOnOppositeTerrain = clickTileIsWater != Game1.player.swimming.Value;
                    if (isClickingOnOppositeTerrain || !Config.MustClickOnOppositeTerrain)
                    {
                        // Set the direction to the direction of the cursor relative to the player.
                        direction = SwimUtils.GetDirection(Game1.player.TilePoint.X, Game1.player.TilePoint.Y, xTile, yTile);

                        if(direction != Game1.player.FacingDirection)
                        {
                            TryToJumpInDirection(Game1.player.Tile, direction);
                        }
                    }
                    else // If the player is pressing the manual swim button, manual swim is on, they are not clicking on the opposite terrain, and mustClickOnOppositeTerrain is true
                    {
                        return;
                    }
                }
                catch
                {
                    // Assiming this happens when the game can't get the mouse position
                    SMonitor.Log("Error in manual direction calculation!");
                }
            }
        }

        public static bool TryToJumpInDirection(Vector2 startingLocation, int direction, bool onlyOneTile = false) // Returns whether or not it is jumping
        {
            int maxDistance;
            Vector2 directionPoint;
            Vector2 jumpLocation = Vector2.Zero;

            switch (direction)
            {
                case 0:
                    maxDistance = (int)Math.Round(Config.TriggerDistanceUp * Config.TriggerDistanceMult);
                    directionPoint = new(0, -1);
                    break;
                case 2:
                    maxDistance = (int)Math.Round(Config.TriggerDistanceDown * Config.TriggerDistanceMult);
                    directionPoint = new(0, 1);
                    break;
                case 1:
                    maxDistance = (int)Math.Round(Config.TriggerDistanceRight * Config.TriggerDistanceMult);
                    directionPoint = new(1, 0);
                    break;
                case 3:
                    maxDistance = (int)Math.Round(Config.TriggerDistanceLeft * Config.TriggerDistanceMult);
                    directionPoint = new(-1, 0);
                    break;
                default:
                    goto case 2;
            }

            Vector2 location = startingLocation + directionPoint;

            do
            {
                if(SwimUtils.IsValidJumpLocation(location))
                {
                    jumpLocation = location;
                    break;
                }

                location += directionPoint;
            }
            while (Math.Abs(location.X - startingLocation.X + location.Y - startingLocation.Y) * Game1.tileSize <= maxDistance && !onlyOneTile);

            //Monitor.Value.Log($"next passable {Game1.player.currentLocation.isTilePassable(new Location((int)tiles.Last().X, (int)tiles.Last().Y), Game1.viewport)} next to land: {nextToLand}, next to water: {nextToWater}");

            if (jumpLocation != Vector2.Zero)
            {
                DoJump(jumpLocation, direction);
                return true;
            }

            return false;
        }

        private static void DoJump(Vector2 toLocation, int direction = -1)
        {
            if(direction != -1)
            {
                Game1.player.faceDirection(direction);
            }
            lastJump.Value = Game1.player.millisecondsPlayed;
            //Monitor.Value.Log("got swim location");
            if (Game1.player.swimming.Value)
            {
                ModEntry.willSwim.Value = false;
                Game1.player.swimming.Value = false;
                Game1.player.freezePause = Config.JumpTimeInMilliseconds;
                Game1.player.currentLocation.playSound("dwop");
                Game1.player.currentLocation.playSound("waterSlosh");
            }
            else
            {
                ModEntry.willSwim.Value = true;
                if (!SwimUtils.IsWearingScubaGear() && !Config.NoAutoSwimSuit)
                    Game1.player.changeIntoSwimsuit();

                Game1.player.freezePause = Config.JumpTimeInMilliseconds;
                Game1.player.currentLocation.playSound("dwop");
            }
            isJumping.Value = true;
            startJumpLoc.Value = Game1.player.position.Value;
            endJumpLoc.Value = new Vector2(toLocation.X * Game1.tileSize, toLocation.Y * Game1.tileSize);
        }

        public static void AbigailCaveTick()
        {
            GameLocation location = Game1.player.currentLocation;

            Game1.player.CurrentToolIndex = Game1.player.Items.Count;

            List<NPC> list = location.characters.ToList().FindAll((n) => (n is Monster) && (n as Monster).Health <= 0);
            foreach (NPC n in list)
            {
                location.characters.Remove(n);
            }

            if (abigailTicks.Value < 0)
            {
                return;
            }
            Game1.exitActiveMenu();

            if (abigailTicks.Value == 0)
            {
                AccessTools.Field(location.characters.GetType(), "OnValueRemoved").SetValue(location.characters, null);
            }

            Vector2 v = Vector2.Zero;
            float yrt = (float)(1 / Math.Sqrt(2));
            if (SHelper.Input.IsDown(SButton.Up) || SHelper.Input.IsDown(SButton.RightThumbstickUp))
            {
                if (SHelper.Input.IsDown(SButton.Right) || SHelper.Input.IsDown(SButton.RightThumbstickRight))
                    v = new Vector2(yrt, -yrt);
                else if (SHelper.Input.IsDown(SButton.Left) || SHelper.Input.IsDown(SButton.RightThumbstickLeft))
                    v = new Vector2(-yrt, -yrt);
                else
                    v = new Vector2(0, -1);
            }
            else if (SHelper.Input.IsDown(SButton.Down) || SHelper.Input.IsDown(SButton.RightThumbstickDown))
            {
                if (SHelper.Input.IsDown(SButton.Right) || SHelper.Input.IsDown(SButton.RightThumbstickRight))
                    v = new Vector2(yrt, yrt);
                else if (SHelper.Input.IsDown(SButton.Left) || SHelper.Input.IsDown(SButton.RightThumbstickLeft))
                    v = new Vector2(-yrt, yrt);
                else
                    v = new Vector2(0, 1);
            }
            else if (SHelper.Input.IsDown(SButton.Right) || SHelper.Input.IsDown(SButton.RightThumbstickDown))
                v = new Vector2(1, 0);
            else if (SHelper.Input.IsDown(SButton.Left) || SHelper.Input.IsDown(SButton.RightThumbstickLeft))
                v = new Vector2(-1, 0);
            else if (SHelper.Input.IsDown(SButton.MouseLeft))
            {
                float x = Game1.viewport.X + Game1.getOldMouseX() - Game1.player.position.X;
                float y = Game1.viewport.Y + Game1.getOldMouseY() - Game1.player.position.Y;
                float dx = Math.Abs(x);
                float dy = Math.Abs(y);
                if (y < 0)
                {
                    if (x > 0)
                    {
                        if (dy > dx)
                        {
                            if (dy - dx > dy / 2)
                                v = new Vector2(0, -1);
                            else
                                v = new Vector2(yrt, -yrt);

                        }
                        else
                        {
                            if (dx - dy > x / 2)
                                v = new Vector2(1, 0);
                            else
                                v = new Vector2(yrt, -yrt);
                        }
                    }
                    else
                    {
                        if (dy > dx)
                        {
                            if (dy - dx > dy / 2)
                                v = new Vector2(0, -1);
                            else
                                v = new Vector2(-yrt, -yrt);

                        }
                        else
                        {
                            if (dx - dy > x / 2)
                                v = new Vector2(-1, 0);
                            else
                                v = new Vector2(-yrt, -yrt);
                        }
                    }
                }
                else
                {
                    if (x > 0)
                    {
                        if (dy > dx)
                        {
                            if (dy - dx > dy / 2)
                                v = new Vector2(0, 1);
                            else
                                v = new Vector2(yrt, yrt);

                        }
                        else
                        {
                            if (dx - dy > x / 2)
                                v = new Vector2(1, 0);
                            else
                                v = new Vector2(yrt, yrt);
                        }
                    }
                    else
                    {
                        if (dy > dx)
                        {
                            if (dy - dx > dy / 2)
                                v = new Vector2(0, -1);
                            else
                                v = new Vector2(-yrt, yrt);

                        }
                        else
                        {
                            if (dx - dy > x / 2)
                                v = new Vector2(-1, 0);
                            else
                                v = new Vector2(-yrt, yrt);
                        }
                    }
                }
            }

            if (v != Vector2.Zero && Game1.player.millisecondsPlayed - lastProjectile.Value > 350)
            {
                location.projectiles.Add(new AbigailProjectile(1, 3, 0, 0, 0, v.X * 6, v.Y * 6, new Vector2(Game1.player.StandingPixel.X - 24, Game1.player.StandingPixel.Y - 48), "Cowboy_monsterDie", null, "Cowboy_gunshot", false, true, location, Game1.player, shotItemId: "(O)382"));
                lastProjectile.Value = Game1.player.millisecondsPlayed;
                Game1.player.faceDirection(SwimUtils.GetDirection(0, 0, v.X, v.Y));
            }

            foreach (SButton button in abigailShootButtons)
            {
                if (SHelper.Input.IsDown(button))
                {
                    switch (button)
                    {
                        case SButton.Up:
                            break;
                        case SButton.Right:
                            v = new Vector2(1, 0);
                            break;
                        case SButton.Down:
                            v = new Vector2(0, 1);
                            break;
                        default:
                            v = new Vector2(-1, 0);
                            break;
                    }
                }
            }

            abigailTicks.Value++;
            if (abigailTicks.Value > 80000 / 16f)
            {
                if (location.characters.ToList().FindAll((n) => (n is Monster)).Count > 0)
                    return;

                abigailTicks.Value = -1;
                Game1.player.hat.Value = null;
                Game1.stopMusicTrack(StardewValley.GameData.MusicContext.Default);

                if (!Game1.player.mailReceived.Contains("ScubaFins"))
                {
                    Game1.playSound("Cowboy_Secret");
                    SwimMaps.AddScubaChest(location, new Vector2(8, 8), "ScubaFins");
                }
                
                location.setMapTile(8, 16, 91, "Buildings", "desert-new");
                location.setMapTile(9, 16, 92, "Buildings", "desert-new");
                location.setTileProperty(9, 16, "Back", "Water", "T");
                location.setMapTile(10, 16, 93, "Buildings", "desert-new");
                location.setMapTile(8, 17, 107, "Buildings", "desert-new");
                location.setMapTile(9, 17, 108, "Back", "desert-new");
                location.setTileProperty(9, 17, "Back", "Water", "T");
                location.removeTile(9, 17, "Buildings");
                location.setMapTile(10, 17, 109, "Buildings", "desert-new");
                location.setMapTile(8, 18, 139, "Buildings", "desert-new");
                location.setMapTile(9, 18, 140, "Buildings", "desert-new");
                location.setMapTile(10, 18, 141, "Buildings", "desert-new");
                SwimMaps.AddWaterTiles(location);
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

        public static bool tryToWarp()
        {
            DiveMap dm = null;
            Point edgePos = Game1.player.TilePoint;

            string locationName = Game1.player.currentLocation.Name == "BeachNightMarket" ? "Beach" : Game1.player.currentLocation.Name;

            if (ModEntry.diveMaps.ContainsKey(locationName))
            {
                dm = ModEntry.diveMaps[locationName];
            }
            else
            {
                return false;
            }

            if (Game1.player.position.Y > Game1.viewport.Y + Game1.viewport.Height - 32)
            {
                Game1.player.position.Value = new Vector2(Game1.player.position.X, Game1.viewport.Y + Game1.viewport.Height - 33);
                if (dm != null)
                {
                    SMonitor.Log($"Trying to warp from ({edgePos.X}, {edgePos.Y})");
                    EdgeWarp edge = dm.EdgeWarps.Find((x) => x.ThisMapEdge == "Bottom" && x.FirstTile <= edgePos.X && x.LastTile >= edgePos.X);
                    if (edge != null)
                    {
                        Point pos = SwimUtils.GetEdgeWarpDestination(edgePos.X, edge);
                        if (pos != Point.Zero)
                        {
                            SMonitor.Log("warping south");
                            Game1.warpFarmer(edge.OtherMapName, pos.X, pos.Y, false);
                            return true;
                        }
                    }
                }
            }
            else if (Game1.player.position.Y < Game1.viewport.Y - 16)
            {
                Game1.player.position.Value = new Vector2(Game1.player.position.X, Game1.viewport.Y - 15);

                if (dm != null)
                {
                    SMonitor.Log($"Trying to warp from ({edgePos.X}, {edgePos.Y})");
                    EdgeWarp edge = dm.EdgeWarps.Find((x) => x.ThisMapEdge == "Top" && x.FirstTile <= edgePos.X && x.LastTile >= edgePos.X);
                    if (edge != null)
                    {
                        Point pos = SwimUtils.GetEdgeWarpDestination(edgePos.X, edge);
                        if (pos != Point.Zero)
                        {
                            SMonitor.Log("warping north");
                            Game1.warpFarmer(edge.OtherMapName, pos.X, pos.Y, false);
                            return true;
                        }
                    }
                }
            }
            else if (Game1.player.position.X > Game1.viewport.X + Game1.viewport.Width - 48)
            {
                Game1.player.position.Value = new Vector2(Game1.viewport.X + Game1.viewport.Width - 49, Game1.player.position.Y);

                if (dm != null)
                {
                    SMonitor.Log($"Trying to warp from ({edgePos.X}, {edgePos.Y})");
                    EdgeWarp edge = dm.EdgeWarps.Find((x) => x.ThisMapEdge == "Right" && x.FirstTile <= edgePos.Y && x.LastTile >= edgePos.Y);
                    if (edge != null)
                    {
                        Point pos = SwimUtils.GetEdgeWarpDestination(edgePos.Y, edge);
                        if (pos != Point.Zero)
                        {
                            SMonitor.Log("warping east");
                            Game1.warpFarmer(edge.OtherMapName, pos.X, pos.Y, false);
                            return true;
                        }
                    }
                }
            }
            else if (Game1.player.position.X < Game1.viewport.X - 32)
            {
                Game1.player.position.Value = new Vector2(Game1.viewport.X - 31, Game1.player.position.Y);

                if (dm != null)
                {
                    SMonitor.Log($"Trying to warp from ({edgePos.X}, {edgePos.Y})");
                    EdgeWarp edge = dm.EdgeWarps.Find((x) => x.ThisMapEdge == "Left" && x.FirstTile <= edgePos.Y && x.LastTile >= edgePos.Y);
                    if (edge != null)
                    {
                        Point pos = SwimUtils.GetEdgeWarpDestination(edgePos.Y, edge);
                        if (pos != Point.Zero)
                        {
                            SMonitor.Log("warping west");
                            Game1.warpFarmer(edge.OtherMapName, pos.X, pos.Y, false);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
