#nullable enable

using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley.Menus;
using System;

namespace Common.Integrations;

/// <summary>
/// A tab changed event is emitted whenever the currently
/// active tab of a Better Game Menu changes.
/// </summary>
public interface ITabChangedEvent
{

    /// <summary>
    /// The Better Game Menu instance involved in the event. You
    /// can use <see cref="IBetterGameMenuApi.AsMenu(IClickableMenu)"/>
    /// to get a more useful interface for this menu.
    /// </summary>
    IClickableMenu Menu { get; }

    /// <summary>
    /// The id of the tab the Game Menu was changed to.
    /// </summary>
    string Tab { get; }

    /// <summary>
    /// The id of the previous tab the Game Menu displayed.
    /// </summary>
    string OldTab { get; }

}

/// <summary>
/// A page created event is emitted whenever a new page
/// is created for a tab by Better Game Menu.
/// </summary>
public interface IPageCreatedEvent
{

    /// <summary>
    /// The Better Game Menu instance involved in the event. You
    /// can use <see cref="IBetterGameMenuApi.AsMenu(IClickableMenu)"/>
    /// to get a more useful interface for this menu.
    /// </summary>
    IClickableMenu Menu { get; }

    /// <summary>
    /// The id of the tab the page was created for.
    /// </summary>
    string Tab { get; }

    /// <summary>
    /// The id of the provider the page was created with.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// The new page that was just created.
    /// </summary>
    IClickableMenu Page { get; }

    /// <summary>
    /// If the page was previously created and is being replaced,
    /// this will be the old page instance. Otherwise, this will
    /// be <c>null</c>.
    /// </summary>
    IClickableMenu? OldPage { get; }

}

/// <summary>
/// A page ready to close event is emitted whenever Better Game Menu
/// checks if a page can be closed, such as when switching to another
/// tab or when preparing to close the menu. This can be useful for
/// overlays that have rendered their own logic over an existing
/// page implementation.
///
/// This can be used to override the result of <see cref="IClickableMenu.readyToClose"/>
/// to please be careful to not set <see cref="ReadyToClose"/> to <c>true</c>
/// if the menu isn't <em>actually ready to close</em>.
/// </summary>
public interface IPageReadyToCloseEvent
{

    /// <summary>
    /// The Better Game Menu instance involved in the event. You
    /// can use <see cref="IBetterGameMenuApi.AsMenu(IClickableMenu)"/>
    /// to get a more useful interface for this menu.
    /// </summary>
    IClickableMenu Menu { get; }

    /// <summary>
    /// The id of the tab the Game Menu is checking can be closed.
    /// </summary>
    string Tab { get; }

    /// <summary>
    /// The id of the provider the page was created with.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// The page instance associated with the tab.
    /// </summary>
    IClickableMenu Page { get; }

    /// <summary>
    /// Whether or not the menu is ready to be closed.
    /// </summary>
    bool ReadyToClose { get; set; }

    /// <summary>
    /// The reason why this event was fired.
    /// </summary>
    PageReadyToCloseReason Reason { get; }
}

/// <summary>
/// The reason why a <see cref="IPageReadyToCloseEvent"/> event is fired.
/// </summary>
public enum PageReadyToCloseReason
{
    /// <summary>The reason for this event wasn't tracked.</summary>
    Unknown = 0,
    /// <summary>The Game Menu is attempting to close.</summary>
    MenuClosing = 1,
    /// <summary>The Game Menu is trying to switch to a new tab.</summary>
    TabChanging = 2,
    /// <summary>The Game Menu wants to recreate the current tab instance.</summary>
    TabReloading = 3,
    /// <summary>The user tried to open a tab context menu.</summary>
    TabContextMenu = 4,
}

/// <summary>
/// A page overlay creation event can be used by other mods to add overlays
/// to a menu page. Overlays allow you to wrap various <see cref="IClickableMenu"/>
/// events and potentially prevent them from firing.
/// </summary>
public interface IPageOverlayCreationEvent
{

    /// <summary>
    /// The Better Game Menu instance involved in the event. You
    /// can use <see cref="IBetterGameMenuApi.AsMenu(IClickableMenu)"/>
    /// to get a more useful interface for this menu.
    /// </summary>
    IClickableMenu Menu { get; }

    /// <summary>
    /// The id of the tab the page belongs to.
    /// </summary>
    string Tab { get; }

    /// <summary>
    /// The id of the provider the page was created with.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// The page to get an overlay for.
    /// </summary>
    IClickableMenu Page { get; }

    /// <summary>
    /// Add an overlay to the menu page. Overlays are executed in the order they're added.
    /// </summary>
    /// <param name="overlay">The overlay to add. Note that this isn't an <see cref="IPageOverlay"/>
    /// since we do special proxying for performance. All methods are optional.</param>
    /// <exception cref="InvalidCastException">Thrown if the provided instance cannot be proxied correctly.</exception>
    void AddOverlay(IDisposable overlay);

}


/// <summary>
/// A page overlay allows you to encapsulate logic that runs over top of an
/// existing menu in a simple way, rather than needing harmony patches or other
/// events. All of the methods listed here are optional and can be omitted
/// from your overlay. This interface is provided as a guide.
///
/// Overlays are designed to have short lifetimes. They're created whenever
/// a page instance is created and disposed of before the page instance is
/// closed. If a page instance is recreated, overlays are also recreated.
/// </summary>
public interface IPageOverlay : IDisposable
{

    #region Life Cycle and Updates

    /// <summary>
    /// This is called whenever the overlayed page becomes the current page
    /// of the menu. This is not called when the overlay is first created.
    /// </summary>
    void OnActivate();

    /// <summary>
    /// This is called whenever the overlayed page becomes inactive, such
    /// as when the menu changes to a different page. This is not called
    /// when the overlay is about to be disposed due to the page instance
    /// being closed.
    /// </summary>
    void OnDeactivate();

    /// <summary>
    /// This is called when checking if the overlayed page is ready to close.
    /// Returning <c>false</c> prevents the page from closing.
    /// </summary>
    bool ReadyToClose();

    /// <summary>
    /// This is called before <see cref="IClickableMenu.update(GameTime)"/>
    /// </summary>
    /// <param name="time">The current time.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void Update(GameTime time, out bool suppressEvent);

    /// <summary>
    /// This is called after the overlayed page changes position or size
    /// due to the game window size changing.
    /// </summary>
    /// <param name="oldSize">The old window size.</param>
    /// <param name="newSize">The new window size.</param>
    void PageSizeChanged(Rectangle oldSize, Rectangle newSize);

    /// <summary>
    /// This is called after <see cref="IClickableMenu.populateClickableComponentList"/>,
    /// assuming the overlayed page calls the base method. It may be called twice.
    /// </summary>
    void PopulateClickableComponents();

    #endregion

    #region Input

    /// <summary>
    /// This is called before <see cref="IClickableMenu.receiveLeftClick(int, int, bool)"/>
    /// </summary>
    /// <param name="x">The mouse's x position.</param>
    /// <param name="y">The mouse's y position.</param>
    /// <param name="playSound">Whether or not sounds should be played.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void ReceiveLeftClick(int x, int y, bool playSound, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.leftClickHeld(int, int)"/>
    /// </summary>
    /// <param name="x">The mouse's x position.</param>
    /// <param name="y">The mouse's y position.</param>
    void LeftClickHeld(int x, int y);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.releaseLeftClick(int, int)"/>
    /// </summary>
    /// <param name="x">The mouse's x position</param>
    /// <param name="y">The mouse's y position</param>
    void ReleaseLeftClick(int x, int y);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.receiveRightClick(int, int, bool)"/>
    /// </summary>
    /// <param name="x">The mouse's x position.</param>
    /// <param name="y">The mouse's y position.</param>
    /// <param name="playSound">Whether or not sounds should be played.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void ReceiveRightClick(int x, int y, bool playSound, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.receiveScrollWheelAction(int)"/>
    /// </summary>
    /// <param name="direction">The direction the scroll was performed in.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void ReceiveScrollWheelAction(int direction, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.performHoverAction(int, int)"/>
    /// </summary>
    /// <param name="x">The mouse's x position.</param>
    /// <param name="y">The mouse's y position.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void PerformHoverAction(int x, int y, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.receiveKeyPress(Keys)"/>
    /// </summary>
    /// <param name="key">The key(s) that was pressed.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void ReceiveKeyPress(Keys key, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.receiveGamePadButton(Buttons)"/>
    /// </summary>
    /// <param name="button">The button(s) that was pressed.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void ReceiveGamePadButton(Buttons button, out bool suppressEvent);

    /// <summary>
    /// This is called before <see cref="IClickableMenu.gamePadButtonHeld(Buttons)"/>
    /// </summary>
    /// <param name="button">The button(s) that was held.</param>
    /// <param name="suppressEvent">Whether or not to allow the relevant method on <see cref="IClickableMenu"/> to run.</param>
    void GamePadButtonHeld(Buttons button, out bool suppressEvent);

    #endregion

    #region Drawing

    /// <summary>
    /// This is called before <see cref="IClickableMenu.draw(SpriteBatch)"/>
    /// </summary>
    /// <param name="batch">The SpriteBatch to draw with.</param>
    void PreDraw(SpriteBatch batch);

    /// <summary>
    /// This is called after <see cref="IClickableMenu.draw(SpriteBatch)"/>
    /// </summary>
    /// <param name="batch">The SpriteBatch to draw with.</param>
    void Draw(SpriteBatch batch);

    #endregion

}

public interface IBetterGameMenuApi
{
    #region Menu Class Access
    /// <summary>
	/// Return the Better Game Menu menu implementation's type, in case
	/// you want to do spooky stuff to it, I guess.
	/// </summary>
	Type GetMenuType();

    /// <summary>
	/// Get the current page of the provided Better Game Menu instance. If the
	/// provided menu is not a Better Game Menu, or a page is not ready, then
	/// return <c>null</c> instead.
	/// </summary>
	/// <param name="menu">The menu to get the page from.</param>
	IClickableMenu? GetCurrentPage(IClickableMenu menu);

    /// <summary>
	/// The current page of the active screen's current Better Game Menu,
	/// if one is open, else <c>null</c>. This exists as a quicker alternative
	/// to <c>ActiveMenu?.CurrentPage</c> with the additional benefit that
	/// you can prune <c>IBetterGameMenu</c> from your copy of the API
	/// file if you're not using anything else from it.
	/// </summary>
	IClickableMenu? ActivePage { get; }
    #endregion

    #region Menu Events
    public delegate void TabChangedDelegate(ITabChangedEvent evt);
    public delegate void MenuCreatedDelegate(IClickableMenu menu);
    public delegate void PageCreatedDelegate(IPageCreatedEvent evt);
    public delegate void PageOverlayCreationDelegate(IPageOverlayCreationEvent evt);
    public delegate void PageReadyToCloseDelegate(IPageReadyToCloseEvent evt);

    /// <summary>
    /// This event fires whenever the current tab changes, except when a
    /// game menu is first created.
    /// </summary>
    void OnTabChanged(TabChangedDelegate handler, EventPriority priority = EventPriority.Normal);

    /// <summary>
    /// This event fires whenever the game menu is created, at the end of
    /// the menu's constructor. As such, this is called before the new <see cref="IBetterGameMenu"/>
    /// instance is assigned to <see cref="Game1.activeClickableMenu"/>.
    /// </summary>
    void OnMenuCreated(MenuCreatedDelegate handler, EventPriority priority = EventPriority.Normal);

    /// <summary>
	/// This event fires whenever a new page instance is created. This can happen
	/// the first time a page is accessed, whenever something calls
	/// <see cref="TryGetPage(string, out IClickableMenu?, bool)"/> with the
	/// <c>forceCreation</c> flag set to true, or when the menu has been resized
	/// and the tab implementation's <c>OnResize</c> method returned a new
	/// page instance.
	/// </summary>
	void OnPageCreated(PageCreatedDelegate handler, EventPriority priority = EventPriority.Normal);

    /// <summary>
	/// This event fires whenever page overlays are being created for a page. This
	/// happens the first time a page is ready to be displayed because it becomes
	/// the current page, or when the current tab's page is recreated.
	/// </summary>
	void OnPageOverlayCreation(PageOverlayCreationDelegate handler, EventPriority priority = EventPriority.Normal);

    /// <summary>
	/// This event fires whenever Better Game Menu checks if a page is ready to
	/// close, either due to the game menu closing or due to the tab being changed.
	/// </summary>
	void OnPageReadyToClose(PageReadyToCloseDelegate handler, EventPriority priority = EventPriority.Normal);
    #endregion

}