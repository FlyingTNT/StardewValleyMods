﻿using Common.Integrations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using Swim.AbigailGame;
using Swim.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // Jumping variables
        public static readonly PerScreen<bool> isJumping = new PerScreen<bool>(() => false);
        public static readonly PerScreen<bool> willSwim = new PerScreen<bool>(() => false);
        public static readonly PerScreen<Vector2> startJumpLoc = new PerScreen<Vector2>();
        public static readonly PerScreen<Vector2> endJumpLoc = new PerScreen<Vector2>();
        public static readonly PerScreen<ulong> lastJump = new PerScreen<ulong>(() => 0);

        public static readonly PerScreen<int> ticksUnderwater = new PerScreen<int>(() => 0);
        public static readonly PerScreen<int> ticksWearingScubaGear = new PerScreen<int>(() => 0);

        // Misc variables
        internal static Texture2D bubbleTexture => SHelper.GameContent.Load<Texture2D>("LooseSprites/temporary_sprites_1");
        public static readonly PerScreen<int> bubbleOffset = new PerScreen<int>(() => 0);
        public static readonly PerScreen<List<Vector2>> bubbles = new PerScreen<List<Vector2>>(() => new List<Vector2>());
        private static readonly PerScreen<int> lastBreatheSound = new(() => 0);

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
                SwimAbigailGame.EnterAbigailGame(e.NewLocation);
            }

            // We don't allow swimming in the night market, so we need to make sure the bridge is fixed so that they can't get softlocked on the right island.
            // This would happen if they swam in thru the river and then jumped out on that island
            if(e.NewLocation is BeachNightMarket market)
            {
                Beach.fixBridge(market);
            }

            ModEntry.locationIsPool.Value = false;
            if(e.NewLocation is BathHousePool)
            {
                ModEntry.locationIsPool.Value = true;
            }

            ModEntry.locationProhibitsSwimming.Value = e.NewLocation?.Map?.Properties?.ContainsKey(ModEntry.ProhibitSwimmingMapPropertyKey) ?? false;
            if(ModEntry.locationProhibitsSwimming.Value)
            {
                SMonitor.Log($"{e.NewLocation.Name} is prohibiting swimming.");
            }

            // Maybe implement this in the future. This scenario would only not be caught by the swimming on land check if the player warped directly from swimming to the bathhouse, which isn't possible without commands.
            /*
            if(e.Player.swimming.Value && SwimUtils.IsValidJumpLocation(e.Player.Tile, e.NewLocation))
            {
                e.Player.swimming.Value = false;
                if(!Config.SwimSuitAlways && e.Player.bathingClothes.Value)
                {
                    e.Player.bathingClothes.Value = false;
                }
            }*/
        }

        public static void Player_InventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            if (e.Player != Game1.player)
            {
                return;
            }

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

        /// <summary>
        /// Drawing bubbles when underwater
        /// </summary>
        public static void Display_RenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (ModEntry.isUnderwater.Value && SwimUtils.IsMapUnderwater(Game1.player.currentLocation.Name))
            {
                if ((ticksUnderwater.Value % 100 / Math.Min(100, Config.BubbleMult)) - bubbleOffset.Value == 0)
                {
                    Game1.playSound("tinyWhip");
                    bubbles.Value.Add(new Vector2(Game1.player.position.X + Game1.random.Next(-24, 25), Game1.player.position.Y - 96));
                    if (bubbles.Value.Count > 100)
                    {
                        bubbles.Value.RemoveAt(0);
                    }
                    bubbleOffset.Value = Game1.random.Next(30 / Math.Min(100, Config.BubbleMult));
                }

                for (int k = 0; k < bubbles.Value.Count; k++)
                {
                    bubbles.Value[k] = new Vector2(bubbles.Value[k].X, bubbles.Value[k].Y - 2);
                }

                foreach (Vector2 v in bubbles.Value)
                {
                    e.SpriteBatch.Draw(bubbleTexture, v + new Vector2((float)Math.Sin(ticksUnderwater.Value / 20f) * 10f - Game1.viewport.X, -Game1.viewport.Y), new Rectangle?(new Rectangle(132, 20, 8, 8)), new Color(1, 1, 1, 0.5f), 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.001f);
                }
                ticksUnderwater.Value++;
            }
            else if(ticksUnderwater.Value > 0)
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
            if (Config.ShowOxygenBar && Game1.displayHUD && ModEntry.Oxygen < SwimUtils.MaxOxygen())
            {
                SwimUtils.DrawOxygenBar();
            }
        }

        public static void Display_RenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (Game1.player.currentLocation.Name == "Custom_ScubaAbigailCave")
            {
                SwimAbigailGame.DrawHud(e.SpriteBatch);
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
                    SwimMaps.ReloadWaterTiles(location);
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

            if(ModEntry.locationIsPool.Value)
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
                {
                    return;
                }

                DialogueBox db = menu as DialogueBox;
                int resp = db.selectedResponse;
                List<Response> resps = db.responses.ToList();

                if (resp < 0 || resps == null || resp >= resps.Count || resps[resp] == null)
                {
                    return;
                }
                Game1.player.currentLocation.lastQuestionKey = "";

                SwimDialog.OldMarinerDialogue(resps[resp].responseKey);
                return;
            }
        }

        public static void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if(Game1.activeClickableMenu is not null)
            {
                return;
            }

            if (Config.DiveKey.JustPressed() && Context.IsPlayerFree && Context.CanPlayerMove && ModEntry.diveMaps.TryGetValue(Game1.player.currentLocation.Name, out DiveMap diveMap) && diveMap.DiveLocations.Count > 0)
            {
                SMonitor.Log("Trying to dive!");
                Point pos = Game1.player.TilePoint;
                Location loc = new Location(pos.X, pos.Y);

                if (!SwimUtils.IsInWater())
                {
                    SMonitor.Log("Not in water");
                    return;
                }

                DiveLocation diveLocation = null;
                foreach (DiveLocation dl in diveMap.DiveLocations)
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

            if (Config.SwimKey.JustPressed() && !isJumping.Value)
            {
                Config.ReadyToSwim = !Config.ReadyToSwim;
                SHelper.WriteConfig(Config);
                SMonitor.Log($"Ready to swim: {Config.ReadyToSwim}");
                return;
            }

            if (Config.SwimSuitKey.JustPressed())
            {
                Config.SwimSuitAlways = !Config.SwimSuitAlways;
                SHelper.WriteConfig(Config);
                if (!Game1.player.swimming.Value)
                {
                    if (!Config.SwimSuitAlways)
                    {
                        Game1.player.changeOutOfSwimSuit();

                    }
                    else
                    {
                        Game1.player.changeIntoSwimsuit();
                    }
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
            {
                return;
            }

            ModEntry.isUnderwater.Value = SwimUtils.IsMapUnderwater(Game1.player.currentLocation.Name);

            if (Game1.player.currentLocation.Name == "Custom_ScubaAbigailCave")
            {
                SwimAbigailGame.GameTick();
            }

            if(e.IsMultipleOf(4))
            {
                AnimationManager.SwimShadowFrame++;
                AnimationManager.SwimShadowFrame %= 10;
            }

            SwimUtils.UpdateOxygenValue();

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
                    if (willSwim.Value)
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

            if (Game1.player.swimming.Value && TryToWarp()) // Returns true if it is warping
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
                {
                    Game1.player.changeIntoSwimsuit();
                }


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

            if(!SwimUtils.IsSafeToTryJump())
            {
                return;
            }

            int direction = Game1.player.FacingDirection;

            bool didJump = TryToJumpInDirection(Game1.player.Tile, direction); // Try to jump in the direction the player is facing

            // If we didn't just jump, try to jump in the direction of the cursor if 
            if (!didJump && Config.ManualJumpButton.IsDown() && SwimUtils.IsMouseButtonDown(Config.ManualJumpButton) && Config.EnableClickToSwim)
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
                    // Assuming this happens when the game can't get the mouse position
                    SMonitor.Log("Error in manual direction calculation!");
                }
            }
        }

        public static bool TryToJumpInDirection(Vector2 startingLocation, int direction, bool onlyOneTile = false) // Returns whether or not it is jumping
        {
            GameLocation location = Game1.player.currentLocation;
            int maxDistance;
            Vector2 directionPoint;
            bool jumpToLand = Game1.player.swimming.Value;

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

            Vector2 jumpLocation = startingLocation + directionPoint;
            bool hasFoundImpassableBuilding = false;
            bool foundJumpLocation = false;

            do
            {
                if (!location.isTileOnMap(jumpLocation))
                {
                    jumpLocation += directionPoint;
                    continue;
                }

                bool isWater = SwimUtils.IsWaterTile(jumpLocation);
                if (jumpToLand == isWater)
                {
                    jumpLocation += directionPoint;
                    continue;
                }

                if (jumpToLand)
                {
                    if(SwimUtils.IsTilePassable(location, jumpLocation))
                    {
                        foundJumpLocation = true;
                        break;
                    }
                    jumpLocation += directionPoint;
                    continue;
                }
                else
                {
                    Tile tile = location.map?.GetLayer("Buildings")?.PickTile(new Location((int)jumpLocation.X * Game1.tileSize, (int)jumpLocation.Y * Game1.tileSize), Game1.viewport.Size);

                    if(tile is not null && !location.isTilePassable(jumpLocation))
                    {
                        hasFoundImpassableBuilding = true;
                        if(SwimUtils.AreAdjascentTilesWater(jumpLocation, location))
                        {
                            foundJumpLocation = true;
                            break;
                        }
                    }

                    if(tile is null && (hasFoundImpassableBuilding || !SwimUtils.HasAnyBuildingsAtPosition(location, (int)jumpLocation.X, (int)jumpLocation.Y)))
                    {
                        foundJumpLocation = true;
                        break;
                    }

                    if (tile is not null && tile.TileIndex is 76)
                    {
                        foundJumpLocation = true;
                        break;
                    }
                }

                jumpLocation += directionPoint;
            }
            while (Math.Abs(jumpLocation.X - startingLocation.X + jumpLocation.Y - startingLocation.Y) * Game1.tileSize <= maxDistance && !onlyOneTile);

            //Monitor.Value.Log($"next passable {Game1.player.currentLocation.isTilePassable(new Location((int)tiles.Last().X, (int)tiles.Last().Y), Game1.viewport)} next to land: {nextToLand}, next to water: {nextToWater}");

            if (foundJumpLocation)
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
                willSwim.Value = false;
                Game1.player.swimming.Value = false;
                Game1.player.freezePause = Config.JumpTimeInMilliseconds;
                Game1.player.currentLocation.playSound("dwop");
                Game1.player.currentLocation.playSound("waterSlosh");
            }
            else
            {
                willSwim.Value = true;
                if (!SwimUtils.IsWearingScubaGear() && !Config.NoAutoSwimSuit)
                {
                    Game1.player.changeIntoSwimsuit();
                }

                Game1.player.freezePause = Config.JumpTimeInMilliseconds;
                Game1.player.currentLocation.playSound("dwop");
            }
            isJumping.Value = true;
            startJumpLoc.Value = Game1.player.position.Value;
            endJumpLoc.Value = new Vector2(toLocation.X * Game1.tileSize, toLocation.Y * Game1.tileSize);
        }

        public static bool TryToWarp()
        {
            if(Game1.isWarping)
            {
                return false;
            }
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

            if (Game1.player.position.Y > Game1.viewport.Y + Game1.viewport.Height - 16)
            {
                Game1.player.position.Value = new Vector2(Game1.player.position.X, Game1.viewport.Y + Game1.viewport.Height - 17);
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
            else if (Game1.player.position.X > Game1.viewport.X + Game1.viewport.Width - 32)
            {
                Game1.player.position.Value = new Vector2(Game1.viewport.X + Game1.viewport.Width - 33, Game1.player.position.Y);

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
