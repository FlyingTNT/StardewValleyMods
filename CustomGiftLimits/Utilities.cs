using StardewValley;

namespace CustomGiftLimits
{
    partial class ModEntry
    {
        private static void GetGiftLimits(Farmer farmer, NPC npc, out int giftsPerWeek, out int giftsPerDay, out FriendshipLevel level)
        {
            if (!farmer.friendshipData.TryGetValue(npc.Name, out Friendship f))
            {
                giftsPerWeek = Config.OrdinaryGiftsPerWeek;
                giftsPerDay = Config.OrdinaryGiftsPerDay;
                level = FriendshipLevel.stranger;
                return;
            }

            GetGiftLimits(f, out giftsPerWeek, out giftsPerDay, out level);
        }

        private static void GetGiftLimits(Friendship friendship, out int giftsPerWeek, out int giftsPerDay, out FriendshipLevel level)
        {
            if (friendship.IsMarried() || friendship.IsRoommate())
            {
                giftsPerWeek = Config.SpouseGiftsPerWeek;
                giftsPerDay = Config.SpouseGiftsPerDay;
                level = FriendshipLevel.spouse;
            }
            else if (friendship.IsDating())
            {
                giftsPerWeek = Config.DatingGiftsPerWeek;
                giftsPerDay = Config.DatingGiftsPerDay;
                level = FriendshipLevel.dating;
            }
            else if (friendship.Points >= 10 * NPC.friendshipPointsPerHeartLevel)
            {
                giftsPerWeek = Config.MaxedHeartsGiftsPerWeek;
                giftsPerDay = Config.MaxedHeartsGiftsPerDay;
                level = FriendshipLevel.maxed;
            }
            else if (friendship.Points >= 1500)
            {
                giftsPerWeek = Config.FriendGiftsPerWeek;
                giftsPerDay = Config.FriendGiftsPerDay;
                level = FriendshipLevel.friend;
            }
            else
            {
                giftsPerWeek = Config.OrdinaryGiftsPerWeek;
                giftsPerDay = Config.OrdinaryGiftsPerDay;
                level = FriendshipLevel.stranger;
            }
        }

        public static void SetGiftsToday(string npcName, int value)
        {
            Friendship friendship;

            if(!GiftsGiven.TryGetValue(npcName, out GiftRecord gifts))
            {
                if(Game1.player.friendshipData.TryGetValue(npcName, out friendship))
                {
                    GiftsGiven[npcName] = new(value, friendship.GiftsThisWeek);
                }
                else
                {
                    GiftsGiven[npcName] = new(value, 0);
                }
            }
            else
            {
                GiftsGiven[npcName] = new(value, gifts.GiftsThisWeek);
            }

            if(!Game1.player.friendshipData.TryGetValue(npcName, out friendship))
            {
                return;
            }

            UpdateFriendshipGiftsToday(friendship, value);
        }

        public static void SetGiftsThisWeek(string npcName, int value)
        {
            Friendship friendship;

            if (!GiftsGiven.TryGetValue(npcName, out GiftRecord gifts))
            {
                if (Game1.player.friendshipData.TryGetValue(npcName, out friendship))
                {
                    GiftsGiven[npcName] = new(friendship.GiftsToday, friendship.GiftsThisWeek);
                }
                else
                {
                    GiftsGiven[npcName] = new(0, value);
                }
            }
            else
            {
                GiftsGiven[npcName] = new(gifts.GiftsToday, value);
            }

            if (!Game1.player.friendshipData.TryGetValue(npcName, out friendship))
            {
                return;
            }

            UpdateFriendshipGiftsThisWeek(friendship, value);
        }

        public static int GetGiftsToday(string npcName)
        {
            Friendship friendship;

            if (!Config.CompatibilityMode || !GiftsGiven.TryGetValue(npcName, out GiftRecord gifts))
            {
                if(!Game1.player.friendshipData.TryGetValue(npcName, out friendship))
                {
                    return 0;
                }

                return friendship?.GiftsToday ?? 0;
            }

            if (Game1.player.friendshipData.TryGetValue(npcName, out friendship) && friendship.GiftsToday > gifts.GiftsToday)
            {
                // We don't actually do anything about this I just want to know if it's happening
                SMonitor.Log($"Discrepency between friendship ({friendship.GiftsToday}) and gifts ({gifts.GiftsToday}) gifts today.");
            }

            return gifts.GiftsToday;
        }

        public static int GetGiftsThisWeek(string npcName)
        {
            Friendship friendship;

            if (!Config.CompatibilityMode || !GiftsGiven.TryGetValue(npcName, out GiftRecord gifts))
            {
                if (!Game1.player.friendshipData.TryGetValue(npcName, out friendship))
                {
                    return 0;
                }

                return friendship?.GiftsThisWeek ?? 0;
            }

            if (Game1.player.friendshipData.TryGetValue(npcName, out friendship) && friendship.GiftsThisWeek > gifts.GiftsThisWeek)
            {
                // We don't actually do anything about this I just want to know if it's happening
                SMonitor.Log($"Discrepency between friendship ({friendship.GiftsThisWeek}) and gifts ({gifts.GiftsThisWeek}) gifts this week.");
            }

            return gifts.GiftsThisWeek;
        }

        private static void UpdateFriendshipGiftsToday(Friendship friendship, int value)
        {
            // If we aren't using compatibility mode, or we haven't hit the base game limit yet, set it to the true value
            if (!Config.CompatibilityMode || value < 1)
            {
                friendship.GiftsToday = value;
                return;
            }

            GetGiftLimits(friendship, out _, out int giftsPerDay, out _);

            // If we are using compatibility mode and we have hit the base game limit, set the value to 2 or 1 based on whether or not we have hit our limit
            friendship.GiftsToday = value >= giftsPerDay && (giftsPerDay >= 0) ? 1 : 0;
        }

        private static void UpdateFriendshipGiftsThisWeek(Friendship friendship, int value)
        {
            // If we aren't using compatibility mode, or we haven't hit the base game limit yet, set it to the true value
            if (!Config.CompatibilityMode || value < 2)
            {
                friendship.GiftsThisWeek = value;
                return;
            }

            GetGiftLimits(friendship, out int giftsPerWeek, out _, out FriendshipLevel level);

            // Special case because the game normally allows infinite gifts per week for spouses
            if(level is FriendshipLevel.spouse && giftsPerWeek >= 0 && value >= giftsPerWeek)
            {
                friendship.GiftsToday = 1;
                friendship.GiftsThisWeek = value;
                return;
            }

            // If we are using compatibility mode and we have hit the base game limit, set the value to 2 or 1 based on whether or not we have hit our limit
            friendship.GiftsThisWeek = (value >= giftsPerWeek && (giftsPerWeek >= 0)) ? 2 : 1;
        }

        public static void ReloadGiftsGiven()
        {
            foreach((string name, Friendship friendship) in Game1.player.friendshipData.Pairs)
            {
                if(!GiftsGiven.ContainsKey(name))
                {
                    GiftsGiven[name] = new(friendship.GiftsToday, friendship.GiftsThisWeek);
                }
            }

            foreach((string name, GiftRecord gifts) in GiftsGiven)
            {
                if(!Game1.player.friendshipData.TryGetValue(name, out Friendship friendship))
                {
                    continue;
                }

                UpdateFriendshipGiftsToday(friendship, gifts.GiftsToday);
                UpdateFriendshipGiftsThisWeek(friendship, gifts.GiftsThisWeek);
            }
        }
    }
}
