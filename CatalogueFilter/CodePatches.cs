using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;
using System.Collections.Generic;

namespace CatalogueFilter
{
    public partial class ModEntry
    {
        private static readonly PerScreen<string> lastFilterString = new(() => "");
        private static readonly PerScreen<TextBox> filterField = new(() => null);
        private static readonly PerScreen<List<ISalable>> allItems = new(() => null);


        private static string LastFilterString
        {
            get => lastFilterString.Value;
            set => lastFilterString.Value = value;
        }

        private static TextBox FilterField
        {
            get => filterField.Value;
            set => filterField.Value = value;
        }

        private static List<ISalable> AllItems
        {
            get => allItems.Value;
            set => allItems.Value = value;
        }

        public static void ShopMenu_Constructor_Postfix(ShopMenu __instance)
        {
            if (!Config.ModEnabled)
                return;
            AllItems = new List<ISalable>(__instance.forSale);
            FilterField = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
            {
                X = __instance.xPositionOnScreen + 28 + Config.FilterOffsetX,
                Y = __instance.yPositionOnScreen + __instance.height - 88 + Config.FilterOffsetY,
                Text = "",
                Selected = Config.AutoSelectFilter
            };
        }

        public static void ShopMenu_applyTab_Postfix(ShopMenu __instance)
        {
            if (FilterField != null)
            {
                FilterField.Text = "";
            }
            AllItems = new List<ISalable>(__instance.forSale);
        }

        public static void ShopMenu_updatePosition_Postfix(ShopMenu __instance)
        {
            if (FilterField is null) // This is called during the constructor, and we setup the field after the constructor, so the field might be null.
                return;

            FilterField.X = __instance.xPositionOnScreen + 28 + Config.FilterOffsetX;
            FilterField.Y = __instance.yPositionOnScreen + __instance.height - 88 + Config.FilterOffsetY;
        }

        public static void ShopMenu_drawCurrency_Postfix(ShopMenu __instance, SpriteBatch b)
        {
            if (!Config.ModEnabled)
                return;
            if (LastFilterString != FilterField.Text)
            {
                LastFilterString = FilterField.Text;

                foreach (var i in __instance.forSale)
                {
                    if (!AllItems.Contains(i))
                    {
                        AllItems.Add(i);
                    }
                }
                for (int i = AllItems.Count - 1; i >= 0; i--)
                {
                    if (!__instance.itemPriceAndStock.ContainsKey(AllItems[i]))
                    {
                        AllItems.RemoveAt(i);
                    }
                }
                __instance.forSale.Clear();
                if (FilterField.Text == "")
                {
                    __instance.forSale.AddRange(AllItems);
                }
                else
                {
                    foreach (var item in AllItems)
                    {
                        if (item.DisplayName.ToLower().Contains(FilterField.Text.ToLower()))
                        {
                            __instance.forSale.Add(item);
                        }
                    }
                }
                __instance.currentItemIndex = 0;

                if (!SHelper.ModRegistry.IsLoaded("spacechase0.BiggerBackpack"))
                    __instance.gameWindowSizeChanged(Game1.graphics.GraphicsDevice.Viewport.Bounds, Game1.graphics.GraphicsDevice.Viewport.Bounds);
            }

            FilterField.Draw(b);
            if (Config.ShowLabel)
            {
                SpriteText.drawStringHorizontallyCenteredAt(b, SHelper.Translation.Get("filter"), __instance.xPositionOnScreen + 128 + Config.FilterOffsetX, __instance.yPositionOnScreen + __instance.height - 136 + Config.FilterOffsetY, 999999, -1, 999999, 1, 0.88f, false, Config.LabelColor, 99999);
            }
        }

        public static void ShopMenu_receiveLeftClick_Postfix()
        {
            if (!Config.ModEnabled)
                return;
            FilterField.Update();
        }

        public static bool ShopMenu_receiveKeyPress_Prefix(Keys key)
        { 
            // If the filter is selected, absorb all key presses except escape
            return !Config.ModEnabled || !FilterField.Selected || key == Keys.Escape;
        }

        public static void ShopMenu_performHoverAction_Postfix(int x, int y)
        {
            if (!Config.ModEnabled)
                return;
            FilterField.Hover(x, y);
        }
    }
}