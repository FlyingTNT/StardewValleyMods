using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using System;
using Object = StardewValley.Object;

namespace PaintingDisplay
{
    public partial class ModEntry
    {
        private static bool Sign_draw_Prefix(Sign __instance, SpriteBatch spriteBatch, int x, int y, float alpha)
        {
            if (!Config.EnableMod || __instance.displayItem.Value is not Furniture furniture || furniture.furniture_type.Value != Furniture.painting || (SHelper.ModRegistry.IsLoaded("aedenthorn.CustomPictureFrames") && __instance.displayItem.Value.modData.ContainsKey("aedenthorn.CustomPictureFrames/index")))
                return true;

            // Calls the base Object.draw method, rather than the Sign method
            var ptr = AccessTools.Method(typeof(Object), "draw", new Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }).MethodHandle.GetFunctionPointer();
            var baseMethod = (Action<SpriteBatch, int, int, float>)Activator.CreateInstance(typeof(Action<SpriteBatch, int, int, float>), __instance, ptr);
            baseMethod(spriteBatch, x, y, alpha);

            Rectangle drawn_source_rect = furniture.sourceRect.Value;

            ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
            if (itemData.IsErrorItem)
            {
                drawn_source_rect = itemData.GetSourceRect();
            }

            spriteBatch.Draw(itemData.GetTexture(), Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64 + 32 - furniture.sourceRect.Width * 2, y * 64 - 8 - furniture.sourceRect.Height * 2)), new Rectangle?(drawn_source_rect), Color.White * alpha, 0f, Vector2.Zero, 4f, furniture.Flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, Math.Max(0f, ((y + 1) * 64 - 24) / 10000f) + x * 1E-05f + 1E-05f);
            return false;
        }
    }
}