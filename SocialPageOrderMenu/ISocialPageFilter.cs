using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace SocialPageOrderRedux
{
    public interface ISocialPageFilter
    {
        /// <summary>
        /// A name for this filter. This is only used internally and should be unique from any other filter name. 
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Whether this filter is currently enabled. 
        /// </summary>
        /// <remarks>
        /// Mostly intended for enabling/disabling through the config. There may be visual issues if you change this while the Social Page is active.
        /// </remarks>
        public bool Enabled { get; }

        /// <summary>
        /// The x offset of this filter in the column of filters.
        /// </summary>
        /// <remarks>
        /// This should almost always be 0.
        /// </remarks>
        public int OffsetX { get; }

        /// <summary>
        /// The y offset of this filter in the column of filters.
        /// </summary>
        /// <remarks>
        /// This should almost always be 0.
        /// </remarks>
        public int OffsetY { get; }

        /// <summary>
        /// The name of the texture where the icon for this filter can be found (like LooseSprites/Cursors2)
        /// </summary>
        public string TextureName { get; }

        /// <summary>
        /// The location of the filter's icon witin the texture.
        /// </summary>
        /// <remarks>
        /// The filters in the base mod are all 12 wide x 11 tall. In general, any added icons should be around this size; for a smaller icon, I would reccomend padding it to 12x11 so that the spacing is consistent.
        /// </remarks>
        public Rectangle SourceRectangle { get; }

        /// <summary>
        /// Gets the text to be displayed when the player hoves their cursor over the icon.
        /// </summary>
        /// <param name="isFiltering">Whether or not this filter is being applied currently. </param>
        /// <returns>The text to be displayed when the player hoves their cursor over the icon.</returns>
        /// <remarks>
        /// This should be translated. See the examples in <see cref="Filters"/> for how I have implemented this.
        /// </remarks>
        public string HoverText(bool isFiltering);

        /// <summary>
        /// Whether or not this filter should filter the given entry.
        /// </summary>
        /// <param name="entry">The entry to (maybe) filter.</param>
        /// <returns>Whether the entry should be filtered (not displayed) based on the rules of this filter.</returns>
        /// <remarks>
        /// Be sure to check for null as necessary. In particular, <see cref="SocialPage.SocialEntry.Friendship"/> will be null if the character is unmet.
        /// </remarks>
        public bool ShouldFilter(SocialPage.SocialEntry entry);
    }
}
