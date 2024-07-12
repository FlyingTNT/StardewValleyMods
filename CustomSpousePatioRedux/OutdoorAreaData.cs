using Microsoft.Xna.Framework;
using StardewValley;
using System.Collections.Generic;

namespace CustomSpousePatioRedux
{
    public class OutdoorAreaData
    {
        /// <summary>
        /// LEGACY; Will automatically be migrated to the dict
        /// Spouse name -> Top-left corner
        /// </summary>
        public Dictionary<string, Vector2> areas;

        /// <summary>
        /// Spouse name -> OutdoorArea
        /// </summary>
        public Dictionary<string, OutdoorArea> dict = new Dictionary<string, OutdoorArea>();
    }

    public class OutdoorArea
    {
        public Vector2 corner;
        public string location;
    }
}