
using StardewModdingAPI.Events;

namespace Common.Multiplayer;

/// <summary>
/// This interface only exists to facilitate <see cref="MultiplayerSyncedGroup"/>, because it would not be possible to make a collection containing <see cref="MultiplayerSynced{T}"/> with different
/// generic types, so this pulls out all the functionality necessary for <see cref="MultiplayerSyncedGroup"/> into a non-generic onterface.
/// </summary>
internal interface IMultiplayerSynced
{
    /// <summary>
    /// The name of the value. Used to differentiate values when syncing.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Whether or not the value has been initialized (for the host instance) or received from the host (for remote instances).
    /// </summary>
    public bool IsReady { get;}

    internal void UpdateValueFrom(ModMessageReceivedEventArgs message);

    /// <summary>
    /// Send the model to the players with the given unique ids
    /// </summary>
    /// <param name="ids"> The ids to send to, or null to send to all players. </param>
    internal void SendModelTo(long[] ids);

    /// <summary>
    /// Initialize the value and mark it as ready.
    /// </summary>
    internal void Initialize();

    /// <summary>
    /// Mark the value as not ready.
    /// </summary>
    internal void Invalidate();
}