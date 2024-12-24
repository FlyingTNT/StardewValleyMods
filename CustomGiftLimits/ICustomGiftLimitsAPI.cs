using StardewValley;

namespace CustomGiftLimits
{
    public interface ICustomGiftLimitsAPI
    {
        public bool ModEnabled { get;}
        public int OrdinaryGiftsPerDay { get; }
        public int FriendGiftsPerDay { get; }
        public int MaxedHeartsGiftsPerDay { get; }
        public int DatingGiftsPerDay { get; }
        public int SpouseGiftsPerDay { get; }
        public int OrdinaryGiftsPerWeek { get; }
        public int FriendGiftsPerWeek { get; }
        public int MaxedHeartsGiftsPerWeek { get; }
        public int DatingGiftsPerWeek { get; }
        public int SpouseGiftsPerWeek { get; }
        public void GetGiftLimits(Friendship friendship, out int giftsPerWeek, out int giftePerDay);
    }
}
