using StardewValley.SpecialOrders;
using StardewValley;

namespace LongerSeasons
{
    partial class ModEntry
    {
        private static int SpecialOrder_GetDaysLeft_PostFix(int _, SpecialOrder __instance)
        {
            var dueDate = new WorldDate
            {
                TotalDays = __instance.dueDate.Value
            };
            return Utilities.GetDaysAway(dueDate);
        }
    }
}
