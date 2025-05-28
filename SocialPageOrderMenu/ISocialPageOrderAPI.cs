using System.Collections.Generic;

namespace SocialPageOrderRedux
{
    public interface ISocialPageOrderAPI
    {
        /// <summary>
        /// Adds the given <see cref="ISocialPageFilter"/> to the social page. Filters are presented in the order that they are added.
        /// </summary>
        /// <remarks>
        /// You'll want to copy ISocialPageFilter.cs, or add its contents to the API to use this.
        /// 
        /// Should generally be called in a GameLaunched event.
        /// </remarks>
        public void AddFilter(ISocialPageFilter filter);

        /// <summary>
        /// Removes the social page filter with the given name.
        /// </summary>
        /// <returns>True if the filter was removed and false otherwise.</returns>
        public bool RemoveFilter(string filterName);

        /// <summary>
        /// Gets all the filters, in the order they are displayed.
        /// </summary
        /// <remarks>
        /// This may also return disabled (but not removed) filters, so you should check <see cref="ISocialPageFilter.Enabled"/> as necessary.
        /// </remarks>
        public IEnumerable<ISocialPageFilter> GetFilters();

        /// <summary>
        /// Gets the component id for the <see cref="StardewValley.Menus.ClickableComponent"/> that the player interacts with for the given filter.
        /// </summary>
        /// <returns> The id, or -1 if it isn't enabled. </returns>
        /// <remarks>
        /// This may change as filters are enabled or disabled; it will be updated when the Social Page is created.
        /// 
        /// The first filter will have id 231445357, with each subsequent filter increasing by one.
        /// </remarks>
        public int GetComponentId(ISocialPageFilter filter);
    }
}
