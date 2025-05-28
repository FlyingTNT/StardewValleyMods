using Common.Integrations;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using static StardewValley.Menus.SocialPage;

namespace SocialPageOrderRedux
{
    public static class Filters
    {
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper => ModEntry.SHelper;

        public class TalkedFilter : ISocialPageFilter
        {
            private static readonly Rectangle sourceRect = new(181, 175, 12, 11);

            string ISocialPageFilter.Name => "ShowTalked";
            bool ISocialPageFilter.Enabled => Config.UseShowTalked;
            int ISocialPageFilter.OffsetX => 0;
            int ISocialPageFilter.OffsetY => 0;
            string ISocialPageFilter.TextureName => "LooseSprites/Cursors2";
            Rectangle ISocialPageFilter.SourceRectangle => sourceRect;

            string ISocialPageFilter.HoverText(bool isFiltering)
            {
                return SHelper.Translation.Get($"{(isFiltering ? "hiding" : "showing")}-talked"); ;
            }

            bool ISocialPageFilter.ShouldFilter(SocialEntry entry)
            {
                return entry.Friendship?.TalkedToToday ?? false;
            }
        }

        public class GiftedFilter : ISocialPageFilter
        {
            private static ICustomGiftLimitsAPI CustomGiftLimitsAPI;
            private static readonly Rectangle sourceRect = new(167, 175, 12, 11);

            public GiftedFilter()
            {
                CustomGiftLimitsAPI = SHelper.ModRegistry.GetApi<ICustomGiftLimitsAPI>(IDs.CustomGiftLimits);
            }

            string ISocialPageFilter.Name => "ShowGifted";
            bool ISocialPageFilter.Enabled => Config.UseShowGifted;
            int ISocialPageFilter.OffsetX => 0;
            int ISocialPageFilter.OffsetY => 0;
            string ISocialPageFilter.TextureName => "LooseSprites/Cursors2";

            Rectangle ISocialPageFilter.SourceRectangle => sourceRect;

            string ISocialPageFilter.HoverText(bool isFiltering)
            {
                return SHelper.Translation.Get($"{(isFiltering ? "hiding" : "showing")}-gifted");
            }

            bool ISocialPageFilter.ShouldFilter(SocialEntry entry)
            {
                return IsMaxGifted(entry);
            }

            private static bool IsMaxGifted(SocialEntry entry)
            {
                if (entry.Friendship is null)
                {
                    return false;
                }

                int perDayLimit;
                int perWeekLimit;
                if (CustomGiftLimitsAPI is not null)
                {
                    CustomGiftLimitsAPI.GetGiftLimits(entry.Friendship, out perWeekLimit, out perDayLimit);
                }
                else
                {
                    perDayLimit = 1;
                    perWeekLimit = entry.IsMarriedToCurrentPlayer() ? -1 : 2;
                }

                return ((entry.Friendship.GiftsThisWeek >= perWeekLimit && perWeekLimit >= 0) || (entry.Friendship.GiftsToday >= perDayLimit && perDayLimit >= 0)) && !(IsBirthday(entry) && entry.Friendship.GiftsToday < 1);
            }

            private static bool IsBirthday(SocialEntry entry)
            {
                if (entry.Data is null)
                {
                    return false;
                }

                return entry.Data.BirthSeason == Game1.season && entry.Data.BirthDay == Game1.dayOfMonth;
            }
        }

        public class MaxedFriendshipFilter : ISocialPageFilter
        {
            private static readonly Rectangle sourceRect = new(0, 0, 12, 11);

            string ISocialPageFilter.Name => "ShowMaxedFriendship";
            bool ISocialPageFilter.Enabled => Config.UseShowMaxFriendship;
            int ISocialPageFilter.OffsetX => 0;
            int ISocialPageFilter.OffsetY => 0;
            string ISocialPageFilter.TextureName => "Mods/FlyingTNT.SocialPageOrderRedux/MaxHeartsIcon";
            Rectangle ISocialPageFilter.SourceRectangle => sourceRect;

            string ISocialPageFilter.HoverText(bool isFiltering)
            {
                return SHelper.Translation.Get($"{(isFiltering ? "hiding" : "showing")}-max-friend"); ;
            }

            bool ISocialPageFilter.ShouldFilter(SocialEntry entry)
            {
                if(entry.Friendship is null)
                {
                    return false;
                }

                return Utility.GetMaximumHeartsForCharacter(entry.Character) * NPC.friendshipPointsPerHeartLevel <= entry.Friendship.Points;
            }
        }

        public class UnmetFilter : ISocialPageFilter
        {
            private static readonly Rectangle sourceRect = new(0, 0, 12, 11);

            string ISocialPageFilter.Name => "ShowUnmet";
            bool ISocialPageFilter.Enabled => Config.UseShowUnmet;
            int ISocialPageFilter.OffsetX => 0;
            int ISocialPageFilter.OffsetY => 0;
            string ISocialPageFilter.TextureName => "Mods/FlyingTNT.SocialPageOrderRedux/UnmetIcon";
            Rectangle ISocialPageFilter.SourceRectangle => sourceRect;

            string ISocialPageFilter.HoverText(bool isFiltering)
            {
                return SHelper.Translation.Get($"{(isFiltering ? "hiding" : "showing")}-unmet"); ;
            }

            bool ISocialPageFilter.ShouldFilter(SocialEntry entry)
            {
                return entry.Friendship is null;
            }
        }
    }
}
