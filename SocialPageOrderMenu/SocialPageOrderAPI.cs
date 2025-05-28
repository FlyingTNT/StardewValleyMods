using System.Collections.Generic;
using System.Linq;

namespace SocialPageOrderRedux
{
    public class SocialPageOrderAPI : ISocialPageOrderAPI
    {
        public void AddFilter(ISocialPageFilter filter)
        {
            ModEntry.filters.Add(new SocialPageFilter(0, 0, filter, ModEntry.OnFilterClicked));
        }

        public int GetComponentId(ISocialPageFilter filter)
        {
            return ModEntry.filters.FirstOrDefault(maybeFilter => maybeFilter.Filter == filter, defaultValue: null)?.myID ?? -1;
        }

        public IEnumerable<ISocialPageFilter> GetFilters()
        {
            return ModEntry.filters.Select(filter => filter.Filter);
        }

        public bool RemoveFilter(string filterName)
        {
            return ModEntry.filters.RemoveAll(filter => filter.Filter.Name == filterName) > 0;
        }
    }
}
