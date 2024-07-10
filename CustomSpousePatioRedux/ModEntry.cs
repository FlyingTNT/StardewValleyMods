using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using xTile.Layers;
using xTile.Tiles;

namespace CustomSpousePatioRedux
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        public static ModConfig Config;
        public static OutdoorAreaData outdoorAreas;
        public static IMonitor SMonitor;
        public static IModHelper SHelper;

        /// <summary> Spouse name -> Layer name -> tiles in that layer *before* the patio was applied. </summary>
        public static Dictionary<string, Dictionary<string, Dictionary<Point, Tile>>> baseSpouseAreaTiles = new Dictionary<string, Dictionary<string, Dictionary<Point, Tile>>>();
        /// <summary> Spouse name -> The point where they should sit/stand.</summary>
        public static Dictionary<string, Point> spousePositions = new Dictionary<string, Point>();
        private static List<string> noCustomAreaSpouses;

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
            if (!Config.EnableMod)
                return;
            SMonitor = Monitor;
            SHelper = Helper;

            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.Saving += GameLoop_Saving;
            Helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            Helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;

            var harmony = new  Harmony(this.ModManifest.UniqueID);

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

        }

        /// <summary> 
        /// Loads the spouse area data from the save file. 
        /// </summary>
        public static void LoadSpouseAreaData()
        {
            if (!Config.EnableMod)
                return; 
            if (!Context.IsMainPlayer)
            {
                SMonitor.Log($"Not the host player, this copy of the mod will not do anything.", LogLevel.Warn);
                return;
            }
            outdoorAreas = SHelper.Data.ReadSaveData<OutdoorAreaData>(saveKey) ?? new OutdoorAreaData();
            if(outdoorAreas.areas != null)
            {
                foreach(var area in outdoorAreas.areas)
                {
                    outdoorAreas.dict.Add(area.Key, new OutdoorArea() { location = Game1.getFarm().Name, corner = area.Value});
                }
            }
            foreach (var area in outdoorAreas.dict)
            {
                CacheOffBasePatioArea(area.Key);
            }
            SMonitor.Log($"Total outdoor spouse areas: {outdoorAreas.dict.Count}", LogLevel.Debug);
        }

        private static Vector2 GetSpouseOutdoorAreaCorner(string spouseName)
        {
            if (!Config.EnableMod || outdoorAreas == null || outdoorAreas.dict.Count == 0)
                return Game1.getFarm().GetSpouseOutdoorAreaCorner();

            return outdoorAreas.dict.TryGetValue(spouseName, out OutdoorArea value) ? value.corner : Game1.getFarm().GetSpouseOutdoorAreaCorner();
        }
        
        private static void ApplyMapOverride(string spouseName, string map_name, string override_key_name, Rectangle? source_rect = null, Rectangle? destination_rect = null)
        {
            if (Config.EnableMod && outdoorAreas == null)
                return;
            GameLocation l = Game1.getLocationFromName(Config.EnableMod && outdoorAreas.dict.TryGetValue(spouseName, out OutdoorArea area) ? area.location : "Farm");
            if (l == null)
                l = Game1.getFarm();
            SMonitor.Log($"Applying patio map override for {spouseName} in {l.Name}");
            if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(l, "_appliedMapOverrides").Contains("spouse_patio"))
            {
                AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(l, "_appliedMapOverrides").Remove("spouse_patio");
            }
            if (AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(l, "_appliedMapOverrides").Contains(spouseName+"_spouse_patio"))
            {
                AccessTools.FieldRefAccess<GameLocation, HashSet<string>>(l, "_appliedMapOverrides").Remove(spouseName + "_spouse_patio");
            }
            l.ApplyMapOverride(map_name, spouseName + "_spouse_patio", source_rect, destination_rect);
        }

        /// <summary>
        /// Reapplies the tiles that were there before the patio was added (i.e. removes the patio)
        /// </summary>
        /// <param name="spouse"> The spouse to remove the patio of. </param>
        private static void ReapplyBasePatioArea(string spouse = "default")
        {
            if (!baseSpouseAreaTiles.ContainsKey(spouse))
            {
                SMonitor.Log($"No cached tiles to reapply for {spouse}", LogLevel.Error);
                return;
            }
            GameLocation l = null;
            if (outdoorAreas != null && outdoorAreas.dict.TryGetValue(spouse, out OutdoorArea area))
            {
                l = Game1.getLocationFromName(area.location);
            }
            else
            {
                l = Game1.getFarm();
            }
            if (l == null)
                l = Game1.getFarm();

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
        }

        /// <summary>
        /// Caches the tiles on the map before the patio is added for the given spouse.
        /// </summary>
        /// <param name="spouse"> The spouse whose tiles to cache.</param>
        private static void CacheOffBasePatioArea(string spouse)
        {
            if (!outdoorAreas.dict.TryGetValue(spouse, out OutdoorArea area))
                return;
            GameLocation l = Game1.getLocationFromName(area.location);
            if (l == null)
                l = Game1.getFarm();
            if (l == null)
                return;
            CacheOffBasePatioArea(spouse, l, area.corner);
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
        }

        public static string GetSpousePatioName(string spouseName)
        {
            return spouseName + "_spouse_patio";
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
    }

}