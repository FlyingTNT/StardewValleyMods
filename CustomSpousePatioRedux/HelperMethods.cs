using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System.Linq;
using Common.Integrations;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using xTile.Layers;
using xTile.Tiles;
using System;
using StardewValley.Extensions;

namespace CustomSpousePatioRedux
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry : Mod
    {
        private void GameLoop_GameLaunched(object sender, GameLaunchedEventArgs args)
        {
            var configMenu = SHelper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => SHelper.WriteConfig(Config)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-EnableMod"),
                getValue: () => Config.EnableMod,
                setValue: value => Config.EnableMod = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-MaxSpousesPerPage"),
                getValue: () => Config.MaxSpousesPerPage,
                setValue: value => { if (value > 0) Config.MaxSpousesPerPage = value; }
            );

            configMenu.AddKeybindList(
                mod: ModManifest,
                name: () => SHelper.Translation.Get("GMCM-PatioWizardKey"),
                getValue: () => Config.PatioWizardKey,
                setValue: value => Config.PatioWizardKey = value
            );
        }

        private void GameLoop_Saving(object sender, SavingEventArgs e)
        {

            if(Game1.player.IsMainPlayer)
            {
                Helper.Data.WriteSaveData(saveKey, new OutdoorAreaData() {dict = OutdoorAreas});
            }
        }
        private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            spousePositions.Clear();
            baseSpouseAreaTiles.Clear();
            OutdoorAreas.Clear();
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Context.IsSplitScreen && !Context.IsMainPlayer)
                return;

            spousePositions.Clear();
            OutdoorAreas.Clear();
            // Keep the cached default tiles because they are cached before this event is fired.
            baseSpouseAreaTiles.RemoveWhere(kvp => kvp.Key != "default");

            LoadSpouseAreaData();
            DefaultSpouseAreaLocation = Game1.getFarm().GetSpouseOutdoorAreaCorner();
        }

        /// <summary>
        /// Places the spouses outdoors if it is Saturday and not raining/winter
        /// </summary>
        public static void GameLoop_DayStarted(object sender, DayStartedEventArgs e)
        {
            foreach(var kvp in OutdoorAreas)
            {
                if (!HasCachedTiles(kvp.Key))
                {
                    SMonitor.Log($"No cached tiles for {kvp.Key} in DayStarted");
                    if (!TryCacheOffBasePatioArea(kvp.Key))
                    {
                        SMonitor.Log($"Unable to cache tiles for {kvp.Key} in DayStarted!");
                        continue;
                    }
                }

                if (Game1.getLocationFromName(kvp.Value.location) is not GameLocation patioLocation)
                    continue;

                if(!HasAppliedOverride(kvp.Key, patioLocation))
                {
                    SMonitor.Log($"Placing patio for {kvp.Key} in DayStarted");
                    PlaceSpousePatio(kvp.Key);
                }
            }

            if (!Context.IsMainPlayer)
                return;
            if (!Game1.isRaining && !Game1.IsWinter && Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Sat"))
            {
                Farmer farmer = Game1.player;
                //Game1.getFarm().addSpouseOutdoorArea(Game1.player.spouse == null ? "" : Game1.player.spouse);
                var spouses = farmer.friendshipData.Pairs.Where(f => f.Value.IsMarried()).Select(f => f.Key).ToList();
                NPC ospouse = farmer.getSpouse();
                if (ospouse != null)
                {
                    spouses.Add(ospouse.Name);
                }
                foreach (string name in spouses)
                {
                    NPC npc = Game1.getCharacterFromName(name);

                    if (OutdoorAreas.ContainsKey(name) || (farmer.spouse.Equals(npc.Name) && name != "Krobus"))
                    {
                        SMonitor.Log($"placing {name} outdoors");
                        npc.setUpForOutdoorPatioActivity();
                    }
                }
            }
        }

        private void Input_ButtonsChanged(object sender, ButtonsChangedEventArgs args)
        {
            if (!Config.EnableMod || !Context.IsWorldReady)
                return;

            if (Context.CanPlayerMove && Game1.activeClickableMenu is null && Config.PatioWizardKey.JustPressed())
            {
                StartWizard();
            }
            else if (Game1.activeClickableMenu != null && Game1.player?.currentLocation?.lastQuestionKey?.StartsWith("CSP_Wizard_Questions") == true)
            {

                IClickableMenu menu = Game1.activeClickableMenu;
                if (menu == null || menu.GetType() != typeof(DialogueBox))
                    return;

                DialogueBox db = menu as DialogueBox;
                int resp = db.selectedResponse;
                Response[] resps = db.responses;

                if (resp < 0 || resps == null || resp >= resps.Length || resps[resp] == null)
                    return;
                Monitor.Log($"Answered {Game1.player.currentLocation.lastQuestionKey} with {resps[resp].responseKey}");

                currentPage = 0;
                CSPWizardDialogue(Game1.player.currentLocation.lastQuestionKey, resps[resp].responseKey);
                return;
            }
            // Debug commands
            /*
            else if(KeybindList.Parse("L").JustPressed())
            {
                SHelper.GameContent.InvalidateCache(Game1.player.currentLocation.mapPath.Value);
            }
            else if(KeybindList.Parse("O").JustPressed())
            {
                GameLocation l = Game1.player.currentLocation;

                int offset = 0;

                foreach(string spouse in baseSpouseAreaTiles.Keys)
                {
                    Point p = Point.Zero;
                    bool first = true;

                    foreach (string layer in baseSpouseAreaTiles[spouse].Keys)
                    {
                        Layer map_layer = l.map.GetLayer(layer);
                        foreach (Point location in baseSpouseAreaTiles[spouse][layer].Keys)
                        {
                            if(first)
                            {
                                p = location;
                                first = false;
                            }
                            
                            Tile base_tile = baseSpouseAreaTiles[spouse][layer][location];
                            if (map_layer != null)
                            {
                                try
                                {
                                    map_layer.Tiles[location.X - p.X + Game1.player.TilePoint.X + offset, location.Y - p.Y + Game1.player.TilePoint.Y] = base_tile;
                                }
                                catch (Exception ex)
                                {
                                    SMonitor.Log($"Error adding tile {spouse} in {l.Name} for 'O': {ex}");
                                }
                            }
                        }
                    }

                    offset += 5;
                }         
            }*/
        }

        /// <summary>
        /// If a map asset is invalidated and reloaded during the play session, this will automatically reapply any patios to the map
        /// Not currently in use. This functionality has been moved to the GameLocation.MakeMapModifications patch
        /// </summary>
        private void Content_AssetReady(object sender, AssetReadyEventArgs args)
        {
            if (!Context.IsWorldReady || (Context.IsSplitScreen && !Context.IsMainPlayer))
                return;

            foreach (var kvp in OutdoorAreas)
            {
                // If this is the first time loading this patio or the loaction DNE, skip it.
                if (!baseSpouseAreaTiles.ContainsKey(kvp.Key) || Game1.getLocationFromName(kvp.Value.location) is not GameLocation location)
                    continue;

                if (!args.Name.IsEquivalentTo(location.mapPath.Value))
                    continue;

                string affectedLocation = kvp.Value.location;
                SMonitor.Log($"Loaction {kvp.Value.location} map reloaded: {args.Name}");

                foreach(var kvp2 in OutdoorAreas)
                {
                    if (kvp2.Value.location != affectedLocation)
                        continue;

                    TryCacheOffBasePatioArea(kvp.Key);
                    PlaceSpousePatio(kvp.Key);
                }

                return;
            }
        }

        #region Multiplayer
        public enum PatioChange
        {
            Remove,
            Move,
            Add
        }

        public struct PatioMessage
        {
            public PatioChange Type = PatioChange.Remove;
            public string Spouse = "";
            public string Location = "";
            public Vector2 Position = Vector2.Zero;
            public bool IsFromMainPlayer = false;

            public PatioMessage() {}

            public PatioMessage(string spouse, string location, Vector2 position, PatioChange type, bool isFromMainPlayer)
            {
                Spouse = spouse;
                Location = location;
                Position = position;
                Type = type;
                IsFromMainPlayer = isFromMainPlayer;
            }
        }

        private void Multiplayer_ModMessageRecieved(object sender, ModMessageReceivedEventArgs args)
        {
            if (args.FromModID != ModManifest.UniqueID)
                return;

            if(args.Type == "PatioRequest")
            {
                if (!Context.IsMainPlayer)
                    return;

                foreach (var kvp in OutdoorAreas)
                {
                    SendModMessage(kvp.Key, kvp.Value.location, kvp.Value.corner, PatioChange.Add, new long[] { args.FromPlayerID});
                }

                return;
            }

            if (args.Type != "PatioMessage")
                return;

            PatioMessage message = args.ReadAs<PatioMessage>();

            SMonitor.Log($"Recieved message {message.Type} {message.Spouse} {message.Location} {message.IsFromMainPlayer}");

            // Host + local players ignore messages from the host, and non-main players ignore messages from other non-main players
            if ((Context.IsOnHostComputer && message.IsFromMainPlayer) || (!Context.IsMainPlayer && !message.IsFromMainPlayer))
                return;

            switch(message.Type)
            {
                case PatioChange.Remove:
                    RemoveSpousePatio(message.Spouse); 
                    break;
                case PatioChange.Move:
                    MoveSpousePatio(message.Spouse, message.Location, message.Position);
                    break;
                case PatioChange.Add:
                    AddSpousePatio(message.Spouse, message.Location, message.Position);
                    break;
            }

            if (!Context.IsMainPlayer)
                return;

            // If this is the main player, repeat the message to all players except the sender
            message.IsFromMainPlayer = true;
            SHelper.Multiplayer.SendMessage(message, "PatioMessage", new string[] { Manifest.UniqueID }, SHelper.Multiplayer.GetConnectedPlayers().Select(peer => peer.PlayerID).Where(id => id != args.FromPlayerID).ToArray());
        }

        private static void SendModMessage(string spouse, string location, Vector2 position, PatioChange type, long[] toPlayer = null)
        {
            SHelper.Multiplayer.SendMessage(new PatioMessage(spouse, location, position, type, Context.IsMainPlayer), "PatioMessage", new string[] {Manifest.UniqueID}, toPlayer);
        }

        #endregion
    }
}