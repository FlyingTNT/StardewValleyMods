using System.Collections.Generic;
using Common.Integrations;
using StardewValley;
using StardewModdingAPI;

namespace LongerSeasons
{
    partial class ModEntry
    {
        private void SetupCPToken()
        {
            IContentPatcherAPI contentPatcherAPI = Helper.ModRegistry.GetApi<IContentPatcherAPI>(IDs.ContentPatcher);
            if (contentPatcherAPI is null)
            {
                return;
            }

            contentPatcherAPI.RegisterToken(ModManifest, "DaysPerMonth", GetDaysPerMonthForCPToken);
            contentPatcherAPI.RegisterToken(ModManifest, "MonthsInCurrentSeason", GetMonthsPerSeasonForCPToken);
            contentPatcherAPI.RegisterToken(ModManifest, "CurrentSeasonMonth", GetSeasonMonthForCPToken);
        }

        private IEnumerable<string> GetDaysPerMonthForCPToken()
        {
            if (!Context.IsWorldReady)
            {
                yield break; // Return an empty list; will mark the token as not ready
            }

            yield return Config.DaysPerMonth.ToString();
        }

        private IEnumerable<string> GetMonthsPerSeasonForCPToken()
        {
            if (!Context.IsWorldReady)
            {
                yield break; // Return an empty list; will mark the token as not ready
            }

            yield return GetMonthsInCurrentSeason().ToString();
        }

        private IEnumerable<string> GetSeasonMonthForCPToken()
        {
            if (!Context.IsWorldReady)
            {
                yield break; // Return an empty list; will mark the token as not ready
            }

            yield return CurrentSeasonMonth.ToString();
        }
    }
}
