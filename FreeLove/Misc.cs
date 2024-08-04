using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static FreeLove.ModEntry;
using Object = StardewValley.Object;

namespace FreeLove
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        private static Dictionary<string, int> topOfHeadOffsets = new Dictionary<string, int>();

        /// <summary>
        /// Reloads all of the spouses in the given Farmer's friendship data into currentSpouses and currentUnofficialSpouses
        /// </summary>
        /// <param name="farmer">The farmer whose spouses to reload.</param>
        public static void ReloadSpouses(Farmer farmer)
        {
            currentSpouses[farmer.UniqueMultiplayerID] = new Dictionary<string, NPC>();
            currentUnofficialSpouses[farmer.UniqueMultiplayerID] = new Dictionary<string, NPC>();
            string ospouse = farmer.spouse;
            if (ospouse != null)
            {
                var npc = Game1.getCharacterFromName(ospouse);
                if(npc is not null)
                {
                    currentSpouses[farmer.UniqueMultiplayerID][ospouse] = npc;
                }
            }
            SMonitor.Log($"Checking for extra spouses in {farmer.friendshipData.Count()} friends");
            foreach (string friend in farmer.friendshipData.Keys)
            {
                if (farmer.friendshipData[friend].IsMarried() && friend != farmer.spouse)
                {
                    var npc = Game1.getCharacterFromName(friend, true);
                    if(npc != null)
                    {
                        currentSpouses[farmer.UniqueMultiplayerID][friend] = npc;
                        currentUnofficialSpouses[farmer.UniqueMultiplayerID][friend] = npc;
                    }
                }
            }
            if (farmer.spouse is null && currentSpouses[farmer.UniqueMultiplayerID].Any())
            {
                farmer.spouse = currentSpouses[farmer.UniqueMultiplayerID].First().Key;
                currentUnofficialSpouses[farmer.UniqueMultiplayerID].Remove(farmer.spouse);
            }
            SMonitor.Log($"reloaded {currentSpouses[farmer.UniqueMultiplayerID].Count} spouses for {farmer.Name} {farmer.UniqueMultiplayerID}");
        }

        /// <summary>
        /// Gets a spouse name => NPC dictionary for the given farmer's spouses
        /// </summary>
        /// <param name="all"> If false, the dictionary will not include the farmer's official spouse. </param>
        /// <returns></returns>
        public static Dictionary<string, NPC> GetSpouses(Farmer farmer, bool all)
        {
            if(!currentSpouses.ContainsKey(farmer.UniqueMultiplayerID) || ((currentSpouses[farmer.UniqueMultiplayerID].Count == 0 && farmer.spouse != null)))
            {
                ReloadSpouses(farmer);
            }
            if(farmer.spouse is null && currentSpouses[farmer.UniqueMultiplayerID].Count > 0)
            {
                farmer.spouse = currentSpouses[farmer.UniqueMultiplayerID].First().Key;
                currentUnofficialSpouses[farmer.UniqueMultiplayerID].Remove(farmer.spouse);
            }
            return all ? currentSpouses[farmer.UniqueMultiplayerID] : currentUnofficialSpouses[farmer.UniqueMultiplayerID];
        }

        /// <summary>
        /// Removes all divorce statuses from Game1.player's friendship data
        /// </summary>
        internal static void ResetDivorces()
        {
            if (!Config.PreventHostileDivorces)
                return;
            List<string> friends = Game1.player.friendshipData.Keys.ToList();
            foreach(string f in friends)
            {
                if(Game1.player.friendshipData[f].Status == FriendshipStatus.Divorced)
                {
                    SMonitor.Log($"Wiping divorce for {f}");
                    if (Game1.player.friendshipData[f].Points < 8 * 250)
                        Game1.player.friendshipData[f].Status = FriendshipStatus.Friendly;
                    else
                        Game1.player.friendshipData[f].Status = FriendshipStatus.Dating;
                }
            }
        }

        public static string GetRandomSpouse(Farmer f)
        {
            var spouses = GetSpouses(f, true);
            if (spouses.Count == 0)
                return null;
            ShuffleDic(ref spouses);
            return spouses.Keys.ToArray()[0];
        }

        public static void PlaceSpousesInFarmhouse(FarmHouse farmHouse)
        {
            return;
            Farmer farmer = farmHouse.owner;

            if (farmer == null)
                return;

            List<NPC> allSpouses = GetSpouses(farmer, true).Values.ToList();

            if (allSpouses.Count == 0)
            {
                SMonitor.Log("no spouses");
                return;
            }

            ShuffleList(ref allSpouses);

            List<string> bedSpouses = new List<string>();
            string kitchenSpouse = null;

            foreach (NPC spouse in allSpouses)
            {
                if(spouse is null) 
                    continue;
                if (!farmHouse.Equals(spouse.currentLocation))
                {
                    SMonitor.Log($"{spouse.Name} is not in farm house ({spouse.currentLocation.Name})");
                    continue;
                }
                int type = myRand.Next(0, 100);

                SMonitor.Log($"spouse rand {type}, bed: {Config.PercentChanceForSpouseInBed} kitchen {Config.PercentChanceForSpouseInKitchen}");
                
                if(type < Config.PercentChanceForSpouseInBed)
                {
                    if (bedSpouses.Count < 1 && (Config.RoommateRomance || !farmer.friendshipData[spouse.Name].IsRoommate()) && HasSleepingAnimation(spouse.Name))
                    {
                        SMonitor.Log("made bed spouse: " + spouse.Name);
                        bedSpouses.Add(spouse.Name);
                    }

                }
                else if(type < Config.PercentChanceForSpouseInBed + Config.PercentChanceForSpouseInKitchen)
                {
                    if (kitchenSpouse == null)
                    {
                        SMonitor.Log("made kitchen spouse: " + spouse.Name);
                        kitchenSpouse = spouse.Name;
                    }
                }
                else if(type < Config.PercentChanceForSpouseInBed + Config.PercentChanceForSpouseInKitchen + Config.PercentChanceForSpouseAtPatio)
                {
                    if (!Game1.isRaining && !Game1.IsWinter && !Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth).Equals("Sat") && !spouse.Name.Equals("Krobus") && spouse.Schedule == null)
                    {
                        SMonitor.Log("made patio spouse: " + spouse.Name);
                        spouse.setUpForOutdoorPatioActivity();
                        SMonitor.Log($"{spouse.Name} at {spouse.currentLocation.Name} {spouse.TilePoint}");
                    }
                }
            }

            foreach (NPC spouse in allSpouses) 
            {
                if (spouse is null)
                    continue;
                SMonitor.Log("placing " + spouse.Name);
                Point spouseRoomSpot = new Point(-1, -1); 
                
                if(CustomSpouseRoomsAPI != null)
                {
                    SMonitor.Log($"Getting spouse spot from Custom Spouse Rooms");

                    spouseRoomSpot = CustomSpouseRoomsAPI.GetSpouseTile(spouse);
                    if(spouseRoomSpot.X >= 0)
                        SMonitor.Log($"Got custom spouse spot {spouseRoomSpot}");
                }
                if(spouseRoomSpot.X < 0 && farmer.spouse == spouse.Name)
                {
                    spouseRoomSpot = farmHouse.GetSpouseRoomSpot();
                    SMonitor.Log($"Using default spouse spot {spouseRoomSpot}");
                }

                if (!farmHouse.Equals(spouse.currentLocation))
                {
                    SMonitor.Log($"{spouse.Name} is not in farm house ({spouse.currentLocation.Name})");
                    continue;
                }

                SMonitor.Log("in farm house");
                spouse.shouldPlaySpousePatioAnimation.Value = false;

                Vector2 bedPos = GetSpouseBedPosition(farmHouse, spouse.Name);

                if (bedSpouses.Count > 0 && bedSpouses.Contains(spouse.Name) && bedPos != Vector2.Zero)
                {
                    SMonitor.Log($"putting {spouse.Name} in bed");
                    spouse.position.Value = GetSpouseBedPosition(farmHouse, spouse.Name);
                }
                else if (kitchenSpouse == spouse.Name && !IsTileOccupied(farmHouse, farmHouse.getKitchenStandingSpot(), spouse.Name))
                {
                    SMonitor.Log($"{spouse.Name} is in kitchen");

                    spouse.setTilePosition(farmHouse.getKitchenStandingSpot());
                    spouse.setRandomAfternoonMarriageDialogue(Game1.timeOfDay, farmHouse, false);
                }
                else if (spouseRoomSpot.X > -1 && !IsTileOccupied(farmHouse, spouseRoomSpot, spouse.Name))
                {
                    SMonitor.Log($"{spouse.Name} is in spouse room");
                    spouse.setTilePosition(spouseRoomSpot);
                    spouse.setSpouseRoomMarriageDialogue();
                }
                else 
                { 
                    SpotDirection spotDirection = GetRandomGoodSpotInFarmhouse(farmHouse);
                    spouse.setTileLocation(spotDirection.Spot);
                    spouse.faceDirection(spotDirection.Direction);
                    SMonitor.Log($"{spouse.Name} spouse random loc {spouse.TilePoint}");
                    spouse.setRandomAfternoonMarriageDialogue(Game1.timeOfDay, farmHouse, false);
                }
            }
        }

        private static bool IsTileOccupied(GameLocation location, Point tileLocation, string characterToIgnore)
        {
            // TODO: Consider just uisng location.IsCharacterAtTile(); or NPC.checkTileOccupancyForSpouse().
            Rectangle tileLocationRect = new Rectangle(tileLocation.X * 64 + 1, tileLocation.Y * 64 + 1, 62, 62);

            for (int i = 0; i < location.characters.Count; i++)
            {
                if (location.characters[i] != null && !location.characters[i].Name.Equals(characterToIgnore) && location.characters[i].GetBoundingBox().Intersects(tileLocationRect))
                {
                    SMonitor.Log($"Tile {tileLocation} is occupied by {location.characters[i].Name}");

                    return true;
                }
            }
            return false;
        }

        public static Point GetSpouseBedEndPoint(FarmHouse fh, string name)
        {
            var bedSpouses = GetBedSpouses(fh);

            Point bedStart = fh.GetSpouseBed().GetBedSpot();
            int bedWidth = GetBedWidth();
            bool up = fh.upgradeLevel > 1;

            int x = (int)(bedSpouses.IndexOf(name) / (float)(bedSpouses.Count) * (bedWidth - 1));
            if (x < 0)
                return Point.Zero;
            return new Point(bedStart.X + x, bedStart.Y);
        }
        public static Vector2 GetSpouseBedPosition(FarmHouse fh, string name)
        {
            var allBedmates = GetBedSpouses(fh);

            Point bedStart = GetBedStart(fh);
            int x = 64 + (int)((allBedmates.IndexOf(name) + 1) / (float)(allBedmates.Count + 1) * (GetBedWidth() - 1) * 64);
            return new Vector2(bedStart.X * 64 + x, bedStart.Y * 64 + bedSleepOffset - (GetTopOfHeadSleepOffset(name) * 4));
        }

        public static Point GetBedStart(FarmHouse fh)
        {
            if (fh?.GetSpouseBed()?.GetBedSpot() == null)
                return Point.Zero;
            return new Point(fh.GetSpouseBed().GetBedSpot().X - 1, fh.GetSpouseBed().GetBedSpot().Y - 1);
        }

        public static bool IsInBed(FarmHouse fh, Rectangle box)
        {
            int bedWidth = GetBedWidth();
            Point bedStart = GetBedStart(fh);
            Rectangle bed = new Rectangle(bedStart.X * 64, bedStart.Y * 64, bedWidth * 64, 3 * 64);

            if (box.Intersects(bed))
            {
                return true;
            }
            return false;
        }
        public static int GetBedWidth()
        {
            if (BedTweaksAPI != null)
            {
                return BedTweaksAPI.GetBedWidth();
            }
            else
            {
                return 3;
            }
        }
        public static List<string> GetBedSpouses(FarmHouse fh)
        {
            if (Config.RoommateRomance)
                return GetSpouses(fh.owner, true).Keys.ToList();

            return GetSpouses(fh.owner, true).Keys.ToList().FindAll(s => !fh.owner.friendshipData[s].RoommateMarriage);
        }

        public static List<string> ReorderSpousesForSleeping(IEnumerable<string> sleepSpouses)
        {
            // Normalize the config option and sleepSpouses to be lowercase and have no leading/trailing whitespace or empty itemss
            List<string> configSpouses = Config.SpouseSleepOrder.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(name => name.ToLower()).ToList();
            sleepSpouses = sleepSpouses.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim().ToLower());

            List<string> spouses = new();
            foreach (string s in configSpouses)
            {
                if (sleepSpouses.Contains(s))
                    spouses.Add(s);
            }

            foreach (string s in sleepSpouses)
            {
                if (!spouses.Contains(s))
                {
                    spouses.Add(s);
                    configSpouses.Add(s);
                }
            }
            string configString = string.Join(",", configSpouses);
            if (configString != Config.SpouseSleepOrder)
            {
                Config.SpouseSleepOrder = configString;
                SHelper.WriteConfig(Config);
            }

            return spouses;
        }


        public static int GetTopOfHeadSleepOffset(string name)
        {
            if (topOfHeadOffsets.ContainsKey(name))
            {
                return topOfHeadOffsets[name];
            }
            //SMonitor.Log($"dont yet have offset for {name}");
            int top = 0;

            if (name == "Krobus")
                return 8;

            Texture2D tex = Game1.content.Load<Texture2D>($"Characters\\{name}");

            int sleepidx;
            string sleepAnim = SleepAnimation(name);
            if (sleepAnim == null || !int.TryParse(sleepAnim.Split('/')[0], out sleepidx))
                sleepidx = 8;

            if ((sleepidx * 16) / 64 * 32 >= tex.Height)
            {
                sleepidx = 8;
            }


            Color[] colors = new Color[tex.Width * tex.Height];
            tex.GetData(colors);

            //SMonitor.Log($"sleep index for {name} {sleepidx}");

            int startx = (sleepidx * 16) % 64;
            int starty = (sleepidx * 16) / 64 * 32;

            //SMonitor.Log($"start {startx},{starty}");

            for (int i = 0; i < 16 * 32; i++)
            {
                int idx = startx + (i % 16) + (starty + i / 16) * 64;
                if (idx >= colors.Length)
                {
                    SMonitor.Log($"Sleep pos couldn't get pixel at {startx + i % 16},{starty + i / 16} ");
                    break;
                }
                Color c = colors[idx];
                if (c != Color.Transparent)
                {
                    top = i / 16;
                    break;
                }
            }
            topOfHeadOffsets.Add(name, top);
            return top;
        }


        public static bool HasSleepingAnimation(string name)
        {
            string sleepAnim = SleepAnimation(name);
            if (sleepAnim == null || !sleepAnim.Contains("/"))
                return false;

            if (!int.TryParse(sleepAnim.Split('/')[0], out int sleepidx))
                return false;

            Texture2D tex = SHelper.GameContent.Load<Texture2D>($"Characters/{name}");
            //SMonitor.Log($"tex height for {name}: {tex.Height}");

            if (sleepidx / 4 * 32 >= tex.Height)
            {
                return false;
            }
            return true;
        }

        private static string SleepAnimation(string name)
        {
            string anim = null;
            if (Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions").ContainsKey(name.ToLower() + "_sleep"))
            {
                anim = Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions")[name.ToLower() + "_sleep"];
            }
            else if (Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions").ContainsKey(name + "_Sleep"))
            {
                anim = Game1.content.Load<Dictionary<string, string>>("Data\\animationDescriptions")[name + "_Sleep"];
            }
            return anim;
        }


        internal static void NPCDoAnimation(NPC npc, string npcAnimation)
        {
            Dictionary<string, string> animationDescriptions = SHelper.GameContent.Load<Dictionary<string, string>>("Data\\animationDescriptions");
            if (!animationDescriptions.ContainsKey(npcAnimation))
                return;

            string[] rawData = animationDescriptions[npcAnimation].Split('/');
            var animFrames = Utility.parseStringToIntArray(rawData[1], ' ');
 
            List<FarmerSprite.AnimationFrame> anim = new List<FarmerSprite.AnimationFrame>();
            for (int i = 0; i < animFrames.Length; i++)
            {
                    anim.Add(new FarmerSprite.AnimationFrame(animFrames[i], 100, 0, false, false, null, false, 0));
            }
            SMonitor.Log($"playing animation {npcAnimation} for {npc.Name}");
            npc.Sprite.setCurrentAnimation(anim);
        }

        public static void ResetSpouses(Farmer f, bool force = false)
        {
            if (force)
            {
                currentSpouses.Remove(f.UniqueMultiplayerID);
                currentUnofficialSpouses.Remove(f.UniqueMultiplayerID);
            }
            Dictionary<string, NPC> spouses = GetSpouses(f,true);
            if (f.spouse == null)
            {
                if(spouses.Count > 0)
                {
                    SMonitor.Log("No official spouse, setting official spouse to: " + spouses.First().Key);
                    f.spouse = spouses.First().Key;
                }
            }

            foreach (string name in f.friendshipData.Keys)
            {
                if (f.friendshipData[name].IsEngaged())
                {
                    SMonitor.Log($"{f.Name} is engaged to: {name} {f.friendshipData[name].CountdownToWedding} days until wedding");
                    if (f.friendshipData[name].WeddingDate.TotalDays < new WorldDate(Game1.Date).TotalDays)
                    {
                        SMonitor.Log("invalid engagement: " + name);
                        f.friendshipData[name].WeddingDate.TotalDays = new WorldDate(Game1.Date).TotalDays + 1;
                    }
                    if(f.spouse != name)
                    {
                        SMonitor.Log("setting spouse to engagee: " + name);
                        f.spouse = name;
                    }
                }
                if (f.friendshipData[name].IsMarried() && f.spouse != name)
                {
                    //SMonitor.Log($"{f.Name} is married to: {name}");
                    if (f.spouse != null && f.friendshipData[f.spouse] != null && !f.friendshipData[f.spouse].IsMarried() && !f.friendshipData[f.spouse].IsEngaged())
                    {
                        SMonitor.Log("invalid ospouse, setting ospouse to " + name);
                        f.spouse = name;
                    }
                    if (f.spouse == null)
                    {
                        SMonitor.Log("null ospouse, setting ospouse to " + name);
                        f.spouse = name;
                    }
                }
            }
            ReloadSpouses(f);
        }
        public static void SetAllNPCsDatable()
        {
            if (!Config.RomanceAllVillagers)
                return;
            Farmer f = Game1.player;
            if (f == null)
            {
                return;
            }
            foreach (string friend in f.friendshipData.Keys)
            {
                NPC npc = Game1.getCharacterFromName(friend);
                if (npc is not null && !npc.datable.Value && npc is not Child && (npc.Age == 0 || npc.Age == 1))
                {
                    SMonitor.Log($"Making {npc.Name} datable.");
                    npc.datable.Value = true;
                }
            }
        }

        public static bool IsMarried(NPC npc, Farmer farmer)
        {
            return farmer.friendshipData.TryGetValue(npc.Name, out Friendship friendship) && friendship.IsMarried();
        }
        public static bool IsMarried(string npc, Farmer farmer)
        {
            return farmer.friendshipData.TryGetValue(npc, out Friendship friendship) && friendship.IsMarried();
        }

        public static void ShuffleList<T>(ref List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = myRand.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        public static void ShuffleList<T>(ref List<T> list, int count)
        {
            int n = list.Count;
            while (n > Math.Max(1, list.Count - count))
            {
                n--;
                int k = myRand.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }

            list.Reverse();
        }

        public static void ShuffleDic<T1,T2>(ref Dictionary<T1,T2> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = myRand.Next(n + 1);
                (list[list.Keys.ToArray()[n]], list[list.Keys.ToArray()[k]]) = (list[list.Keys.ToArray()[k]], list[list.Keys.ToArray()[n]]);
            }
        }

        private static bool HasThingToLookAt(GameLocation location, Vector2 point)
        {
            return location.IsTileOccupiedBy(point, collisionMask: CollisionMask.Furniture | CollisionMask.Objects, ignorePassables: CollisionMask.All);
        }

        public readonly struct SpotDirection
        {
            public static readonly SpotDirection None = new(Vector2.Zero, Game1.down);

            public readonly Vector2 Spot;
            public readonly int Direction;

            public SpotDirection(Vector2 spot, int direction)
            {
                Spot = spot;
                Direction = direction;
            }
        }

        /// <summary>
        /// Returns a point and a direction in the given FarmHouse such that a character standing at that point and facing that direction is looking at some non-passable funriture 
        /// or object.
        /// </summary>
        public static SpotDirection GetRandomGoodSpotInFarmhouse(FarmHouse house)
        {
            const int tries = 30;

            for (int i = 0; i < tries; i++)
            {
                Vector2 result = new Vector2(myRand.Next(house.Map.Layers[0].LayerWidth), myRand.Next(house.Map.Layers[0].LayerHeight));

                int x = (int)result.X;
                int y = (int)result.Y;

                if (house.getTileIndexAt(x, y, "Back") == -1 || !house.CanItemBePlacedHere(result) || house.isTileOnWall(x, y) || house.getTileIndexAt(x, y, "Back") == 0 && house.getTileSheetIDAt(x, y, "Back") == "indoor")
                {
                    continue;
                }

                foreach(var point in Utility.getAdjacentTileLocationsArray(result))
                {
                    if(HasThingToLookAt(house, point))
                    {
                        return new(result, Utility.getDirectionFromChange(point, result));
                    }
                }
            }

            SMonitor.Log("Failed to find good spot :(");
            return new(house.getRandomOpenPointInHouse(myRand).ToVector2(), myRand.Next(4));
        }

        /// <summary>
        /// Tries to load the given child's parent's name from its mod data, if that fails, it defaults to it's player parent's official spouse.
        /// </summary>
        public static bool TryGetNPCParent(Child child, out string parentName)
        {
            // Note that getFarmer will fallback to Game1.MasterPlayer so we don't need to check null on it.
            parentName = child.modData.TryGetValue("aedenthorn.FreeLove/OtherParent", out string npcParent) ? npcParent : Game1.getFarmer(child.idOfParent.Value).spouse;

            return !string.IsNullOrEmpty(parentName);
        }

        /// <summary>
        /// Gets all of the children of the given NPC with their spouse as listed in npc.getSpouse().
        /// </summary>
        public static List<Child> GetChildren(NPC npc)
        {
            if(npc.getSpouse() is null)
            {
                return new();
            }

            // Finds all children in the npc's spouse's children where we can find the child's parent and the parent is the npc.
            return npc.getSpouse().getChildren().FindAll(child => TryGetNPCParent(child, out string npcParent) && npcParent == npc.Name);
        }

        public static Point? GetSpouseRoomSpot(NPC npc)
        {
            if (npc.getSpouse() is not Farmer spouse || spouse.HouseUpgradeLevel < 1)
            {
                return null;
            }

            // The official spouse always has a spouse room
            if (npc.Name == spouse.spouse)
            {
                return Utility.getHomeOfFarmer(spouse).GetSpouseRoomSpot();
            }

            if (CustomSpouseRoomsAPI is null)
            {
                return null;
            }

            return CustomSpouseRoomsAPI.GetSpouseTile(npc);
        }

        public static bool HasSpouseRoom(NPC npc)
        {
            return GetSpouseRoomSpot(npc).HasValue;
        }

        /// <summary>
        /// Checks the given tile for if it is occupied by a NPC other than the first argument.
        /// </summary>
        public static bool IsTileOccupiedByCharacterOtherThan(NPC npc, GameLocation location, Vector2 tile)
        {
            return GetOtherCharacterOccupyingTile(npc, location, tile) is not null;
        }

        /// <summary>
        /// Checks the given tile for if it is occupied by a NPC other than the first argument.
        /// 
        /// Almost entirely lifted from GameLocation.IsTileOccupiedBy
        /// </summary>
        /// <returns>
        /// The NPC occupying the tile, or null if there is no NPC (ignoring the given one)
        /// </returns>
        public static NPC GetOtherCharacterOccupyingTile(NPC npc, GameLocation location, Vector2 tile)
        {
            Rectangle tileRect = new Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
            foreach (NPC character in location.characters)
            {
                if (character is not null && character.Name != npc?.Name && character.GetBoundingBox().Intersects(tileRect) && !character.IsInvisible)
                {
                    SMonitor.Log($"Tile occupied by {character.Name}!");
                    return character;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks whether the given position is a valid (unobstructed) place for the given NPC to stand.
        /// If requireFlooringIndoors is true, will also check that they are standing on a floorable tile when in a DecoratableLocation so that positions inside walls aren't marked valid.
        /// </summary>
        public static bool CanNPCStandHere(NPC npc, GameLocation location, Vector2 tile, bool requireFlooringIndoors = true)
        {
            // Check if it is occupied by anything other than characters and the player, ignoring all pasables
            bool hasObstacle = location.IsTileOccupiedBy(tile, ~(CollisionMask.Characters | CollisionMask.Farmers), CollisionMask.All);

            // Check if it is occupied by another NPC
            hasObstacle |= IsTileOccupiedByCharacterOtherThan(npc, location, tile);

            if(hasObstacle || location is not DecoratableLocation decorable)
            {
                return !hasObstacle;
            }

            // If it is indoors, also check that the spot has flooring, so that the NPC doesn't stand in tiles in the wall that don't have the buildings layer
            return !hasObstacle && (!requireFlooringIndoors || decorable.GetFloorID((int)tile.X, (int)tile.Y) is not null);
        }

        /// <summary>
        /// Gets an unoccupied spot in front of this location's built-in fridge or a placed mini-fridge
        /// </summary>
        public static Vector2 GetUnoccupiedSpotInFrontOfFridge(GameLocation location)
        {
            // Checks the fridge that is part of the map (if it exists)
            if (location.GetFridgePosition() is Point fridgePoint)
            {
                Vector2 fridgeVector = fridgePoint.ToVector2();
                fridgeVector.Y++;
                
                if (!CanNPCStandHere(null, location, fridgeVector, requireFlooringIndoors: false))
                {
                    SMonitor.Log("Found unoccupied spot in front of base fridge!");
                    return fridgeVector;
                }
            }

            // Checks placed mini-fridges
            foreach (Object item in location.Objects.Values)
            {
                if (item is null || !item.bigCraftable.Value || item is not Chest chest || !chest.fridge.Value)
                {
                    continue;
                }

                // We don't just use ches.TileLocation so that we don't modify it with the ++.
                Vector2 fridgeVector = new(chest.TileLocation.X, chest.TileLocation.Y);
                fridgeVector.Y++;

                if (!location.IsTileOccupiedBy(fridgeVector, ~(CollisionMask.Farmers), CollisionMask.All))
                {
                    SMonitor.Log("Found unoccupied spot in front of placed fridge!");
                    return fridgeVector;
                }
            }

            return Vector2.Zero;
        }

        public static SpotDirection GetCurrentSpotDirection(NPC npc)
        {
            return new(npc.Tile, npc.FacingDirection);
        }

        /// <summary>
        /// Moves the given NPC to a new open position in their current location.
        /// Uses <see cref="TryFindNewStandingSpotFor(NPC, out SpotDirection)"/> to get the location.
        /// </summary>
        /// <returns> True if it was able to find an open spot (even if the NPC was not moved). </returns>
        public static bool MoveToNewStandingSpot(NPC npc)
        {
            if(TryFindNewStandingSpotFor(npc, out SpotDirection spotDirection))
            {
                npc.setTileLocation(spotDirection.Spot);
                npc.faceDirection(spotDirection.Direction);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to find a new place within its current location for the given NPC to stand.
        /// The following options will be tried: <br/>
        /// If the NPC is in the Farm: <br/>
        ///  - Facing a random machine or chest within a 15-tile radius of the porch spot <br/>
        /// <br/> 
        /// If the NPC is in the FarmHouse: <br/>
        ///  - If they have an item grab dialogue (giving a breakfast meal), in front of the kitchen or a fridge. <br/>
        ///  - In front of a random Furniture, using <see cref="FindFurnitureToLookAt(GameLocation)"/> <br/>
        /// </summary>
        /// <param name="npc">The NPC to find a new spot for.</param>
        /// <param name="spotDirection">The position and direction the NPC should be moved to. May be its current location. </param>
        /// <returns>True if it found a valid spot, or false if if it failed to. </returns>
        public static bool TryFindNewStandingSpotFor(NPC npc, out SpotDirection spotDirection)
        {
            GameLocation location = npc.currentLocation;
            spotDirection = SpotDirection.None;

            if(location is not (Farm or FarmHouse))
            {
                SMonitor.Log($"The NPC is not in a Farm or Farmhouse! Not changing their location ({location.Name}).");
                return false;
            }

            if(location is Farm)
            {
                Vector2 porchSpot = npc.getHome() is FarmHouse ? (npc.getHome() as FarmHouse).getPorchStandingSpot().ToVector2() : Vector2.Zero;

                if(porchSpot == Vector2.Zero)
                {
                    return false;
                }

                const int shuffleCount = 30;

                List<Object> objects = location.Objects.Values.ToList();

                ShuffleList(ref objects, shuffleCount);

                // Try to find a mchine for them to look at
                for (int i = 0; i < Math.Min(shuffleCount, objects.Count); i++)
                {
                    Object item = objects[i];

                    // If the Object is not a machine, continue
                    if (item?.GetMachineData() is null && item is not Chest)
                    {
                        continue;
                    }

                    // If the machine is more than 20 tiles from the porch spot, continue
                    if (Vector2.DistanceSquared(item.TileLocation, porchSpot) > 15 * 15)
                    {
                        continue;
                    }

                    // Try to find a valid standing spot in the tiles adjacent to the machine
                    foreach (Vector2 standingSpot in Utility.getAdjacentTileLocationsArray(item.TileLocation))
                    {
                        if (!CanNPCStandHere(npc, location, standingSpot))
                        {
                            continue;
                        }

                        SMonitor.Log($"Placing {npc.Name} in front of machine {item.Name}.");

                        spotDirection = new(standingSpot, Utility.getDirectionFromChange(item.TileLocation, standingSpot));
                        return true;
                    }
                }
            }

            if(location is FarmHouse)
            {
                // The item grab dialogue is them giving you a meal for breakfast. If they are doing that, they need to be at the kitchen.
                // If they have that dialogue, they should be at the kitchen standing spot
                if(npc.currentMarriageDialogue.Count > 0 && npc.currentMarriageDialogue[0].IsItemGrabDialogue(npc))
                {
                    NPC otherNPC = GetOtherCharacterOccupyingTile(npc, location, npc.Tile);

                    if(otherNPC is null)
                    {
                        spotDirection = GetCurrentSpotDirection(npc);
                        return true;
                    }

                    // If the other NPC is not giving a breakfast meal, move them from in front of the fridge
                    if(otherNPC.currentMarriageDialogue.Count <= 0 || !otherNPC.currentMarriageDialogue[0].IsItemGrabDialogue(otherNPC))
                    {
                        SMonitor.Log($"Moving {otherNPC.Name} for {npc.Name}.");
                        MoveToNewStandingSpot(npc);

                        spotDirection = GetCurrentSpotDirection(npc);
                        return true;
                    }
                    else
                    {
                        Vector2 newSpot = GetUnoccupiedSpotInFrontOfFridge(location);
                        if(newSpot != Vector2.Zero)
                        {
                            SMonitor.Log($"Placing {npc.Name} in front of a fridge.");

                            spotDirection = new(newSpot, Game1.up);
                            return true;
                        }
                    }
                }

                float random = myRand.NextSingle();

                if(random < 0.5f && HasSpouseRoom(npc))
                {
                    SMonitor.Log($"Placing {npc.Name} in their room.");
                    spotDirection = new(GetSpouseRoomSpot(npc).Value.ToVector2(), Game1.up);
                    return true;
                }
                else if(random < 0.75f && (spotDirection = FindFurnitureToLookAt(location)).Spot != SpotDirection.None.Spot)
                {
                    SMonitor.Log($"Placing {npc.Name} in front of furniture.");
                    return true;
                }
                else
                {
                    SMonitor.Log($"Placing {npc.Name} in bed.");
                    spotDirection = new(GetSpouseBedPosition(location as FarmHouse, npc.Name), Game1.down);
                }
            }

            return false;
        }

        public static SpotDirection FindFurnitureToLookAt(GameLocation location)
        {
            const int shuffleCount = 30;

            List<Furniture> furniture = location.furniture.ToList();

            ShuffleList(ref furniture, shuffleCount);

            for(int i = 0; i < shuffleCount; i++)
            {
                Furniture furniturei = furniture[i];

                if (furniturei is null)
                {
                    continue;
                }

                Vector2 standingSpot;

                switch (furniturei.furniture_type.Value)
                {
                    case Furniture.window:
                        standingSpot = new(furniturei.TileLocation.X, furniturei.TileLocation.Y + 1);
                        if (!CanNPCStandHere(null, location, standingSpot))
                        {
                            break;
                        }

                        SMonitor.Log($"Found spot in front of {furniturei.Name}.");
                        return new(standingSpot, Utility.getDirectionFromChange(furniturei.TileLocation, standingSpot));
                    case Furniture.table:
                    case Furniture.longTable:
                        Vector2 baseSpot = furniturei.TileLocation;
                        List<SpotDirection> standingSpots = new();
                        for(int j = 0; j < furniturei.getTilesWide(); j++)
                        {
                            standingSpots.Add(new(new(baseSpot.X + j, baseSpot.Y - 1), Game1.down));
                            standingSpots.Add(new(new(baseSpot.X + j, baseSpot.Y + furniturei.getTilesHigh()), Game1.up));
                        }

                        for(int j = 0; j < furniturei.getTilesHigh(); j++)
                        {
                            standingSpots.Add(new(new(baseSpot.X - 1, baseSpot.Y + j), Game1.right));
                            standingSpots.Add(new(new(baseSpot.X + furniturei.getTilesWide(), baseSpot.Y + j), Game1.left));
                        }

                        ShuffleList(ref standingSpots);
                        foreach(var spotDirection in standingSpots)
                        {
                            if (!CanNPCStandHere(null, location, spotDirection.Spot))
                            {
                                continue;
                            }

                            SMonitor.Log($"Found spot in front of {furniturei.Name}.");
                            return spotDirection;
                        }
                        break;
                    case Furniture.painting:
                        standingSpot = new(furniturei.TileLocation.X, furniturei.TileLocation.Y + 1);

                        for(int j = 0; j < furniturei.getTilesWide(); j++)
                        {
                            if (!CanNPCStandHere(null, location, standingSpot))
                            {
                                standingSpot.X++;
                                continue;
                            }

                            SMonitor.Log($"Found spot in front of {furniturei.Name}.");
                            return new(standingSpot, Game1.up);
                        }

                        break;
                    case Furniture.bookcase:
                    case Furniture.dresser:
                        bool isFacingHorizontal = furniturei.currentRotation.Value % 2 == 0;
                        int offset = furniturei.currentRotation.Value switch
                        {
                            0 => furniturei.getTilesHigh(), // Facing down
                            1 => furniturei.getTilesWide(), // Facing right
                            2 => -1, // Facing up
                            3 => -1, // Facing left
                            _ => -1 // nonsense value
                        };

                        List<Vector2> standingSpots1 = new();

                        for (int j = 0; j < (isFacingHorizontal ? furniturei.getTilesWide() : furniturei.getTilesHigh()); j++)
                        {
                            standingSpots1.Add(new(
                                furniturei.TileLocation.X + (isFacingHorizontal ? j : offset),
                                furniturei.TileLocation.Y + (isFacingHorizontal ? offset : j)));
                        }

                        ShuffleList(ref standingSpots1);

                        foreach(Vector2 spot in standingSpots1)
                        {
                            if (!CanNPCStandHere(null, location, spot))
                            {
                                continue;
                            }

                            SMonitor.Log($"Found spot in front of {furniturei.Name}.");
                            return new(spot, furniturei.currentRotation.Value switch
                            {
                                0 => 0,
                                1 => 3,
                                2 => 2,
                                3 => 1,
                                _ => 2
                            });
                        }

                        break;
                }
            }

            return SpotDirection.None;
        }
    }
}