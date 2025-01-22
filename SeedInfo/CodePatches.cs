using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData.Shops;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Object = StardewValley.Object;

namespace SeedInfo
{
    public partial class ModEntry
    {
        private static PerScreen<Dictionary<string, SeedEntryInfo>> shopDict = new(() => new());
        public static Dictionary<string, SeedEntryInfo> ShopDict => shopDict.Value;

        public static void ShopMenu_Constructor_Postfix(ShopMenu __instance)
        {
            if (!Config.ModEnabled)
                return;
            ShopDict.Clear();
            for (int i = 0; i < __instance.forSale.Count; i++)
            {
                if (__instance.forSale[i] is not Item item || item.Category != Object.SeedsCategory)
                    continue;
                ShopDict[item.QualifiedItemId] = new SeedEntryInfo(item);
            }
        }

        public static IEnumerable<CodeInstruction> ShopMenu_draw_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            SMonitor.Log($"Transpiling ShopMenu.draw");

            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == AccessTools.Field(typeof(ShopMenu), "hoverText"))
                {
                    SMonitor.Log("Adding draw method");
                    codes.Insert(i, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(ModEntry.DrawAllInfo))));
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                    break;
                }
            }

            return codes.AsEnumerable();
        }
    }
}