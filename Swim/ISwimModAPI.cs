using StardewModdingAPI;

namespace Swim
{
    public interface ISwimModAPI
    {
        /// <summary> Whether Swim is enabled. </summary>
        public bool IsEnabled { get; }

        /// <summary> 
        /// Whether the current location allows swimming. 
        /// 
        /// Will be reset when the player warps.
        /// </summary>
        /// <remarks> 
        /// Setting this true may not allow swimming if the current location uses PoolEntry tiles, as that disables this mod. However, setting this false will always disallow swimming.
        /// If not setting this in a warp event, you should also check that the player is not swimming unless you intend to lock the player in the water.
        /// </remarks>
        public bool CanSwimHere { get; set; }

        /// <summary> The player's current oxygen value. </summary>
        public int Oxygen { get; }

        /// <summary> The player's maximum oxygen value. </summary>
        public int MaxOxygen {  get; }

        /// <summary> 
        /// Adds a dive map content pack. 
        /// </summary>
        /// <remarks>
        /// This should probably just be done with the ContentPackFor key in the pack's manifest.
        /// </remarks>
        public bool AddContentPack(IContentPack contentPack);
    }
}
