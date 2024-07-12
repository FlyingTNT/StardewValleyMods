using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Layers;
using xTile.Tiles;
using Common.Utilities;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace CustomSpousePatioRedux
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        public static ModConfig Config;
        public static readonly Dictionary<string, OutdoorArea> OutdoorAreas = new();
        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        private static IManifest Manifest;

        /// <summary> Spouse name -> Layer name -> tiles in that layer *before* the patio was applied. </summary>
        public static readonly Dictionary<string, Dictionary<string, Dictionary<Point, Tile>>> baseSpouseAreaTiles = new();
        /// <summary> Spouse name -> The point where they should sit/stand.</summary>
        public static readonly Dictionary<string, Point> spousePositions = new();
        private static readonly List<string> noCustomAreaSpouses = new();

        /// <summary> The base game spouse patio location. </summary>
        public static Vector2 DefaultSpouseAreaLocation { get; set; }
        public static readonly string saveKey = "custom-spouse-patio-data";

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            
            // Adds config options to GMCM
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            if (!Config.EnableMod)
                return;
            SMonitor = Monitor;
            SHelper = Helper;
            Manifest = ModManifest;

            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.Saving += GameLoop_Saving;
            Helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            Helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            //Helper.Events.Content.AssetReady += Content_AssetReady;
            Helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageRecieved;

            var harmony = new Harmony(ModManifest.UniqueID);

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.GetSpousePatioPosition)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.NPC_GetSpousePatioPosition_Prefix))
            );
            
            harmony.Patch(
               original: AccessTools.Method(typeof(Farm), nameof(Farm.addSpouseOutdoorArea)),
               transpiler: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Farm_addSpouseOutdoorArea_Transpiler)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Farm_addSpouseOutdoorArea_Postfix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(Farm), nameof(Farm.CacheOffBasePatioArea)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Farm_CacheOffBasePatioArea_Prefix))
            );
            harmony.Patch(
               original: AccessTools.Method(typeof(Farm), nameof(Farm.ReapplyBasePatioArea)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Farm_ReapplyBasePatioArea_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(NPC), nameof(NPC.setUpForOutdoorPatioActivity)),
               prefix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.NPC_setUpForOutdoorPatioActivity_Prefix))
            );

            harmony.Patch(
               original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.MakeMapModifications)),
               postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.GameLocation_MakeMapModifications_Postifx))
            );

        }

        /// <summary> 
        /// Loads the spouse area data from the save file. 
        /// </summary>
        public static void LoadSpouseAreaData()
        {
            if (!Config.EnableMod)
                return;

            // If this is a remote player, ask the host to send the patios.
            if(!Context.IsOnHostComputer)
            {
                SHelper.Multiplayer.SendMessage("", "PatioRequest", new string[] { Manifest.UniqueID }, new long[] { Game1.MasterPlayer.UniqueMultiplayerID });
                return;
            }

            OutdoorAreaData outdoorAreas = PerSaveConfig.LoadConfigOption<OutdoorAreaData>(SHelper, saveKey, defaultValue: new());
            if(outdoorAreas.areas != null)
            {
                foreach(var area in outdoorAreas.areas)
                {
                    outdoorAreas.dict.Add(area.Key, new OutdoorArea() { location = Game1.getFarm().Name, corner = area.Value});
                }
            }

            foreach(var kvp in outdoorAreas.dict)
            {
                OutdoorAreas.Add(kvp.Key, kvp.Value);
                if(!TryCacheOffBasePatioArea(kvp.Key))
                {
                    SMonitor.Log("Unable to cache patios in load!");
                }
            }

            SMonitor.Log($"Total outdoor spouse areas: {outdoorAreas.dict.Count}", LogLevel.Debug);
        }

        private static Vector2 GetSpouseOutdoorAreaCorner(string spouseName)
        {
            if (!Config.EnableMod || OutdoorAreas.Count == 0)
                return Game1.getFarm().GetSpouseOutdoorAreaCorner();

            return OutdoorAreas.TryGetValue(spouseName, out OutdoorArea value) ? value.corner : Game1.getFarm().GetSpouseOutdoorAreaCorner();
        }

        /// <summary>
        /// Adds the patio tiles for the given spouse. Used in the Farm_addSpouseOutdoorArea_Transpiler.
        /// </summary>
        private static void ApplyMapOverride(string spouseName, string map_name, string override_key_name, Rectangle? source_rect = null, Rectangle? destination_rect = null)
        {
            if (!Config.EnableMod)
                return;
            GameLocation l = Game1.getLocationFromName(Config.EnableMod && OutdoorAreas.TryGetValue(spouseName, out OutdoorArea area) ? area.location : "Farm") ?? Game1.getFarm();

            SMonitor.Log($"Applying patio map override for {spouseName} in {l.Name}");

            RemoveOverrideFlags(spouseName, l);

            // The layers before the override was added (if the override map contains layers not in the base map, they will be added automatically)
            HashSet<string> beforeLayers = new();
            foreach(var layer in l.Map.Layers)
            {
                beforeLayers.Add(layer.Id);
            }

            l.ApplyMapOverride(map_name, spouseName + "_spouse_patio", source_rect, destination_rect);

            // If there were any layers added by the override, add them to the cached base tiles as all null so that that layer gets removed if the patio is moved/removed
            foreach (var layer in l.Map.Layers)
            {
                if (beforeLayers.Contains(layer.Id))
                    continue;

                Dictionary<Point, Tile> thisLayerDict = new();

                for(int i = 0; i < destination_rect.Value.Width; i++)
                {
                    for(int j = 0; j < destination_rect.Value.Height; j++)
                    {
                        thisLayerDict[new Point(destination_rect.Value.Left + i, destination_rect.Value.Top + j)] = null;
                    }
                }

                baseSpouseAreaTiles[spouseName][layer.Id] = thisLayerDict;
            }
        }

        private static void RemoveOverrideFlags(string spouse, GameLocation location)
        {
            if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(location, "_appliedMapOverrides").Contains("spouse_patio"))
            {
                AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(location, "_appliedMapOverrides").Remove("spouse_patio");
            }
            if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(location, "_appliedMapOverrides").Contains(spouse + "_spouse_patio"))
            {
                AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(location, "_appliedMapOverrides").Remove(spouse + "_spouse_patio");
            }
        }

        private static bool HasAppliedOverride(string spouse, GameLocation location)
        {
            return AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(location, "_appliedMapOverrides").Contains(spouse + "_spouse_patio");
        }

        /// <summary>
        /// Reapplies the tiles that were there before the patio was added (i.e. removes the patio)
        /// </summary>
        /// <param name="spouse"> The spouse to remove the patio of. </param>
        private static void ReapplyBasePatioArea(string spouse = "default")
        {
            if (!baseSpouseAreaTiles.ContainsKey(spouse) && !TryCacheOffBasePatioArea(spouse))
            {
                SMonitor.Log($"No cached tiles to reapply for {spouse}", LogLevel.Error);
                return;
            }

            GameLocation l;
            if (OutdoorAreas.TryGetValue(spouse, out OutdoorArea area))
            {
                l = Game1.getLocationFromName(area.location) ?? Game1.getFarm();
            }
            else
            {
                l = Game1.getFarm();
            }

            SMonitor.Log($"Reapplying base patio area for {spouse} in {l.Name}");

            foreach (string layer in baseSpouseAreaTiles[spouse].Keys)
            {
                Layer map_layer = l.map.GetLayer(layer);
                foreach (Point location in baseSpouseAreaTiles[spouse][layer].Keys)
                {
                    Tile base_tile = baseSpouseAreaTiles[spouse][layer][location];
                    if (map_layer != null)
                    {
                        try
                        {
                            map_layer.Tiles[location.X, location.Y] = base_tile;
                        }
                        catch(Exception ex)
                        {
                            SMonitor.Log($"Error adding tile {spouse} in {l.Name}: {ex}");
                        }
                    }
                }
            }

            RemoveOverrideFlags(spouse, l);

            if (l is DecoratableLocation decoratableLocation)
            {
                // Incase the wallpaper or floor has changed since we cached the tiles
                decoratableLocation.setWallpapers();
                decoratableLocation.setFloors();
            }
        }

        /// <summary>
        /// Caches the tiles on the map before the patio is added for the given spouse.
        /// This method can still throw exceptions; it basically only returns false if the location to cache from is not loaded.
        /// </summary>
        /// <param name="spouse"> The spouse whose tiles to cache.</param>
        private static bool TryCacheOffBasePatioArea(string spouse)
        {
            if(spouse == "default")
            {
                return HasCachedTiles("default");
            }

            baseSpouseAreaTiles.Remove(spouse);

            if (!OutdoorAreas.TryGetValue(spouse, out OutdoorArea area))
                return false;
            GameLocation l = Game1.getLocationFromName(area.location) ?? Game1.getLocationFromName("Farm");

            if (l is null)
                return false;

            CacheOffBasePatioArea(spouse, l, area.corner);
            return true;
        }

        /// <summary>
        /// Sets up baseSpouseAreaTiles.
        /// Caches the tiles currently in the location (before the patio)
        /// </summary>
        /// <param name="spouse"> The name of the spouse whose patio to setup. </param>
        /// <param name="l"> The location whose tiles to cache. </param>
        /// <param name="corner"> The upper-left corner of the patio. </param>
        private static void CacheOffBasePatioArea(string spouse, GameLocation l, Vector2 corner)
        {
            SMonitor.Log($"Caching base patio area for {spouse} in {l.Name} at {corner}");

            baseSpouseAreaTiles.Remove(spouse);

            // Temporarily removes all patios in this location so that their tiles don't end up being cached.
            List<string> temporarilyRemovedPatios = new();
            foreach (var kvp in OutdoorAreas)
            {
                if(kvp.Value.location == l.Name && HasAppliedOverride(kvp.Key, l) && HasCachedTiles(kvp.Key))
                {
                    ReapplyBasePatioArea(kvp.Key);
                    temporarilyRemovedPatios.Add(kvp.Key);
                }
            }

            baseSpouseAreaTiles[spouse] = new Dictionary<string, Dictionary<Point, Tile>>();

            List<string> layers_to_cache = new List<string>();
            foreach (Layer layer in l.map.Layers)
            {
                layers_to_cache.Add(layer.Id);
            }
            foreach (string layer_name in layers_to_cache)
            {
                Layer original_layer = l.map.GetLayer(layer_name);
                Dictionary<Point, Tile> tiles = new Dictionary<Point, Tile>();
                baseSpouseAreaTiles[spouse][layer_name] = tiles;
                Vector2 spouse_area_corner = corner;
                for (int x = (int)spouse_area_corner.X; x < (int)spouse_area_corner.X + 4; x++)
                {
                    for (int y = (int)spouse_area_corner.Y; y < (int)spouse_area_corner.Y + 4; y++)
                    {
                        if (original_layer == null)
                        {
                            tiles[new Point(x, y)] = null;
                        }
                        else
                        {
                            tiles[new Point(x, y)] = original_layer.Tiles[x, y];
                        }
                    }
                }
            }

            foreach(string spouse1 in temporarilyRemovedPatios)
            {
                PlaceSpousePatio(spouse1);
            }
        }

        public static bool HasCachedTiles(string spouse)
        {
            return baseSpouseAreaTiles.ContainsKey(spouse);
        }

        public static bool IsSpousePatioDay(NPC npc)
        {
            return !Game1.isRaining && !Game1.IsWinter && Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Sat") && npc.getSpouse() == Game1.MasterPlayer && !npc.Name.Equals("Krobus");
        }
        public static void PlaceSpouses()
        {
            foreach(KeyValuePair<string, Friendship> kvp in Game1.MasterPlayer.friendshipData.Pairs.Where(n => n.Value.IsMarried() && !n.Value.IsEngaged()))
            {
                NPC npc = Game1.getCharacterFromName(kvp.Key);
                if (IsSpousePatioDay(npc))
                    npc.setUpForOutdoorPatioActivity();
            }
        }

        public static void AddSpousePatio(string spouse, string loaction, Vector2 position)
        {
            if (OutdoorAreas.ContainsKey(spouse))
            {
                MoveSpousePatio(spouse, loaction, position);
                return;
            }

            // Reapply the default patio
            ReapplyBasePatioArea();
            RemoveOverrideFlags("default", Game1.getFarm());

            OutdoorAreas.Add(spouse, new OutdoorArea() {location = loaction, corner = position});
            
            if (!TryCacheOffBasePatioArea(spouse))
            {
                SMonitor.Log($"Unable to cache patio for {spouse}!", LogLevel.Error);
                return;
            }

            Game1.getFarm().UpdatePatio();

            if (Game1.getCharacterFromName(spouse)?.shouldPlaySpousePatioAnimation.Value == true)
            {
                Game1.getCharacterFromName(spouse).setUpForOutdoorPatioActivity();
            }

            SMonitor.Log($"Added spouse patio for {spouse} at {position}");
        }

        public static void MoveSpousePatio(string spouse, string toLocation, Vector2 toPosition)
        {
            if(!OutdoorAreas.ContainsKey(spouse))
            {
                AddSpousePatio(spouse, toLocation, toPosition);
            }

            // Remove the patio
            ReapplyBasePatioArea(spouse);

            // Update the position internally
            OutdoorAreas[spouse].corner = toPosition;
            OutdoorAreas[spouse].location = toLocation;
            
            SMonitor.Log($"Moved spouse patio for {spouse} to {toPosition}");

            // Cache the base tiles
            if (!TryCacheOffBasePatioArea(spouse))
            {
                SMonitor.Log($"Unable to cache patio for {spouse}!", LogLevel.Error);
                return;
            }

            // Update all of the patios
            Game1.getFarm().UpdatePatio();

            // Move the spouse to the correct position, if applicable
            if (Game1.getCharacterFromName(spouse)?.shouldPlaySpousePatioAnimation.Value == true)
            {
                Game1.getCharacterFromName(spouse).setUpForOutdoorPatioActivity();
            }
        }

        public static void RemoveSpousePatio(string spouse)
        {
            ReapplyBasePatioArea(spouse);
            baseSpouseAreaTiles.Remove(spouse);
            spousePositions.Remove(spouse);
            OutdoorAreas.Remove(spouse);

            Game1.getFarm().UpdatePatio();

            SMonitor.Log($"Removed spouse patio for {spouse}");
        }

        public static void PlaceSpousePatio(string spouse)
        {
            if (Game1.getFarm() is not Farm farm)
                return;

            addingExtraAreas = true;
            farm.addSpouseOutdoorArea(spouse);
            addingExtraAreas = false;
        }
    }

}