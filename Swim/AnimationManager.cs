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
using StardewModdingAPI.Utilities;

namespace Swim
{
    internal class AnimationManager
    {
        private static IMonitor SMonitor;
        private static ModConfig Config;
        private static IModHelper SHelper;

        private static readonly PerScreen<bool> spriteDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<bool> eyesDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<bool> skinDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<bool> shoesDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<bool> shirtDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<bool> pantsDirty = new PerScreen<bool>(()=>true);
        private static readonly PerScreen<Texture2D> texture = new PerScreen<Texture2D>(()=>null);

        const int LegacyPatches = 0;
        const int MediumPatches = 1;
        const int AllPatches = 2;

        public static void Initialize(IMonitor monitor, IModHelper helper, ModConfig config)
        {
            SMonitor = monitor;
            Config = config;
            SHelper = helper;
        }

        public static void EditAssets(object sender, AssetRequestedEventArgs e)
        {
            if (Config.AnimationPatches != AllPatches)
                return;

            if(e.NameWithoutLocale.StartsWith("FlyingTNT.Swim/DisarmedModels"))
            {
                string[] path = SplitPath(e.NameWithoutLocale.BaseName);

                if (e.DataType == typeof(Texture2D))
                {
                    e.LoadFrom(() => Game1.content.Load<Texture2D>("Characters/Farmer" + PathUtilities.PreferredAssetSeparator + path[2]), AssetLoadPriority.Medium);
                }
                else if (e.DataType == typeof(IRawTextureData))
                {
                    e.LoadFrom(() => Game1.content.Load<IRawTextureData>("Characters/Farmer" + PathUtilities.PreferredAssetSeparator + path[2]), AssetLoadPriority.Medium);
                }
                else
                {
                    SMonitor.Log($"Error loading file {"FlyingTNT.Swim/DisarmedModels/" + path[2]}! Invalid type ({e.DataType})", LogLevel.Error);
                    return;
                }

                e.Edit(asset =>
                {
                    IAssetDataForImage image = asset.AsImage();
                    UnarmBathingModels(image, path[2].Contains("girl"));
                });
            }else if(e.NameWithoutLocale.IsEquivalentTo("Characters/Farmer/Pants"))
            {
                e.Edit(asset =>
                {
                    IAssetDataForImage image = asset.AsImage();
                    UpdateBathingSuits(image);
                });
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
            
            // Always run pre and post fixes, only run transpiler if AnimationPatches is AllPatches
            harmony.Patch(
               original: AccessTools.Method(typeof(FarmerRenderer), nameof(FarmerRenderer.draw), new Type[] { typeof(SpriteBatch), typeof(FarmerSprite.AnimationFrame), typeof(int), typeof(Rectangle), typeof(Vector2), typeof(Vector2), typeof(float), typeof(int), typeof(Color), typeof(float), typeof(float), typeof(Farmer) }),
               prefix: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_draw_Prefix)),
               postfix: new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_draw_Postfix)),
               transpiler: Config.AnimationPatches == AllPatches ? new HarmonyMethod(typeof(AnimationManager), nameof(FarmerRenderer_draw_Transpiler)) : null
            );

            if(Config.AnimationPatches == MediumPatches)
            {
                harmony.Patch(
                    original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(FarmerSprite.endOfAnimationBehavior), typeof(int), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(FarmerSprite.endOfAnimationBehavior), typeof(int) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(bool), typeof(int) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(bool) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int) }),
                   prefix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor2_Prefix)),
                   postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );
            }

            if (Config.AnimationPatches == AllPatches)
            {
                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(FarmerSprite.endOfAnimationBehavior), typeof(int) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(bool), typeof(int) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(FarmerSprite.endOfAnimationBehavior), typeof(int), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                   original: AccessTools.Constructor(typeof(FarmerSprite.AnimationFrame), new Type[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(FarmerSprite.endOfAnimationBehavior), typeof(bool) }),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(AnimationFrame_Constructor3_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(Farmer), nameof(Farmer.GetDisplayPants)),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(Farmer_GetDisplayPants_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Constructor(typeof(FarmerRenderer), new Type[] { typeof(string), typeof(Farmer)}),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(FramerRenderer_Constructor_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Constructor(typeof(FarmerRenderer), new Type[] {}),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(FramerRenderer_Constructor_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(Farmer), nameof(Farmer.ShirtHasSleeves)),
                    postfix: new HarmonyMethod(typeof(AnimationManager), nameof(Farmer_ShirtHasSleeves_Postfix))
                );
            }
        }

        #region PATCHES

        public static void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            MarkAllDirty();
        }

        public struct FarmerRendererDrawState
        {
            public bool wasSwimming = false;
            public Texture2D oldTexture = null;
            public string oldTextureName = null;
            public bool wasSpriteDirty = false;
            public bool wasSkinDirty = false;
            public bool wasEyesDirty = false;
            public bool wasShirtDirty = false;
            public bool wasPantsDirty = false;
            public bool wasShoesDirty = false;
            public bool wasBaseTextureDirty = false;

            public FarmerRendererDrawState(){}
        }

        public static void FarmerRenderer_draw_Prefix(FarmerRenderer __instance, Farmer who, int currentFrame, ref NetString ___textureName, ref bool ____spriteDirty, ref bool ____eyesDirty, ref bool ____skinDirty, ref bool ____shirtDirty, ref bool ____pantsDirty, ref bool ____shoesDirty, ref bool ____baseTextureDirty, ref FarmerRendererDrawState __state)
        {
            try
            {
                if (who.swimming.Value && Game1.player.currentLocation.Name.StartsWith("Custom_Underwater"))
                {
                    who.swimming.Value = false;
                    __state.wasSwimming = true;
                }

                if(who.bathingClothes.Value && Config.AnimationPatches == AllPatches && TryGetShouldUseArmless(currentFrame, out bool shouldUseArmless) && shouldUseArmless)
                {
                    string which = SplitPath(__instance.textureName.Value)[2];

                    string textureName = "FlyingTNT.Swim/DisarmedModels/" + which;

                    if(texture.Value is null || texture.Value.IsDisposed)
                    {
                        SMonitor.Log($"Reloading Screen {Context.ScreenId} Texture!");
                        texture.Value = GetUniqueScreenTexture(textureName);
                    }

                    IReflectedField<Texture2D> baseTexture = SHelper.Reflection.GetField<Texture2D>(__instance, "baseTexture");
                    __state.oldTexture = baseTexture.GetValue();
                    baseTexture.SetValue(texture.Value);

                    if(spriteDirty.Value)
                    {
                        __state.wasSpriteDirty = ____spriteDirty;
                        __state.wasSkinDirty = ____skinDirty;
                        __state.wasEyesDirty = ____eyesDirty;
                        __state.wasShirtDirty = ____shirtDirty;
                        __state.wasPantsDirty = ____pantsDirty;
                        __state.wasShoesDirty = ____shoesDirty;
                        __state.wasBaseTextureDirty = ____baseTextureDirty;

                        __state.oldTextureName = ___textureName.Value;
                        ___textureName.Set(textureName);

                        SMonitor.Log("Sprite Dirty");
                        if (skinDirty.Value)
                        {
                            SMonitor.Log("Skin Dirty");
                            ____skinDirty = true;
                            skinDirty.Value = false;
                        }
                        if (eyesDirty.Value)
                        {
                            SMonitor.Log("Eyes Dirty");
                            ____eyesDirty = true;
                            eyesDirty.Value = false;
                        }
                        if (shirtDirty.Value)
                        {
                            SMonitor.Log("Shirt Dirty");
                            ____shirtDirty = true;
                            shirtDirty.Value = false;
                        }
                        if (pantsDirty.Value)
                        {
                            SMonitor.Log("Pants Dirty");
                            ____pantsDirty = true;
                            pantsDirty.Value = false;
                        }
                        if (shoesDirty.Value)
                        {
                            SMonitor.Log("Shoes Dirty");
                            ____shoesDirty = true;
                            shoesDirty.Value = false;
                        }
                        ____spriteDirty = true;
                        spriteDirty.Value = false;
                    }
                    ____baseTextureDirty = false; // If it is true, it will undo our changes to the baseTexture.

                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_draw_Prefix)}:\n{ex}", LogLevel.Error);
            }
        }

        internal static void FarmerRenderer_draw_Postfix(FarmerRenderer __instance, Farmer who, ref NetString ___textureName, ref bool ____spriteDirty, ref bool ____eyesDirty, ref bool ____skinDirty, ref bool ____shirtDirty, ref bool ____pantsDirty, ref bool ____shoesDirty, ref bool ____baseTextureDirty, FarmerRendererDrawState __state)
        {
            try
            {
                if (__state.wasSwimming)
                {
                    who.swimming.Value = true;
                }

                if (__state.oldTexture is not null)
                {
                    SHelper.Reflection.GetField<Texture2D>(__instance, "baseTexture").SetValue(__state.oldTexture);
                }

                if(__state.oldTextureName is not null)
                {
                    ___textureName.Set(__state.oldTextureName);
                    ____spriteDirty = __state.wasSpriteDirty;
                }

                if(__state.wasEyesDirty)
                {
                    ____eyesDirty = true;
                }

                if (__state.wasSkinDirty)
                {
                    ____skinDirty = true;
                }
                
                if (__state.wasShirtDirty)
                {
                    ____shirtDirty = true;
                }
                
                if (__state.wasPantsDirty)
                {
                    ____pantsDirty = true;
                }
                
                if (__state.wasShoesDirty)
                {
                    ____shoesDirty = true;
                }

                ____baseTextureDirty = __state.wasBaseTextureDirty;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_draw_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        static void FramerRenderer_Constructor_Postfix(NetColor ___eyes, NetInt ___skin, NetString ___shoes, NetString ___shirt, NetString ___pants)
        {
            ___eyes.fieldChangeVisibleEvent += delegate
            {
                spriteDirty.Value = true;
                eyesDirty.Value = true;
            };
            ___skin.fieldChangeVisibleEvent += delegate
            {
                spriteDirty.Value = true;
                skinDirty.Value = true;
                shirtDirty.Value = true;
            };
            ___shoes.fieldChangeVisibleEvent += delegate
            {
                spriteDirty.Value = true;
                shoesDirty.Value = true;
            };
            ___shirt.fieldChangeVisibleEvent += delegate
            {
                spriteDirty.Value = true;
                shirtDirty.Value = true;
            };
            ___pants.fieldChangeVisibleEvent += delegate
            {
                spriteDirty.Value = true;
                pantsDirty.Value = true;
            };
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

        public static void AnimationFrame_Constructor3_Postfix(ref FarmerSprite.AnimationFrame __instance, int frame)
        {
            try
            {
                if (!Game1.player.bathingClothes.Value)
                    return;

                if (!(TryGetShouldUseArmless(frame, out bool shouldUseArmless) && shouldUseArmless))
                {
                    __instance.armOffset = 0;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(AnimationFrame_Constructor3_Postfix)}:\n{ex}", LogLevel.Error);
            }
        }

        public static void AnimationFrame_Constructor2_Prefix(ref int frame)
        {
            MapAnimationFrameToBathingSuitAnimation(Game1.player, ref frame);
        }

        public static void Farmer_GetDisplayPants_Postfix(Farmer __instance, ref int spriteIndex)
        {
            if (!__instance.bathingClothes.Value || FarmerRenderer.isDrawingForUI)
                return;

            spriteIndex = TryGetShouldUseArmless(__instance.FarmerSprite.CurrentFrame, out bool shouldUseArmless) && shouldUseArmless ? 0 : 1;
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

            /*
             * 
             * 0  IL_0e8d: ldarg.3
	         * 1  IL_0e8e: ldfld class Netcode.NetRef`1<class StardewValley.Objects.Hat> StardewValley.Farmer::hat
	         * 2  IL_0e93: callvirt instance !0 class Netcode.NetFieldBase`2<class StardewValley.Objects.Hat, class Netcode.NetRef`1<class StardewValley.Objects.Hat>>::get_Value()
	         * 3  IL_0e98: brfalse IL_116d
             *
             *    // We want to edit this section so that instead of checking farmer.bathingClothes, it calls ShouldNotDrawHat()
	         * 4  IL_0e9d: ldarg.3 <- keep (it loads the Farmer object onto the stack)
	         * 5  IL_0e9e: ldfld class Netcode.NetBool StardewValley.Farmer::bathingClothes <- delete (we only need 3 instructions)
	         * 6  IL_0ea3: call bool Netcode.NetBool::op_Implicit(class Netcode.NetBool) <- replace operand with ShouldNotDrawHat()
	         * 7  IL_0ea8: brtrue IL_116d <- keep
             *
	         * 8  IL_0ead: ldarg.3
	         * 9  IL_0eae: callvirt instance class StardewValley.FarmerSprite StardewValley.Farmer::get_FarmerSprite()
	         * 10 IL_0eb3: callvirt instance valuetype StardewValley.FarmerSprite/AnimationFrame StardewValley.FarmerSprite::get_CurrentAnimationFrame()
	         * 11 IL_0eb8: ldfld bool StardewValley.FarmerSprite/AnimationFrame::flip
             */

            int hatCount = 0;

            try
            {
                SMonitor.Log($"Transpiling FarmerRenderer.drawHairAndAccessories");

                bool hat = true;

                // We want codes[i] to be that first ldarg. We are going to check every instruction shown above because for this method, this code is kind of generic (similar checks happen multiple times)
                for (int i = 0; i < codes.Count; i++)
                {
                    if (hat && codes[i].opcode == OpCodes.Ldarg_3 && codes[i + 1].opcode == OpCodes.Ldfld && codes[i + 2].opcode == OpCodes.Callvirt && codes[i + 3].opcode == OpCodes.Brfalse &&
                        codes[i + 4].opcode == OpCodes.Ldarg_3 && codes[i + 5].opcode == OpCodes.Ldfld && codes[i + 6].opcode == OpCodes.Call && codes[i + 7].opcode == OpCodes.Brtrue &&
                        codes[i + 8].opcode == OpCodes.Ldarg_3 && codes[i + 9].opcode == OpCodes.Callvirt && codes[i + 10].opcode == OpCodes.Callvirt && codes[i + 11].opcode == OpCodes.Ldfld &&
                        (FieldInfo)codes[i + 5].operand == AccessTools.Field(typeof(Farmer), nameof(Farmer.bathingClothes)) && (MethodInfo)codes[i + 6].operand == AccessTools.Method(typeof(NetBool), "op_Implicit") &&
                        (FieldInfo)codes[i + 11].operand == AccessTools.Field(typeof(FarmerSprite.AnimationFrame), nameof(FarmerSprite.AnimationFrame.flip)))
                    {
                        SMonitor.Log("Adding hat patch!");
                        codes[i + 5].opcode = OpCodes.Nop;
                        codes[i + 5].operand = null;

                        codes[i + 6].operand = AccessTools.Method(typeof(AnimationManager), nameof(ShouldNotDrawHat));

                        hat = false;
                    }

                    // Only edit the feature offset if animation patches = 3
                    if (Config.AnimationPatches == AllPatches && codes[i].opcode == OpCodes.Ldsfld && codes[i].operand is FieldInfo info && info == AccessTools.Field(typeof(FarmerRenderer), nameof(FarmerRenderer.featureYOffsetPerFrame)))
                    {
                        SMonitor.Log($"Editing the feature offset ({hatCount})");

                        List<Label> labels = codes[i].labels;

                        codes[i] = CodeInstruction.Call(typeof(AnimationManager), nameof(MapFarmerRendererFeatureYOffset));

                        codes[i].labels = labels;

                        hatCount++;
                    }

                    if (Config.AnimationPatches == AllPatches && codes[i].opcode == OpCodes.Ldsfld && codes[i].operand is FieldInfo info2 && info2 == AccessTools.Field(typeof(FarmerRenderer), nameof(FarmerRenderer.featureXOffsetPerFrame)))
                    {
                        SMonitor.Log($"Editing the feature offset ({hatCount})");

                        List<Label> labels = codes[i].labels;

                        codes[i] = CodeInstruction.Call(typeof(AnimationManager), nameof(MapFarmerRendererFeatureXOffset));
                        codes[i].labels = labels;

                        hatCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_drawHairAndAccessories_Transpiler)}:\n{ex}", LogLevel.Error);
            }

            return codes.AsEnumerable();
        }

        public static IEnumerable<CodeInstruction> FarmerRenderer_draw_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool pants = true;
            bool body = true;
            int hatCount = 0;

            SMonitor.Log($"Transpiling FarmerRenderer.draw");

            try
            {
                for (int i = 0; i < codes.Count; i++)
                {
                    if (body && codes[i].opcode == OpCodes.Ldarg_S && codes[i + 1].opcode == OpCodes.Newobj && codes[i + 2].opcode == OpCodes.Ldarg_S && codes[i + 3].opcode == OpCodes.Ldarg_S &&
                        (byte)codes[i].operand == 4 && (byte)codes[i + 2].operand == 9 && (byte)codes[i + 3].operand == 10)
                    {
                        SMonitor.Log($"Editing the base sprite.");

                        codes.Insert(i + 1, CodeInstruction.Call(typeof(AnimationManager), nameof(getSwimSourceRectangle), new Type[] { typeof(Farmer), typeof(Rectangle) }));
                        codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_S, (byte)12));
                        body = false;
                    }

                    // Replace the pants sprite with the swimsuit
                    if (pants && codes[i].opcode == OpCodes.Ldarg_S && codes[i + 1].opcode == OpCodes.Ldfld && codes[i + 2].opcode == OpCodes.Ldarg_S && codes[i + 3].opcode == OpCodes.Ldfld &&
                        codes[i + 4].opcode == OpCodes.Ldarg_S && codes[i + 5].opcode == OpCodes.Ldfld && codes[i + 6].opcode == OpCodes.Ldarg_S && codes[i + 7].opcode == OpCodes.Ldfld &&
                        (FieldInfo)codes[i + 1].operand == AccessTools.Field(typeof(Rectangle), nameof(Rectangle.X)) &&
                        (FieldInfo)codes[i + 3].operand == AccessTools.Field(typeof(Rectangle), nameof(Rectangle.Y)) &&
                        (FieldInfo)codes[i + 5].operand == AccessTools.Field(typeof(Rectangle), nameof(Rectangle.Width)) &&
                        (FieldInfo)codes[i + 7].operand == AccessTools.Field(typeof(Rectangle), nameof(Rectangle.Height)))
                    {
                        SMonitor.Log($"Editing the pants sprite.");

                        codes[i + 1].opcode = OpCodes.Call;
                        codes[i + 1].operand = AccessTools.Method(typeof(AnimationManager), nameof(GetPantsRectX));

                        codes[i + 3].opcode = OpCodes.Call;
                        codes[i + 3].operand = AccessTools.Method(typeof(AnimationManager), nameof(GetPantsRectY));

                        codes[i + 5].opcode = OpCodes.Call;
                        codes[i + 5].operand = AccessTools.Method(typeof(AnimationManager), nameof(GetPantsRectWidth));

                        codes[i + 7].opcode = OpCodes.Call;
                        codes[i + 7].operand = AccessTools.Method(typeof(AnimationManager), nameof(GetPantsRectHeight));

                        pants = false;
                    }

                    if (codes[i].opcode == OpCodes.Ldsfld && codes[i].operand is FieldInfo info1 && info1 == AccessTools.Field(typeof(FarmerRenderer), nameof(FarmerRenderer.featureYOffsetPerFrame)))
                    {
                        SMonitor.Log($"Editing the feature offset ({hatCount})");

                        codes[i] = CodeInstruction.Call(typeof(AnimationManager), nameof(MapFarmerRendererFeatureYOffset));

                        hatCount++;
                    }

                    if (codes[i].opcode == OpCodes.Ldsfld && codes[i].operand is FieldInfo info2 && info2 == AccessTools.Field(typeof(FarmerRenderer), nameof(FarmerRenderer.featureXOffsetPerFrame)))
                    {
                        SMonitor.Log($"Editing the feature offset ({hatCount})");

                        codes[i] = CodeInstruction.Call(typeof(AnimationManager), nameof(MapFarmerRendererFeatureXOffset));

                        hatCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(FarmerRenderer_draw_Transpiler)}:\n{ex}", LogLevel.Error);
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

        private const int sidewaysStandstill = 114;
        private const int sidewaysRightFootRaised = 115;
        private const int sidewaysLeftFootRaised = 116;
        private const int upStandstill = 120;
        private const int upLeftFootRaised = 121;
        private const int upRightFootRaised = 122;
        private const int downStandstill = 108;
        private const int downLeftFootRaised = 109;
        private const int downRightFootRaised = 110;

        public static readonly int[] AnimationFrameToBathingSuitFrameMap = new int[] {
            downStandstill, // 0 is just standing still facing down
            downLeftFootRaised, // 1 is left foot partially raised
            downRightFootRaised, // 2 is right foot partially raised
            3, // 3 is Hi emote - also used in other emotes
            4, // 4 is pass out frame 1
            5, // 5 is pass out frame 2
            sidewaysStandstill, // 6 is just standing still facing sideways
            sidewaysRightFootRaised, // 7 is right foot partially raised (walking)
            sidewaysLeftFootRaised, // 8 is left foot partially raised (walking)
            9,
            10, // 10 is Taunt emote frame 2
            sidewaysLeftFootRaised, // 11 is left foot partially raised
            upStandstill, // 12 is just standing still facing up
            upRightFootRaised, // 13 is right foot partially raised
            upLeftFootRaised, // 14 is left foot partially raised
            15,
            16, // 16 is tired
            sidewaysRightFootRaised, // 17 is right foot partially raised
            downLeftFootRaised, // 18 is left foot fully raised
            downRightFootRaised, // 19 is right foot fully raised
            sidewaysLeftFootRaised, // 20 is left foot fully raised
            sidewaysRightFootRaised, // 21 is right foot fully raised
            upRightFootRaised, // 22 is right foot fully raised
            upLeftFootRaised, // 23 is left foot fully raised
            downStandstill, // 24 is using scythe facing down frame 0?
            downStandstill, // 25 is using scythe facing down frame 1
            downStandstill, // 26 is using scythe facing down frame 2
            downStandstill, // 27 is using scythe facing down frame 3
            downStandstill, // 28 is using scythe facing down frame 4
            downStandstill, // 29 is using scythe facing down frame 5
            sidewaysStandstill, // 30 is using scythe facing sideways frame 0?
            sidewaysStandstill, // 31 is using scythe facing sideways frame 1
            sidewaysStandstill, // 32 is using scythe facing sideways frame 2
            sidewaysStandstill, // 33 is using scythe facing sideways frame 3
            sidewaysStandstill, // 34 is using scythe facing sideways frame 4
            sidewaysStandstill, // 35 is using scythe facing sideways frame 5
            upStandstill, // 36 is using tool facing up frame 0?
            upStandstill, // 37 is using tool facing up frame 1 - Also using scythe facing up frame 1
            upStandstill, // 38 is using tool facing up frame 2 - Also using scythe facing up frame 2
            upStandstill, // 39 is using scythe facing up frame 3
            upStandstill, // 40 is using scythe facing up frame 4
            upStandstill, // 41 is using scythe facing up frame 5
            42,
            43,
            upStandstill, // 44 is fishing facing up
            sidewaysStandstill, // 45 is using watering can facing sideways
            upStandstill, // 46 is using watering can facing up
            47,
            sidewaysRightFootRaised, // 48 is sideways charging fishing pole
            sidewaysStandstill, // 49 is using tool facing sideways frame 1
            sidewaysStandstill, // 50 is using tool facing sideways frame 2
            sidewaysStandstill, // 51 is using tool facing sideways frame 3
            sidewaysStandstill, // 52 is using tool facing sideways frame 4
            53,
            downLeftFootRaised, // 54 is grabbing forage facing down frame 1
            downLeftFootRaised, // 55 is grabbing forage facing down frame 2
            downStandstill, // 56 is grabbing forage facing down frame 3
            downStandstill, // 57 is grabbing forage facing down frame 4
            sidewaysLeftFootRaised, // 58 is grabbing forage facing sideways 1
            sidewaysLeftFootRaised, // 59 is grabbing forage facing sideways 2
            sidewaysStandstill, // 60 is grabbing forage facing sideways 3
            sidewaysStandstill, // 61 is grabbing forage facing sideways 4
            upStandstill, // 62 is using tool facing up frame 4 - Also grabbing forage facing up frame 1
            upStandstill, // 63 is using tool facing up frame 3 - Also grabbing forage facing up frame 2
            upStandstill, // 64 is grabbing forage facing up frame 3
            upStandstill, // 65 is grabbing forage facing up frame 4
            downLeftFootRaised, // 66 is down charging fishing pole
            downStandstill, // 67 is using tool facing down frame 1
            downStandstill, // 68 is using tool facing down frame 2
            downStandstill, // 69 is using tool facing down frame 3
            downStandstill, // 70 is using tool facing down frame 4
            71,
            sidewaysRightFootRaised, // 72 is pulling fish out of the water facing sideways
            73,
            downLeftFootRaised, // 74 is pulling fish out of the water facing down
            75,
            upRightFootRaised, // 76 is charging fishing pole facing up
            77,
            downStandstill, // 78 is shear down frame 1
            downStandstill, // 79 is shear down frame 2
            sidewaysStandstill, // 80 is shear sideways frame 1
            sidewaysStandstill, // 81 is shear sideways frame 2
            upStandstill, // 82 is shear up frame 1
            upStandstill, // 83 is shear up frame 2
            downStandstill, // 84 is displaying item (facing down) - Also eat frame 1
            downStandstill, // 85 is eat frame 2
            downStandstill, // 86 is eat frame 3
            downStandstill, // 87 is eat frame 4
            downStandstill, // 88 is eat frame 5
            sidewaysStandstill, // 89 is fishing sideways idle frame
            downStandstill, // 90 is drink frame 1
            downStandstill, // 91 is drink frame 2
            downStandstill, // 92 is drink frame 3
            downStandstill, // 93 is drink frame 4
            94,
            95,
            96,
            97, // 97 is cheer
            98, // 98 is music emote frame 3
            99, // 99 is music emote frame 1
            100, // 100 is music emote frame 2
            101,
            102, // 102 is Laugh emote frame 2
            103, // 103 is Laugh emote frame 1
            104, // 104 is sick frame 1
            105, // 105 is sick frame 2
            106,
            107,
            downStandstill, // 108 is bathing suit down standing still
            downLeftFootRaised, // 109 is bathing suit down left foot raised
            downRightFootRaised, // 110 is bathing suit down right foot raised
            111, // 111 is jar emote
            112, // 112 is jar emote but open
            113,
            sidewaysStandstill, // 114 is bathing suit sideways standing still
            sidewaysRightFootRaised, // 115 is bathing suit sideways right foot raised
            sidewaysLeftFootRaised, // 116 is bathing suit sideways left foot raised
            117,
            118,
            119,
            upStandstill, // 120 is bathing suit up standing still
            upLeftFootRaised, // 121 is bathing suit up left foot raised
            upRightFootRaised, // 122 is bathing suit up right foot raised
            downStandstill, // 123 is pan frame 1
            downStandstill, // 124 is pan frame 2
            downStandstill, // 125 is pan frame 3
        };

        public static readonly bool[] ShouldUseArmlessSpritePerFrame = new bool[]
            {
               false, false, false,  true,  true,  true, false, false, false,  true,// 0
                true, false, false, false, false,  true,  true, false, false, false,// 10
               false, false, false, false,  true,  true,  true,  true,  true,  true,// 20
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 30
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 40
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 50
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 60
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 70
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 80
                true,  true,  true,  true,  true,  true,  true,  true,  true,  true,// 90
                true,  true,  true,  true,  true,  true,  true,  true, false, false,// 100
               false,  true,  true,  true, false, false, false,  true,  true,  true,// 110
               false, false, false,  true,  true,  true                             // 120
            };

        public static bool TryMapAnimationFrame(int frame, out int mappedFrame)
        {
            if(frame >= 0 && frame < AnimationFrameToBathingSuitFrameMap.Length)
            {
                mappedFrame = AnimationFrameToBathingSuitFrameMap[frame];
                return true;
            }

            mappedFrame = frame;
            return false;
        }

        public static bool TryMapAnimationFrame(ref int frame)
        {
            if (frame >= 0 && frame < AnimationFrameToBathingSuitFrameMap.Length)
            {
                frame = AnimationFrameToBathingSuitFrameMap[frame];
                return true;
            }

            return false;
        }

        public static bool TryGetShouldUseArmless(int frame, out bool shouldUseArmless)
        {
            if (frame >= 0 && frame < ShouldUseArmlessSpritePerFrame.Length)
            {
                shouldUseArmless = ShouldUseArmlessSpritePerFrame[frame];
                return true;
            }

            shouldUseArmless = false;
            return false;
        }

        public static bool MapAnimationFrameToBathingSuitAnimation(Farmer farmer, ref int frame)
        {
            if (!farmer.bathingClothes.Value)
                return false;

            int oldFrame = frame;
            TryMapAnimationFrame(ref frame);
            return oldFrame != frame;
        }

        public static Rectangle getSwimSourceRectangle(Farmer farmer, Rectangle ogRect)
        {
            if (!farmer.bathingClothes.Value || farmer.swimming.Value || FarmerRenderer.isDrawingForUI)
                return ogRect;

            TryMapAnimationFrame(farmer.FarmerSprite.CurrentFrame, out int frame);
            return new Rectangle(frame * farmer.FarmerSprite.SpriteWidth % 96, frame * farmer.FarmerSprite.SpriteWidth / 96 * farmer.FarmerSprite.SpriteHeight, farmer.FarmerSprite.SpriteWidth, farmer.FarmerSprite.SpriteHeight);
        }

        public static int GetPantsRectX(Rectangle sourceRect)
        {
            return getSwimSourceRectangle(Game1.player, sourceRect).X;
        }

        public static int GetPantsRectY(Rectangle sourceRect)
        {
            return getSwimSourceRectangle(Game1.player, sourceRect).Y;
        }

        public static int GetPantsRectWidth(Rectangle sourceRect)
        {
            return getSwimSourceRectangle(Game1.player, sourceRect).Width;
        }

        public static int GetPantsRectHeight(Rectangle sourceRect)
        {
            return getSwimSourceRectangle(Game1.player, sourceRect).Height;
        }

        public static readonly int[] FarmerRendererFeatureXOffsetPerFrameMapped = AnimationFrameToBathingSuitFrameMap.Select(value => FarmerRenderer.featureXOffsetPerFrame[value]).ToArray();
        public static readonly int[] FarmerRendererFeatureYOffsetPerFrameMapped = AnimationFrameToBathingSuitFrameMap.Select(value => FarmerRenderer.featureYOffsetPerFrame[value]).ToArray();

        public static int[] MapFarmerRendererFeatureXOffset()
        {
            return Game1.player.bathingClothes.Value ? FarmerRendererFeatureXOffsetPerFrameMapped : FarmerRenderer.featureXOffsetPerFrame;
        }

        public static int[] MapFarmerRendererFeatureYOffset()
        {
            return Game1.player.bathingClothes.Value ? FarmerRendererFeatureYOffsetPerFrameMapped : FarmerRenderer.featureYOffsetPerFrame;
        }

        const int bathingSuitTextureStartY = 576; // X is just 0
        static readonly Rectangle replaceRectangle = new Rectangle(0, 14, 16, 32-14);
        public static void UnarmBathingModels(IAssetDataForImage image, bool girl)
        {
            IRawTextureData textureData = SHelper.ModContent.Load<IRawTextureData>(girl ? "assets/PlayerBaseGirlArmless.png" : "assets/PlayerBaseBoyArmless.png");

            Rectangle sourceRectangle = new Rectangle(replaceRectangle.Left, replaceRectangle.Top, replaceRectangle.Width, replaceRectangle.Height);
            Rectangle targetRectangle = new Rectangle(replaceRectangle.Left, replaceRectangle.Top + bathingSuitTextureStartY, replaceRectangle.Width, replaceRectangle.Height);

            for (int i = 0; i < 3; i++)
            {
                for(int j = 0; j < 3; j++)
                {
                    image.PatchImage(textureData, sourceRectangle, targetRectangle, PatchMode.Replace);
                    sourceRectangle.Offset(0, 32);
                    targetRectangle.Offset(0, 32);
                }
                sourceRectangle.Offset(16, -96);
                targetRectangle.Offset(16, -96);
            }
        }
        public static void UpdateBathingSuits(IAssetDataForImage image)
        {
            IRawTextureData girlTextureData = SHelper.ModContent.Load<IRawTextureData>("assets/GirlSwimsuitArmless.png");
            IRawTextureData boyTextureData = SHelper.ModContent.Load<IRawTextureData>("assets/BoySwimsuitArmless.png");

            Rectangle targetRectangle = new Rectangle(0, 0, 16*3, 32*3);
            // source is just the whole source image so we don't need a rectangle for it

            for (int i = 0; i < 1; i++)
            {
                targetRectangle.Offset(-targetRectangle.Left, -targetRectangle.Top);
                targetRectangle.Offset(i % 10 * 192 + (108 * 16 % 96), i / 10 * 688 + (108 * 16 / 96 * 32));

                image.PatchImage(boyTextureData, null, targetRectangle, PatchMode.Replace);

                targetRectangle.Offset(96, 0);

                image.PatchImage(girlTextureData, null, targetRectangle, PatchMode.Replace);
            }
        }

        public static string[] SplitPath(string path)
        {
            return path.Split(new string[] { "/", "\\"}, StringSplitOptions.None);
        }

        public static void MarkAllDirty()
        {
            spriteDirty.Value = true;
            eyesDirty.Value = true;
            skinDirty.Value = true;
            shirtDirty.Value = true;
            pantsDirty.Value = true;
            shoesDirty.Value = true;
        }

        public static Texture2D GetUniqueScreenTexture(string textureName)
        {
            Texture2D texture = Game1.temporaryContent.CreateTemporary().Load<Texture2D>(textureName);
            return texture;
        }

        #endregion
    }
}
