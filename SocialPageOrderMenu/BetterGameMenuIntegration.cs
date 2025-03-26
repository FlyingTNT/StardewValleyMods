using Common.Integrations;
using StardewModdingAPI;
using StardewValley.Menus;
using StardewValley;
using System;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;

namespace SocialPageOrderRedux
{
    partial class ModEntry
    {
        public static IBetterGameMenuApi API => BetterGameMenuAPI;

        public static void Setup(IBetterGameMenuApi api)
        {
            api.OnTabChanged(BetterGameMenuChangeTab);
            api.OnMenuCreated(BetterGameMenuMenuCreated);
            api.OnPageReadyToClose(BetterGameMenuPageReadyToClose);
            api.OnPageOverlayCreation(BetterGameMenuPageOverlayCreation);
        }

        public static void BetterGameMenuPageOverlayCreation(IPageOverlayCreationEvent pageOverlayCreationEvent)
        {
            if(pageOverlayCreationEvent.Page is SocialPage page)
            {
                pageOverlayCreationEvent.AddOverlay(new SocialPageOrderOverlay(page));
            }
        }

        public static void BetterGameMenuPageReadyToClose(IPageReadyToCloseEvent readyToCloseEvent)
        {
            try
            {
                if (filterField.Value is null)
                {
                    return;
                }

                if (readyToCloseEvent.Page is not SocialPage)
                {
                    return;
                }

                if(SHelper.Input.IsDown(SButton.Escape))
                {
                    return;
                }

                if (readyToCloseEvent.Reason is not PageReadyToCloseReason.TabChanging or PageReadyToCloseReason.TabReloading)
                {
                    readyToCloseEvent.ReadyToClose &= !filterField.Value.Selected;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuPageReadyToClose)}: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Prevents a null error due to the order that BetterGameMenu events fire relative to our patches.
        /// </summary>
        public static void BetterGameMenuMenuCreated(IClickableMenu menu)
        {
            try
            {
                InitElements();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuMenuCreated)}: {ex}", LogLevel.Error);
            }
        }

        public static void BetterGameMenuChangeTab(ITabChangedEvent e)
        {
            try
            {
                if (BetterGameMenuAPI.ActivePage is SocialPage page)
                {
                    if (Config.UseFilter && filterField.Value is not null && !Game1.options.gamepadControls)
                        filterField.Value.Selected = Config.SearchBarAutoFocus;

                    // I don't think this is necessary, but I'm not removing it because it also isn't hurting anything.
                    ApplyFilter(page);
                    if (Game1.options.SnappyMenus)
                    {
                        page.snapToDefaultClickableComponent();
                    }
                }
                else
                {
                    filterField.Value.Selected = false;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed in {nameof(BetterGameMenuChangeTab)}: {ex}", LogLevel.Error);
            }
        }
    }

    public class SocialPageOrderOverlay : IPageOverlay
    {
        private readonly SocialPage Page;

        public SocialPageOrderOverlay(SocialPage page)
        {
            Page = page;
            OnActivate();
        }

        public void Dispose()
        {
            
        }

        public void Draw(SpriteBatch batch)
        {
            ModEntry.DrawDropDown(Page, batch);
        }

        public void GamePadButtonHeld(Buttons button, out bool suppressEvent)
        {
            suppressEvent = false;
        }

        public void LeftClickHeld(int x, int y)
        {
            
        }

        public void OnActivate()
        {
            ModEntry.SocialPage_Constructor_Postfix(Page);
            ModEntry.SocialPage_FindSocialCharacters_Postfix(Page.SocialEntries);
            Page.updateSlots();
            ModEntry.UpdateElementPositions(Page);
        }

        public void OnDeactivate()
        {
            
        }

        public void PageSizeChanged(Rectangle oldSize, Rectangle newSize)
        {
            ModEntry.UpdateElementPositions(Page);
        }

        public void PerformHoverAction(int x, int y, out bool suppressEvent)
        {
            ModEntry.SocialPage_performHoverAction_Postfix(Page, x, y, ref Page.hoverText);
            suppressEvent = false;
        }

        public void PopulateClickableComponents()
        {
            ModEntry.IClickableMenu_populateClickableComponentList_Postfix(Page);
        }

        public void PreDraw(SpriteBatch batch)
        {
            
        }

        public bool ReadyToClose()
        {
            return true;
        }

        public void ReceiveGamePadButton(Buttons button, out bool suppressEvent)
        {
            suppressEvent = false;
        }

        public void ReceiveKeyPress(Keys key, out bool suppressEvent)
        {
            suppressEvent = !ModEntry.SocialPage_recieveKeyPress_Prefix(Page, key);
        }

        public void ReceiveLeftClick(int x, int y, bool playSound, out bool suppressEvent)
        {
            suppressEvent = !ModEntry.SocialPage_receiveLeftClick_Prefix(Page, x, y);
        }

        public void ReceiveRightClick(int x, int y, bool playSound, out bool suppressEvent)
        {
            suppressEvent = false;
        }

        public void ReceiveScrollWheelAction(int direction, out bool suppressEvent)
        {
            suppressEvent = false;
        }

        public void ReleaseLeftClick(int x, int y)
        {
            ModEntry.SocialPage_releaseLeftClick_Prefix(Page, x, y);
        }

        public void Update(GameTime time, out bool suppressEvent)
        {
            suppressEvent = false;
        }
    }
}
