using StardewModdingAPI;

namespace Swim
{
    public class SwimModApi : ISwimModAPI
    {
        private static IMonitor SMonitor => ModEntry.SMonitor;

        public bool IsEnabled => ModEntry.Config.EnableMod;

        public bool CanSwimHere
        {
            get => !ModEntry.LocationProhibitsSwimming;

            set => ModEntry.locationProhibitsSwimming.Value = value;
        }

        public int Oxygen => ModEntry.Oxygen;

        public int MaxOxygen => SwimUtils.MaxOxygen();

        public bool AddContentPack(IContentPack contentPack)
        {
            try
            {
                SMonitor.Log($"Reading content pack: {contentPack.Manifest.Name} {contentPack.Manifest.Version} from {contentPack.DirectoryPath}");
                DiveMapData data = contentPack.ReadJsonFile<DiveMapData>("content.json");
                SwimUtils.ReadDiveMapData(data);
                return true;
            }
            catch
            {
                SMonitor.Log($"couldn't read content.json in content pack {contentPack.Manifest.Name}", LogLevel.Warn);
                return false;
            }
        }
    }
}