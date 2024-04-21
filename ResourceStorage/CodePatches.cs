using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.Inventories;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace ResourceStorage
{
    public partial class ModEntry
    {
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ReduceId))]
        public class Inventory_ReduceId_Patch
        {
            public static bool Prefix(Inventory __instance, string itemId, ref int count)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return true;

                if(!TryGetInventoryOwner(__instance, out Farmer farmer))
                {
                    return true;
                }

                count += (int)ModifyResourceLevel(farmer, ItemRegistry.QualifyItemId(itemId), -count);
                return count > 0;
            }
        }

        [HarmonyPatch(typeof(Farmer), nameof(Farmer.addItemToInventory), new Type[] { typeof(Item), typeof(List<Item>) })]
        public class Farmer_addItemToInventory_Patch
        {
            public static bool Prefix(Farmer __instance, Item item)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is ResourceMenu || item is not Object || !CanStore(item as Object))
                    return true;
                return ModifyResourceLevel(__instance, item.QualifiedItemId, item.Stack) <= 0;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountId))]
        public class Inventory_CountId_Patch
        {
            public static void Postfix(Inventory __instance, string itemId, ref int __result)
            {
                if (!Config.ModEnabled || !Config.AutoUse || !ModEntry.TryGetInventoryOwner(__instance, out Farmer farmer))
                    return;

                __result += (int)GetResourceAmount(farmer, ItemRegistry.QualifyItemId(itemId));
            }
        }

        [HarmonyPatch(typeof(Farmer), nameof(Farmer.getItemCount))]
        public class Farmer_getItemCount_Patch
        {
            public static void Postfix(Farmer __instance, string itemId, ref int __result)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return;

                __result += GetMatchesForCrafting(__instance, itemId);
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] {typeof(string)})]
        public class Inventory_ContainsId_Patch
        {
            public static void Postfix(Inventory __instance, string itemId, ref bool __result)
            {
                if (__result || !Config.ModEnabled || !Config.AutoUse || !TryGetInventoryOwner(__instance, out Farmer farmer))
                    return;

                __result = (int)GetResourceAmount(farmer, ItemRegistry.QualifyItemId(itemId)) > 0;
            }
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ContainsId), new Type[] { typeof(string), typeof(int) })]
        public class Inventory_ContainsId2_Patch
        {
            public static void Postfix(Inventory __instance, string itemId, int minimum, ref bool __result)
            {
                if (__result || !Config.ModEnabled || !Config.AutoUse || !TryGetInventoryOwner(__instance, out Farmer farmer))
                    return;

                __result = (int)GetResourceAmount(farmer, ItemRegistry.QualifyItemId(itemId)) >= minimum;
            }
        }

        public static void Inventory_GetById_Postfix(Inventory __instance, string itemId, ref IEnumerable<Item> __result)
        {
            if(!Config.ModEnabled || !Config.AutoUse || !TryGetInventoryOwner(__instance, out Farmer farmer))
            {
                return;
            }

            __result.Append(ItemRegistry.Create<Item>(itemId, (int)GetResourceAmount(farmer, ItemRegistry.QualifyItemId(itemId))));
        }

        [HarmonyPatch(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), new Type[] {typeof(Item)})]
        public class Farmer_couldInventoryAcceptThisItem_Patch
        {
            public static void Postfix(Farmer __instance, Item item, ref bool __result)
            {
                if (!Config.ModEnabled || __result || item is not Object || !CanStore(item as Object))
                    return;

                if (GetResourceAmount(__instance, item.QualifiedItemId) > 0 || CanAutoStore(item.QualifiedItemId))
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(Farmer), nameof(Farmer.couldInventoryAcceptThisItem), new Type[] { typeof(string), typeof(int), typeof(int) })]
        public class Farmer_couldInventoryAcceptThisItem2_Patch
        {
            public static void Postfix(Farmer __instance, string id, int quality, ref bool __result)
            {
                if (!Config.ModEnabled || __result || quality > 0)
                    return;

                string qualifiedId = ItemRegistry.QualifyItemId(id);
                if (GetResourceAmount(__instance, qualifiedId) > 0 || CanAutoStore(qualifiedId))
                    __result = true;
            }
        }

        [HarmonyPatch(typeof(Object), nameof(Object.ConsumeInventoryItem), new Type[] { typeof(Farmer), typeof(Item), typeof(int) })]
        public class Object_ConsumeInventoryItem_Patch
        {
            public static bool Prefix(Farmer who, Item drop_in, ref int amount)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return true;

                amount += (int)ModifyResourceLevel(who, drop_in.QualifiedItemId, -amount);
                return amount > 0;
            }
        }

        [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.ConsumeAdditionalIngredients))]
        public class CraftingRecipe_ConsumeAdditionalIngredients_Patch
        {
            public static void Prefix(List<KeyValuePair<string, int>> additionalRecipeItems)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return;
                for(int i = 0; i < additionalRecipeItems.Count; i++)
                {
                    additionalRecipeItems[i] = new KeyValuePair<string, int>(additionalRecipeItems[i].Key, additionalRecipeItems[i].Value + ConsumeItemsForCrafting(Game1.player, additionalRecipeItems[i].Key, additionalRecipeItems[i].Value));
                }
            }
        }
        [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.getCraftableCount), new Type[] { typeof(IList<Item>) })]
        public class CraftingRecipe_getCraftableCount_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                SMonitor.Log($"Transpiling CraftingRecipe.getCraftableCount");
                var codes = new List<CodeInstruction>(instructions);
                for (int i = 0; i < codes.Count; i++)//Broken
                {
                    if (codes[i].opcode == OpCodes.Ldloc_3 && codes[i + 1].opcode == OpCodes.Ldloc_S && codes[i + 2].opcode == OpCodes.Div)
                    {
                        SMonitor.Log($"adding method to increase ingredient count");
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ModEntry), nameof(AddIngredientAmount))));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_2));
                    }
                }

                return codes.AsEnumerable();
            }
        }

        [HarmonyPatch(typeof(CraftingRecipe), nameof(CraftingRecipe.consumeIngredients))]
        public class CraftingRecipe_consumeIngredients_Patch
        {
            public static void Prefix(CraftingRecipe __instance, ref Dictionary<string, int> __state)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return;

                __state = __instance.recipeList;
                Dictionary<string, int> dict = new();
                foreach(var s in __state)
                {
                    int amount = s.Value + ConsumeItemsForCrafting(Game1.player, s.Key, s.Value);
                    if (amount <= 0)
                        continue;
                    dict.Add(s.Key, amount);
                }
                __instance.recipeList = dict;
            }
            public static void Postfix(CraftingRecipe __instance, ref Dictionary<string, int> __state)
            {
                if (!Config.ModEnabled || !Config.AutoUse)
                    return;
                __instance.recipeList = __state;
            }
        }
        [HarmonyPatch(typeof(GameMenu), new Type[] { typeof(bool)})]
        [HarmonyPatch(MethodType.Constructor)]
        public class GameMenu_Patch
        {
            public static void Postfix(GameMenu __instance)
            {
                if (!Config.ModEnabled)
                    return;
                gameMenu = null;
                SetupResourceButton(__instance);
            }
        }

        [HarmonyPatch(typeof(InventoryPage), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) })]
        [HarmonyPatch(MethodType.Constructor)]
        public class InventoryPage_Patch
        {
            public static void Postfix(InventoryPage __instance)
            {
                if (!Config.ModEnabled)
                    return;

                if(__instance.organizeButton is not null)
                    __instance.organizeButton.downNeighborID = 42999;

                if (__instance.trashCan is not null)
                    __instance.trashCan.upNeighborID = 42999;
            }
        }
        [HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.populateClickableComponentList))]
        public class IClickableMenu_populateClickableComponentList_Patch
        {
            public static void Postfix(IClickableMenu __instance)
            {
                if (!Config.ModEnabled || __instance is not InventoryPage)
                    return;

                if (resourceButton is null)
                    SetupResourceButton(__instance);

                __instance.allClickableComponents.Add(resourceButton);

            }
        }
        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.draw))]
        public class InventoryPage_draw_Patch
        {
            public static void Prefix(SpriteBatch b)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is not GameMenu menu)
                    return;
                SetupResourceButton(menu); // Update the button's bounds
                resourceButton.draw(b);
            }
        }
        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.performHoverAction))]
        public class InventoryPage_performHoverAction_Patch
        {
            public static bool Prefix(ref string ___hoverText, int x, int y)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is not GameMenu)
                    return true;
                if(resourceButton.containsPoint(x, y))
                {
                    ___hoverText = resourceButton.hoverText;
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.receiveKeyPress))]
        public class InventoryPage_receiveKeyPress_Patch
        {
            public static bool Prefix(InventoryPage __instance, Keys key, ref string ___hoverText)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is not GameMenu)
                    return true;
                if(SButtonExtensions.ToSButton(key) == Config.ResourcesKey)
                {
                    ___hoverText = "";
                    Game1.playSound("bigSelect");
                    gameMenu = Game1.activeClickableMenu as GameMenu;
                    Game1.activeClickableMenu = new ResourceMenu();
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.receiveGamePadButton))]
        public class InventoryPage_receiveGamePadButton_Patch
        {
            public static bool Prefix(InventoryPage __instance, Buttons b, ref string ___hoverText)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is not GameMenu)
                    return true;
                if (SButtonExtensions.ToSButton(b) == Config.ResourcesKey)
                {
                    ___hoverText = "";
                    Game1.playSound("bigSelect");
                    gameMenu = Game1.activeClickableMenu as GameMenu;
                    Game1.activeClickableMenu = new ResourceMenu();
                    return false;
                }
                return true;
            }
        }


        [HarmonyPatch(typeof(InventoryPage), nameof(InventoryPage.receiveLeftClick))]
        public class InventoryPage_receiveLeftClick_Patch
        {
            public static bool Prefix(InventoryPage __instance, ref string ___hoverText, int x, int y)
            {
                if (!Config.ModEnabled || Game1.activeClickableMenu is not GameMenu)
                    return true;
                if(resourceButton.containsPoint(x, y))
                {
                    if(Game1.player.CursorSlotItem is Object obj)
                    {
                        if (CanStore(obj) && Game1.objectData.ContainsKey(obj.ItemId))
                        {
                            Game1.playSound("Ship");
                            ModifyResourceLevel(Game1.player, obj.QualifiedItemId, Game1.player.CursorSlotItem.Stack, false);
                            Game1.player.CursorSlotItem = null;
                        }
                    }
                    else
                    {
                        ___hoverText = "";
                        Game1.playSound("bigSelect");
                        gameMenu = Game1.activeClickableMenu as GameMenu;
                        Game1.activeClickableMenu = new ResourceMenu();
                        
                    }
                    return false;
                }
                return true;
            }
        }

        public static void Leclair_Stardew_Common_InventoryHelper_CountItem_Postfix(Farmer who, Func<Item, bool> matcher, ref int __result)
        {
            if (!Config.ModEnabled)
                return;
            var resDict = GetFarmerResources(who);
            foreach(var res in resDict)
            {
                Object obj = new Object(DequalifyItemId(res.Key), (int)res.Value);
                if (matcher(obj))
                {
                    __result = (int.MaxValue - (int)res.Value < __result) ? int.MaxValue : (int)res.Value + __result;
                    return;
                }
            }
        }
        public static void Leclair_Stardew_Common_InventoryHelper_ConsumeItem_Prefix(Func<Item, bool> matcher, IList<Item> items, int amount)
        {
            if (!Config.ModEnabled || items != Game1.player.Items)
                return;

            var resDict = GetFarmerResources(Game1.player);
            foreach(var res in resDict)
            {
                Object obj = new Object(DequalifyItemId(res.Key), (int)res.Value);
                if (matcher(obj))
                {
                    amount += (int)ModifyResourceLevel(Game1.player, res.Key, -amount);
                    return;
                }
            }
        }
    }
}