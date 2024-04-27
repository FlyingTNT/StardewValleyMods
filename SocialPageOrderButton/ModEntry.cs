using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;

namespace SocialPageOrderButton
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        public static ModConfig Config;
        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        private static Texture2D buttonTexture;
        private static int xOffset = 16;
        public static readonly PerScreen<bool> wasSorted = new PerScreen<bool>(() => false);
        public static readonly PerScreen<int> currentSort = new PerScreen<int>(() => 0);

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();
            if (!Config.EnableMod)
                return;
            SMonitor = Monitor;
            SHelper = Helper;

            buttonTexture = helper.ModContent.Load<Texture2D>("assets/button.png");

            Helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;

            var harmony = new Harmony(ModManifest.UniqueID);
            harmony.PatchAll();
        }

        private void GameLoop_UpdateTicked(object sender, UpdateTickedEventArgs e)
        {

            if (Game1.activeClickableMenu is GameMenu)
            {
                if (!wasSorted.Value)
                {
                    ResortSocialList();
                    wasSorted.Value = true;
                }
            }
            else
                wasSorted.Value = false;
        }
        private void Input_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (Game1.activeClickableMenu is GameMenu && (Game1.activeClickableMenu as GameMenu).currentTab == GameMenu.socialTab)
            {
                Rectangle rect = new Rectangle(Game1.activeClickableMenu.xPositionOnScreen - xOffset, Game1.activeClickableMenu.yPositionOnScreen, buttonTexture.Width * 4, buttonTexture.Height * 4);
                if (rect.Contains(Game1.getMousePosition()))
                {
                    currentSort.Value++;
                    currentSort.Value %= 4;
                    Helper.WriteConfig(Config);
                    ResortSocialList();
                }
            }
        }

        [HarmonyPatch(typeof(SocialPage), nameof(SocialPage.draw), new Type[] { typeof(SpriteBatch) })]
        public class IClickableMenu_drawTextureBox_Patch
        {
            public static void Prefix(SpriteBatch b)
            {
                if (!Config.EnableMod)
                    return;
                b.Draw(buttonTexture, new Rectangle(Game1.activeClickableMenu.xPositionOnScreen - xOffset, Game1.activeClickableMenu.yPositionOnScreen, buttonTexture.Width * 4, buttonTexture.Height * 4), null, Color.White);
                Rectangle rect = new Rectangle(Game1.activeClickableMenu.xPositionOnScreen - xOffset, Game1.activeClickableMenu.yPositionOnScreen, buttonTexture.Width * 4, buttonTexture.Height * 4);
                if (rect.Contains(Game1.getMousePosition()))
                {
                    (Game1.activeClickableMenu as GameMenu).hoverText = SHelper.Translation.Get($"sort-{currentSort.Value}");
                }
            }
        }
        public static void ResortSocialList()
        {
            if (Game1.activeClickableMenu is GameMenu)
            {
                SocialPage page = (Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab] as SocialPage;

                List<NameSpriteSlot> nameSprites = new List<NameSpriteSlot>();
                List<ClickableTextureComponent> sprites = new List<ClickableTextureComponent>(SHelper.Reflection.GetField<List<ClickableTextureComponent>>(page, "sprites").GetValue());
                for (int i = 0; i < page.SocialEntries.Count; i++)
                {
                    nameSprites.Add(new NameSpriteSlot(page.SocialEntries[i], sprites[i], page.characterSlots[i]));
                }
                switch (currentSort.Value)
                {
                    case 0: // friend asc
                        SMonitor.Log("sorting by friend asc");
                        nameSprites.Sort(delegate (NameSpriteSlot x, NameSpriteSlot y)
                        {
                            bool xIsPlayerOrNullFriendship = x.entry.IsPlayer || x.entry.Friendship is null || x.entry.IsChild;
                            bool yIsPlayerOrNullFriendship = y.entry.IsPlayer || y.entry.Friendship is null || y.entry.IsChild;
                            if (xIsPlayerOrNullFriendship && yIsPlayerOrNullFriendship)
                                return 0;

                            if (xIsPlayerOrNullFriendship)
                                return 1;

                            if (yIsPlayerOrNullFriendship)
                                return -1;

                            int c = x.entry.Friendship.Points.CompareTo(y.entry.Friendship.Points);
                            if (c == 0)
                                c = x.entry.DisplayName.CompareTo(y.entry.DisplayName);
                            return c;

                        });
                        break;
                    case 1: // friend desc
                        SMonitor.Log("sorting by friend desc");
                        nameSprites.Sort(delegate (NameSpriteSlot x, NameSpriteSlot y)
                        {
                            bool xIsPlayerOrNullFriendship = x.entry.IsPlayer || x.entry.Friendship is null || x.entry.IsChild;
                            bool yIsPlayerOrNullFriendship = y.entry.IsPlayer || y.entry.Friendship is null || y.entry.IsChild;
                            if (xIsPlayerOrNullFriendship && yIsPlayerOrNullFriendship)
                                return 0;

                            if (xIsPlayerOrNullFriendship)
                                return 1;

                            if (yIsPlayerOrNullFriendship)
                                return -1;

                            int c = -x.entry.Friendship.Points.CompareTo(y.entry.Friendship.Points);
                            if (c == 0)
                                c = x.entry.DisplayName.CompareTo(y.entry.DisplayName);
                            return c;

                        });
                        break;
                    case 2: // alpha asc
                        SMonitor.Log("sorting by alpha asc");
                        nameSprites.Sort(delegate (NameSpriteSlot x, NameSpriteSlot y)
                        {
                            if (!x.entry.IsMet && !y.entry.IsMet)
                                return 0;
                            if (!x.entry.IsMet)
                                return 1;
                            if (!y.entry.IsMet)
                                return -1;

                            return x.entry.DisplayName.CompareTo(y.entry.DisplayName);
                        });
                        break;
                    case 3: // alpha desc
                        SMonitor.Log("sorting by alpha desc");
                        nameSprites.Sort(delegate (NameSpriteSlot x, NameSpriteSlot y)
                        {
                            if (!x.entry.IsMet && !y.entry.IsMet)
                                return 0;
                            if (!x.entry.IsMet)
                                return 1;
                            if (!y.entry.IsMet)
                                return -1;
                            return -x.entry.DisplayName.CompareTo(y.entry.DisplayName);
                        });
                        break;
                }
                var cslots = ((Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab] as SocialPage).characterSlots;
                for (int i = 0; i < nameSprites.Count; i++)
                {
                    nameSprites[i].slot.myID = i;
                    nameSprites[i].slot.downNeighborID = i + 1;
                    nameSprites[i].slot.upNeighborID = i - 1;
                    if (nameSprites[i].slot.upNeighborID < 0)
                    {
                        nameSprites[i].slot.upNeighborID = 12342;
                    }
                    sprites[i] = nameSprites[i].sprite;
                    nameSprites[i].slot.bounds = cslots[i].bounds;
                    cslots[i] = nameSprites[i].slot;
                    page.SocialEntries[i] = nameSprites[i].entry;
                }
                SHelper.Reflection.GetField<List<ClickableTextureComponent>>((Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab], "sprites").SetValue(new List<ClickableTextureComponent>(sprites));

                int first_character_index = 0;
                for (int l = 0; l < page.SocialEntries.Count; l++)
                {
                    if (!(((SocialPage)(Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab]).SocialEntries[l].IsPlayer))
                    {
                        first_character_index = l;
                        break;
                    }
                }
                SHelper.Reflection.GetField<int>((Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab], "slotPosition").SetValue(first_character_index);
                SHelper.Reflection.GetMethod((Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab], "setScrollBarToCurrentIndex").Invoke();
                ((SocialPage)(Game1.activeClickableMenu as GameMenu).pages[GameMenu.socialTab]).updateSlots();
            }
        }
    }

    internal class NameSpriteSlot
    {
        public SocialPage.SocialEntry entry;
        public ClickableTextureComponent sprite;
        public ClickableTextureComponent slot;

        public NameSpriteSlot(SocialPage.SocialEntry obj, ClickableTextureComponent clickableTextureComponent, ClickableTextureComponent slotComponent)
        {
            entry = obj;
            sprite = clickableTextureComponent;
            slot = slotComponent;
        }
    }
}