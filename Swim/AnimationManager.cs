using HarmonyLib;
using Netcode;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley.Locations;

namespace Swim
{
    internal class AnimationManager
    {
        private static IMonitor SMonitor;
        private static ModConfig Config => ModEntry.Config;
        private static IModHelper SHelper;

        private static Texture2D swimShadow => Game1.content.Load<Texture2D>("LooseSprites\\swimShadow");
        public static int SwimTimer = 0;
        public static int SwimShadowFrame = 0;

        public static void Initialize(IMonitor monitor, IModHelper helper)
        {
            SMonitor = monitor;
            SHelper = helper;
        }

        public static void EditAssets(object sender, AssetRequestedEventArgs e)
        {

            if (e.NameWithoutLocale.IsEquivalentTo("FlyingTNT.Swim/MappedPants"))
            {
                e.LoadFromModFile<Texture2D>("assets/PantsMap.png", AssetLoadPriority.Medium);



                /*e.Edit(
                    asset =>
                    {
                        var image = asset.AsImage();
                        Texture2D pant = FarmerRenderer.pantsTexture;
                        for(int i = 0; i < 125; i++)
                        {
                            Rectangle dest = getSwimSourceRectanglePant2(i);
                            Rectangle source = getSwimSourceRectanglePant1(i);

                            image.PatchImage(pant, source, dest, PatchMode.Replace);

                            dest.X += 96;
                            source.X += 96;
                            image.PatchImage(pant, source, dest, PatchMode.Replace);
                        }
                    }
                    );*/
            }
        }

        public static void Patch(Harmony harmony)
        {
            // Always run
            harmony.Patch(
               original: AccessTools.Method(typeof(FarmerSprite), "checkForFootstep"),
               prefix: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerSprite_checkForFootstep_Prefix))
            );

            // Always run
            harmony.Patch(
               original: AccessTools.Method(typeof(FarmerRenderer), nameof(FarmerRenderer.drawHairAndAccesories)),
               transpiler: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_drawHairAndAccessories_Transpiler))
            );

            harmony.Patch(
                   original: AccessTools.Method(typeof(FarmerRenderer), nameof(FarmerRenderer.draw), new Type[] { typeof(SpriteBatch), typeof(FarmerSprite.AnimationFrame), typeof(int), typeof(Rectangle), typeof(Vector2), typeof(Vector2), typeof(float), typeof(int), typeof(Color), typeof(float), typeof(float), typeof(Farmer) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_draw_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_draw_Postfix))
                );

            harmony.Patch(
                    original: AccessTools.Method(typeof(Farmer), nameof(Farmer.ShirtHasSleeves)),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(Farmer_ShirtHasSleeves_Postfix))
                );
        }

        #region PATCHES

        public static void FarmerRenderer_draw_Prefix(Farmer who, ref bool __state)
        {
            try
            {
                if (who.swimming.Value && Game1.player.currentLocation.Name.StartsWith("Custom_Underwater"))
                {
                    who.swimming.Value = false;
                    __state = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_draw_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        internal static void FarmerRenderer_draw_Postfix(SpriteBatch b, Farmer who, bool __state)
        {
            try
            {
                // I want to implement this eventually, but is isn't working completely right rn.
                /*
                if (who.swimming.Value && !FarmerRenderer.isDrawingForUI)
                {
                    b.Draw(swimShadow, Game1.GlobalToLocal(Game1.viewport, who.Position + new Vector2(0f, who.Sprite.SpriteHeight / 4 * 4)), new Rectangle(SwimShadowFrame * 16, 0, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
                }
                */

                if (__state)
                {
                    who.swimming.Value = true;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_draw_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static bool FarmerSprite_checkForFootstep_Prefix()
        {
            try
            {
                if (Game1.player.swimming.Value)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerSprite_checkForFootstep_Prefix)}:\n{ex}", LogLevel.Error);
            }
            return true;
        }

        /// <summary>
        /// Patches the check in FarmerRenderer.drawHairAndAccessories that causes the player's hat to not be drawn when they are wearing a bathing suit.
        /// Instead, replaces that with a call to SwimUitls.ShouldNotDrawHat.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static IEnumerable<CodeInstruction> FarmerRenderer_drawHairAndAccessories_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            // Finds if (who.hat.Value != null && !who.bathingClothes.Value) and replaces the second part with a call to ShouldNotDrawHat

            try
            {
                SMonitor.Log($"Transpiling FarmerRenderer.drawHairAndAccessories");
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].operand is FieldInfo hat && hat == AccessTools.Field(typeof(Farmer), nameof(Farmer.hat)) &&
                        codes[i + 2].opcode == OpCodes.Brfalse &&
                        codes[i + 4].operand is FieldInfo bathingSuit && bathingSuit == AccessTools.Field(typeof(Farmer), nameof(Farmer.bathingClothes)) &&
                        codes[i + 6].opcode == OpCodes.Brtrue)
                    {
                        SMonitor.Log("Adding hat patch!");
                        codes[i + 4].opcode = OpCodes.Nop;
                        codes[i + 4].operand = null;
                        codes[i + 5].opcode = OpCodes.Call;
                        codes[i + 5].operand = AccessTools.Method(typeof(AnimationManager), nameof(ShouldNotDrawHat));

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_drawHairAndAccessories_Transpiler)}:\n{ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        public static bool Farmer_ShirtHasSleeves_Postfix(bool result, Farmer __instance)
        {
            return result && !__instance.bathingClothes.Value;
        }

        #endregion

        #region HELPER_METHODS

        public static bool ShouldNotDrawHat(Farmer farmer)
        {
            return (!Config.DisplayHatWithSwimsuit) && farmer.bathingClothes.Value;
        }
        #endregion
    }
}
