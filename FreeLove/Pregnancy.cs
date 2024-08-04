using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Characters;
using StardewValley.Events;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FreeLove
{
    public partial class ModEntry
    {

        public static bool Utility_pickPersonalFarmEvent_Prefix(ref FarmEvent __result)
        {
            if (!Config.EnableMod)
                return true;
            SMonitor.Log("picking event");
            if (Game1.weddingToday)
            {
                __result = null;
                return false;
            }



            List<NPC> allSpouses = GetSpouses(Game1.player,true).Values.ToList();

            ShuffleList(ref allSpouses);
            
            foreach (NPC spouse in allSpouses)
            {
                if (spouse == null)
                {
                    SMonitor.Log($"Utility_pickPersonalFarmEvent_Prefix spouse is null");
                    continue;
                }
                Farmer f = spouse.getSpouse();

                if (!Game1.player.friendshipData.TryGetValue(spouse.Name, out Friendship friendship))
                    continue;

                if (friendship.DaysUntilBirthing <= 0 && friendship.NextBirthingDate != null)
                {
                    LastPregnantSpouse = null;
                    LastBirthingSpouse = spouse;
                    __result = new BirthingEvent();
                    return false;
                }
            }

            if (PlannedParenthoodAPI is not null && PlannedParenthoodAPI.GetPartnerTonight() is not null)
            {
                SMonitor.Log($"Handing farm sleep event off to Planned Parenthood");
                return true;
            }

            LastBirthingSpouse = null;
            LastPregnantSpouse = null;

            foreach (NPC spouse in allSpouses)
            {
                if (spouse is null)
                    continue;

                if (!Game1.player.friendshipData.TryGetValue(spouse.Name, out Friendship friendship) && friendship.IsMarried())
                    continue;

                if (!Config.RoommateRomance && Game1.player.friendshipData[spouse.Name].RoommateMarriage)
                    continue;

                int heartsWithSpouse = Game1.player.getFriendshipHeartLevelForNPC(spouse.Name);
                List<Child> kids = Game1.player.getChildren();
                int maxChildren = ChildrenAPI == null ? Config.MaxChildren : ChildrenAPI.GetMaxChildren();
                FarmHouse fh = Utility.getHomeOfFarmer(Game1.player);
                // Days after last birth starts at 5 when giving birth and then goes down by one each day after until -1. That check isn't in the normal method, but I'll leave it.
                bool can = spouse.daysAfterLastBirth <= 0 && fh.cribStyle.Value > 0 && fh.upgradeLevel >= 2 && friendship.DaysUntilBirthing < 0 && heartsWithSpouse >= 10 && friendship.DaysMarried >= 7 && (kids.Count < maxChildren) && GameStateQuery.CheckConditions(spouse.GetData()?.SpouseWantsChildren);
                SMonitor.Log($"Checking ability to get pregnant: {spouse.Name} {can}:{(fh.cribStyle.Value > 0 ? $" no crib":"")}{(Utility.getHomeOfFarmer(Game1.player).upgradeLevel < 2 ? $" house level too low {Utility.getHomeOfFarmer(Game1.player).upgradeLevel}":"")}{(friendship.DaysMarried < 7 ? $", not married long enough {friendship.DaysMarried}":"")}{(friendship.DaysUntilBirthing >= 0 ? $", already pregnant (gives birth in: {friendship.DaysUntilBirthing})":"")}");
                if (can && Game1.player.currentLocation == Game1.getLocationFromName(Game1.player.homeLocation.Value) && myRand.NextDouble() <  0.05)
                {
                    SMonitor.Log("Requesting a baby!");
                    LastPregnantSpouse = spouse;
                    __result = new QuestionEvent(1);
                    return false;
                }
            }
            return true;
        }

        private static readonly PerScreen<NPC> lastPregnantSpouse = new(() => null);
        public static NPC LastPregnantSpouse { get => lastPregnantSpouse.Value; set => lastPregnantSpouse.Value = value; }

        private static readonly PerScreen<NPC> lastBirthingSpouse = new(() => null);
        private static NPC LastBirthingSpouse { get => lastBirthingSpouse.Value; set => lastBirthingSpouse.Value = value; }

        public static bool QuestionEvent_setUp_Prefix(int ___whichQuestion, ref bool __result)
        {
            if(!Config.EnableMod || ___whichQuestion != 1)
                return true;

            if (LastPregnantSpouse is null)
            {
                SMonitor.Log("No LastPregnantSpouse in QuestionEvent.setUp");
                __result = true;
                return false;
            }

            TempOfficialSpouse = LastPregnantSpouse;

            return true;
        }

        public static void QuestionEvent_setUp_Postfix(int ___whichQuestion)
        {
            if (!Config.EnableMod || ___whichQuestion != 1)
                return;

            TempOfficialSpouse = null;
        }

        public static void QuestionEvent_answerPregnancyQuestion_Prefix()
        {
            if (!Config.EnableMod)
                return;

            TempOfficialSpouse = LastPregnantSpouse;
        }

        public static void QuestionEvent_answerPregnancyQuestion_Postfix()
        {
            if (!Config.EnableMod)
                return;

            TempOfficialSpouse = null;
        }

        /// <summary>
        /// Unfortunately, I don't think I could turn this into a smaller prefix/postfix without a transpiler too, so I'm going to just leave it as-is
        /// </summary>
        public static bool BirthingEvent_tickUpdate_Prefix(GameTime time, BirthingEvent __instance, ref bool __result, ref int ___timer, ref bool ___naming, bool ___getBabyName, bool ___isMale, string ___babyName)
        {
            if (!Config.EnableMod)
                return true;

            if (!___getBabyName)
                return true;

            Game1.player.CanMove = false;
            ___timer += time.ElapsedGameTime.Milliseconds;
            Game1.fadeToBlackAlpha = 1f;

            if (!___naming)
            {
                Game1.activeClickableMenu = new NamingMenu(__instance.returnBabyName, Game1.content.LoadString(___isMale ? "Strings\\Events:BabyNamingTitle_Male" : "Strings\\Events:BabyNamingTitle_Female"), "");
                ___naming = true;
            }
            if (!string.IsNullOrEmpty(___babyName) && ___babyName.Length > 0)
            {
                NPC spouse = LastBirthingSpouse ?? Game1.player.getSpouse();
                double chance = (spouse.hasDarkSkin() ? 0.5 : 0.0) + (Game1.player.hasDarkSkin() ? 0.5 : 0.0);
                bool isDarkSkinned = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed).NextBool(chance);
                string newBabyName = ___babyName;
                List<NPC> all_characters = Utility.getAllCharacters();
                bool collision_found;
                do
                {
                    collision_found = false;
                    if (Game1.characterData.ContainsKey(newBabyName))
                    {
                        newBabyName += " ";
                        collision_found = true;
                        continue;
                    }
                    foreach (NPC item in all_characters)
                    {
                        if (item.Name == newBabyName)
                        {
                            newBabyName += " ";
                            collision_found = true;
                            break;
                        }
                    }
                }
                while (collision_found);
                Child baby = new Child(newBabyName, ___isMale, isDarkSkinned, Game1.player);
                baby.Age = 0;
                baby.Position = new Vector2(16f, 4f) * 64f + new Vector2(0f, -24f);

                baby.modData["aedenthorn.FreeLove/OtherParent"] = spouse.Name;

                Utility.getHomeOfFarmer(Game1.player).characters.Add(baby);
                Game1.playSound("smallSelect");
                spouse.daysAfterLastBirth = 5;
                Game1.player.friendshipData[spouse.Name].NextBirthingDate = null;
                if (Game1.player.getChildrenCount() >= 2)
                {
                    spouse.shouldSayMarriageDialogue.Value = true;
                    spouse.currentMarriageDialogue.Insert(0, new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_SecondChild" + Game1.random.Next(1, 3), true));
                    Game1.getSteamAchievement("Achievement_FullHouse");
                }
                else if (spouse.isAdoptionSpouse())
                {
                    spouse.currentMarriageDialogue.Insert(0, new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_Adoption", true, ___babyName));
                }
                else
                {
                    spouse.currentMarriageDialogue.Insert(0, new MarriageDialogueReference("Data\\ExtraDialogue", "NewChild_FirstChild", true, ___babyName));
                }
                Game1.morningQueue.Enqueue(delegate
                {
                    string text = Game1.getCharacterFromName(spouse?.Name)?.GetTokenizedDisplayName() ?? spouse?.Name ?? Game1.player.spouse;
                    Game1.Multiplayer.globalChatInfoMessage("Baby", Lexicon.capitalize(Game1.player.Name), text, Lexicon.getTokenizedGenderedChildTerm(___isMale), Lexicon.getTokenizedPronoun(___isMale), baby.displayName);
                });
                if (Game1.keyboardDispatcher != null)
                {
                    Game1.keyboardDispatcher.Subscriber = null;
                }
                Game1.player.Position = Utility.PointToVector2(Utility.getHomeOfFarmer(Game1.player).GetPlayerBedSpot()) * 64f;
                Game1.globalFadeToClear();

                __result = true;
                return false;
            }

            __result = false;
            return false;
        }
        
        public static bool BirthingEvent_setUp_Prefix()
        {
            if (!Config.EnableMod)
                return true;

            if(LastBirthingSpouse is null)
            {
                SMonitor.Log("No LastBirthingSpouse in BirthingEvent.setUp");
                return false;
            }

            TempOfficialSpouse = LastBirthingSpouse;

            return true;
        }

        public static void BirthingEvent_setUp_Postfix(ref bool ___isMale)
        {
            if (!Config.EnableMod)
                return;

            TempOfficialSpouse = null;

            ___isMale = myRand.NextBool();
        }
    }
}
