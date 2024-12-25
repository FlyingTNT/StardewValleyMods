using StardewValley;

namespace CustomGiftLimits
{
    public class CustomGiftLimitsAPI : ICustomGiftLimitsAPI
    {
        public bool ModEnabled => ModEntry.Config.ModEnabled;

        public int OrdinaryGiftsPerDay => ModEntry.Config.OrdinaryGiftsPerDay;

        public int FriendGiftsPerDay => ModEntry.Config.FriendGiftsPerDay;

        public int MaxedHeartsGiftsPerDay => ModEntry.Config.MaxedHeartsGiftsPerDay;

        public int DatingGiftsPerDay => ModEntry.Config.DatingGiftsPerDay;

        public int SpouseGiftsPerDay => ModEntry.Config.SpouseGiftsPerDay;

        public int OrdinaryGiftsPerWeek => ModEntry.Config.OrdinaryGiftsPerWeek;

        public int FriendGiftsPerWeek => ModEntry.Config.FriendGiftsPerWeek;

        public int MaxedHeartsGiftsPerWeek => ModEntry.Config.MaxedHeartsGiftsPerWeek;

        public int DatingGiftsPerWeek => ModEntry.Config.DatingGiftsPerWeek;

        public int SpouseGiftsPerWeek => ModEntry.Config.SpouseGiftsPerWeek;
        public void GetGiftLimits(Friendship friendship, out int giftsPerWeek, out int giftsPerDay)
        {
            if (!ModEnabled)
            {
                giftsPerWeek = 2;
                giftsPerDay = 1;
                return;
            }

            ModEntry.GetGiftLimits(friendship, out giftsPerWeek, out giftsPerDay, out _);
        }
    }
}
