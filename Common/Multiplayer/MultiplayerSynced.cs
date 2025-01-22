using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;

namespace Common.Multiplayer;

/// <summary>
/// A value of type T synced across multiplayer clients. Should not be used as a PerScreen value.
/// Uses mod messages to sync the data.
/// The main player contains the 'ground-truth' and dissemenates it to the other instances. When a player joins, they request the value from the
/// host, and whenever they want to change the value, they send the new version to the host which then broadcasts it to all of the players.
/// 
/// There is not currently any system for locking the data to prevent multiple instances from editing it at the same time. If that becomes necessary, I will
/// implement that in the future.
/// </summary>
/// <typeparam name="T">The type of the value to be synced.</typeparam>
public class MultiplayerSynced<T> : IMultiplayerSynced
{
    /// <summary> The synced value. Undefined before a save is loaded. </summary>
    protected T internalValue;

    /// <summary> A name for the value. Should be unique for this mod. </summary>
    public string Name {get;}

    /// <summary> The IModHelper for the mod that owns this object.</summary>
    private readonly IModHelper SHelper;

    /// <summary> The unique id for the mod that owns this object. </summary>
    private readonly string ModId;

    /// <summary> A function to initialize the value upon save load. </summary>
    private readonly Func<T> Initializer;

    /// <summary>
    /// The synced value. Undefined before a save is loaded. 
    /// </summary>
    public T Value
    {
        get
        {
            return internalValue;
        }

        set
        {
            internalValue = value;
            PushChanges();
        }
    }

    /// <summary> Whether or not the Value has been initialized for this save. </summary>
    public bool IsReady { get; protected set; } = false;

    public EventHandler<ValueChangedArgs> OnValueChanged;

    /// <summary>
    /// Creates a variable synced across all players in multiplayer.
    /// 
    /// Note that its value will be undefined until a save is loaded.
    /// </summary>
    /// <param name="helper">The IModHelper for the mod that owns this object.</param>
    /// <param name="name">A name for the value. Should be unique for this mod.</param>
    /// <param name="initializer">A function to initialize the value upon save load.</param>
    public MultiplayerSynced(Mod mod, string name, Func<T> initializer)
    {
        Name = name;
        SHelper = mod.Helper;
        ModId = mod.ModManifest.UniqueID;
        Initializer = initializer;

        SHelper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageRecieved;
        SHelper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
        SHelper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
        SHelper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
    }

    /// <summary>
    /// Method used by <see cref="MultiplayerSyncedGroup"/>
    /// </summary>
    /// <param name="name"></param>
    /// <param name="initializer"></param>
    internal MultiplayerSynced(string name, IModHelper helper, string modId, Func<T> initializer)
    {
        Name = name;
        SHelper = helper;
        ModId = modId;
        Initializer = initializer;
    }

    protected virtual void Multiplayer_ModMessageRecieved(object sender, ModMessageReceivedEventArgs args)
    {
        if(args.FromModID != ModId)
        {
            return;
        }

        // If the main player recieves a request, send the Value to the requesting player
        if(args.Type == Name + "Request" && Context.IsMainPlayer)
        {
            SendModelTo(new long[] {args.FromPlayerID});
            return;
        }

        // If the main player recieves a response from a remote player, they update their Value and broadcast the new value;
        // If a remote player recieves a response from the main player, they update their Value.
        if(args.Type == Name + "Response")
        {
            UpdateValueFrom(args);

            // If this is the main player, broadcast the new value
            if (Context.IsMainPlayer)
            {
                SendModelTo(null);
            }

            return;
        }
    }

    /// <summary>
    /// This is called immediately upon the main player loading the save or a farmhand joining.
    /// This uses high event priority so that the value is ready in mods' SaveLoaded events *on the host computer*.
    /// 
    /// If this is the main player, it uses the initializer to initialize the value.
    /// If this is not on the host computer, it requests the value from the host.
    /// </summary>
    [EventPriority(EventPriority.High)]
    private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs args)
    {
        if(Context.IsMainPlayer)
        {
            internalValue = Initializer();
        }

        if(Context.IsOnHostComputer)
        {
            IsReady = true;
            return;
        }

        // If the player is remote and they haven't already received the value, request the value from the host.
        if(!IsReady)
        {
            SHelper.Multiplayer.SendMessage("", Name + "Request", new string[] { ModId }, null);
        }
    }

    protected virtual void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs args)
    {
        IsReady = false;
    }

    /// <summary>
    /// If this is the host instance, send the connecting player the value.
    /// 
    /// This is to try and reduce the amount of time it takes before the value is ready for remote players.
    /// If they haven't received it when they get to their SaveLoaded event, they will still request it to be safe (idk if theres a chance for this event to not fire or messages to be dropped or smth).
    /// </summary>
    private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs args)
    {
        if(!Context.IsMainPlayer || !args.Peer.HasSmapi || args.Peer.IsSplitScreen || args.Peer.IsHost || args.Peer.GetMod(ModId) is null || !IsReady)
        {
            return;
        }

        SendModelTo(new long[] { args.Peer.PlayerID });
    }

    /// <summary>
    /// Sends the current value to the given players. If given null as the parameter, will send the value to all players.
    /// </summary>
    /// <param name="UniqueMultiplayerIds">The unique ids of the players to send the value to, or null to send it to all players.</param>
    public void SendModelTo(long[] UniqueMultiplayerIds)
    {
        SHelper.Multiplayer.SendMessage(new Message(internalValue, Context.IsMainPlayer), Name + "Response", new string[] {ModId}, UniqueMultiplayerIds);
    }

    /// <summary>
    /// Sends the Value to the other players. If this is the host player, then it simply broadcasts the new value to all other players. If
    /// it is not the host player, it sends its change to the host player, which will then update its own and broadcast the new value.
    /// 
    /// Note that this should be called after any changes to the fields of the value; i.e. whenever the value is changed without direct assignment.
    /// </summary>
    public virtual void PushChanges()
    {
        SendModelTo(Context.IsMainPlayer ? null : new long[] { Game1.MasterPlayer.UniqueMultiplayerID });

        if (OnValueChanged is not null)
            OnValueChanged(this, new ValueChangedArgs(internalValue, internalValue, true));
    }

    public void UpdateValueFrom(ModMessageReceivedEventArgs message)
    {
        Message data = message.ReadAs<Message>();

        // Main players ignore other main players (impossible situation) and remote players ignore other remote players (in case of desyncs??? idk if that can happen, but the main player should be ground truth)
        if ((data.IsFromMainPlayer && !Context.IsOnHostComputer) || (!data.IsFromMainPlayer && Context.IsMainPlayer))
        {
            internalValue = data.Value;
            IsReady = true;
        }
    }

    public void Initialize()
    {
        Value = Initializer();
        IsReady = true;
    }

    public void Invalidate()
    {
        IsReady = false;
    }

    public struct Message
    {
        public bool IsFromMainPlayer = false;
        public T Value = default;

        public Message(T value, bool fromMainPlayer)
        {
            IsFromMainPlayer = fromMainPlayer;
            Value = value;
        }
    }

    public struct ValueChangedArgs
    {
        /// <summary> Whether or not the change came from this instance of the game. </summary>
        public bool FromLocal;
        /// <summary> The old value. May not be accurate if FromLocal is true. </summary>
        public T OldValue;
        /// <summary> The new value. </summary>
        public T NewValue;

        public ValueChangedArgs(T oldValue, T newValue, bool fromLocal)
        {
            OldValue = oldValue;
            NewValue = newValue;
            FromLocal = fromLocal;
        }
    }
}