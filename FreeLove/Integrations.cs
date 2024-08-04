using StardewModdingAPI;
using StardewValley;
using System;
using System.Linq;

namespace FreeLove
{
    public partial class ModEntry
    {
        public static IKissingAPI KissingAPI { get; private set; }
        public static IBedTweaksAPI BedTweaksAPI { get; private set; }
        public static IChildrenTweaksAPI ChildrenAPI { get; private set; }
        public static ICustomSpouseRoomsAPI CustomSpouseRoomsAPI { get; private set; }
        public static IPlannedParenthoodAPI PlannedParenthoodAPI { get; private set; }
        public static IContentPatcherAPI ContentPatcherAPI { get; private set; }

        public void LoadModApis()
        {
            KissingAPI = SHelper.ModRegistry.GetApi<IKissingAPI>("aedenthorn.HugsAndKisses");
            BedTweaksAPI = SHelper.ModRegistry.GetApi<IBedTweaksAPI>("aedenthorn.BedTweaks");
            ChildrenAPI = SHelper.ModRegistry.GetApi<IChildrenTweaksAPI>("aedenthorn.ChildrenTweaks");
            CustomSpouseRoomsAPI = SHelper.ModRegistry.GetApi<ICustomSpouseRoomsAPI>("aedenthorn.CustomSpouseRooms");
            PlannedParenthoodAPI = SHelper.ModRegistry.GetApi<IPlannedParenthoodAPI>("aedenthorn.PlannedParenthood");

            if (KissingAPI != null)
            {
                SMonitor.Log("Kissing API loaded");
            }
            if (BedTweaksAPI != null)
            {
                SMonitor.Log("BedTweaks API loaded");
            }
            if (ChildrenAPI != null)
            {
                SMonitor.Log("ChildrenTweaks API loaded");
            }
            if (CustomSpouseRoomsAPI != null)
            {
                SMonitor.Log("CustomSpouseRooms API loaded");
            }
            if (PlannedParenthoodAPI != null)
            {
                SMonitor.Log("PlannedParenthood API loaded");
            }
            ContentPatcherAPI = SHelper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");
            ContentPatcherAPI?.RegisterToken(ModManifest, "PlayerSpouses", () =>
            {
                Farmer player;

                if (Context.IsWorldReady)
                    player = Game1.player;
                else if (SaveGame.loaded?.player != null)
                    player = SaveGame.loaded.player;
                else
                    return null;

                var spouses = GetSpouses(player, true).Keys.ToList();
                spouses.Sort(delegate (string a, string b) {
                    player.friendshipData.TryGetValue(a, out Friendship af);
                    player.friendshipData.TryGetValue(b, out Friendship bf);
                    if (af == null && bf == null)
                        return 0;
                    if (af == null)
                        return -1;
                    if (bf == null)
                        return 1;
                    if (af.WeddingDate == bf.WeddingDate)
                        return 0;
                    return af.WeddingDate > bf.WeddingDate ? -1 : 1;
                });
                return spouses.ToArray();
            });
        }
    }
}