#nullable enable

using StardewModdingAPI.Events;
using StardewValley.Menus;
using System;

namespace Common.Integrations;

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

    #endregion

}

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