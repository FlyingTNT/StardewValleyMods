using StardewValley;

namespace LongerSeasons
{
    public interface ILongerSeasonsAPI
    {
        /// <summary>
        /// Gets the days per season as defined in the mod's config. The config is synced in multiplayer, so this method will return the effective days per season, which may or may not be the value in the local config.
        /// 
        /// This value will be null before the value is synced. When on the host computer, the value will be initialized in a SaveLoaded event with High priority. When remote, the host will send the value to the remote
        /// player when the host receives the PeerConnected event. There is no guarantee of timing for remote players.
        /// </summary>
        public int? GetDaysPerMonth();

        /// <summary>
        /// Gets the months in the given season as defined in the mod's config. The config is synced in multiplayer, so this method will return the effective months per season, which may or may not be the value in the 
        /// local config.
        /// 
        /// This value will be null before the value is synced. When on the host computer, the value will be initialized in a SaveLoaded event with High priority. When remote, the host will send the value to the remote
        /// player when the host receives the PeerConnected event. There is no guarantee of timing for remote players.
        /// </summary>
        public int? GetMonthsInSeason(Season season);

        /// <summary>
        /// Gets the month in the current season. This is stored in the save data and synced in multiplayer.
        /// 
        /// This value will be null before the value is synced. When on the host computer, the value will be initialized in a SaveLoaded event with High priority. When remote, the host will send the value to the remote
        /// player when the host receives the PeerConnected event. There is no guarantee of timing for remote players.
        /// </summary>
        public int? GetCurrentSeasonMonth();

        /// <summary>
        /// Gets the days per season as defined in the mod's config. Unlike <see cref="GetDaysPerMonth"/>, this will always return the value in the local config.
        /// </summary>
        public int GetLocalDaysPerMonth();

        /// <summary>
        /// Gets the months per season as defined in the mod's config. Unlike <see cref="GetMonthsInSeason"/>, this will always return the value in the local config.
        /// </summary>
        public int GetLocalMonthsInSeason(Season season);
    }
}
