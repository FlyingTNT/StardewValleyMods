using StardewModdingAPI;
using StardewModdingAPI.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Common.Multiplayer;

/// <summary>
/// A class that manages multiple MultiplayerSynced instances. All this really does is combine all of their events into one so that we aren't adding an excessive amount of events.
/// The performance gain from this is probably negligable, but I think it's cleaner.
/// </summary>
public class MultiplayerSyncedGroup
{
    #if DEBUG
    private const bool DEBUG_LOG = true;
    #else
    private const bool DEBUG_LOG = false;
    #endif

    private readonly IMonitor monitor;
    private readonly IModHelper helper;
    private readonly string ModId;

    private readonly Dictionary<string, IMultiplayerSynced> managedValues = new();

    public MultiplayerSyncedGroup(Mod mod)
    {
        monitor = mod.Monitor;
        helper = mod.Helper;
        ModId = mod.ModManifest.UniqueID;

        helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageRecieved;
        helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
        helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
    }

    public MultiplayerSynced<T> AddSyncedValue<T>(string name, Func<T> initializer)
    {
        MultiplayerSynced<T> synced = new(name, helper, ModId, initializer);
        
        managedValues.Add(name, synced);

        if(Context.IsWorldReady)
        {
            Initialize(synced);
        }

        return synced;
    }

    /// <summary>
    /// Checks whether the value with the given name is ready. 
    /// 
    /// In general, this should not be used and you should just store the instance returned by <see cref="AddSyncedValue{T}(string, Func{T})"/>
    /// </summary>
    public bool IsReady(string name)
    {
        return managedValues.TryGetValue(name, out IMultiplayerSynced synced) && synced.IsReady;
    }

    /// <summary>
    /// Tries to get the synced value with the given name. 
    /// 
    /// In general, this should not be used and you should just store the instance returned by <see cref="AddSyncedValue{T}(string, Func{T})"/>
    /// </summary>
    /// <returns>
    /// True if there is a value with the given name that is ready and has the specified generic type.
    /// </returns>
    public bool TryGetValue<T>(string name, [NotNullWhen(true)] out T value)
    {
        if(!managedValues.TryGetValue(name, out IMultiplayerSynced synced))
        {
            value = default;
            return false;
        }

        if(!synced.IsReady)
        {
            value = default;
            return false;
        }

        if(synced is not MultiplayerSynced<T> typed)
        {
            value = default;
            return false;
        }

        value = typed.Value;
        return true;
    }

    private void Multiplayer_ModMessageRecieved(object sender, ModMessageReceivedEventArgs args)
    {
        if (args.FromModID != ModId)
        {
            return;
        }

        bool isResponse = false;
        string name;

        if(args.Type.EndsWith("Response"))
        {
            isResponse = true;
            name = args.Type[..^8]; // 8 = length of 'Response'
        }
        else if(args.Type.EndsWith("Request"))
        {
            name = args.Type[..^7]; // 7 = length of 'Request'
        }
        else
        {
            // Unrecognized message type
            return;
        }
        if(DEBUG_LOG)
        {
            monitor.Log($"Received {(isResponse ? "response" : "request")} for {name}.");
        }

        // We don't need to do anything for requests if we're not the main player
        if((!isResponse && !Context.IsMainPlayer))
        {
            return;
        }

        if(!managedValues.TryGetValue(name, out IMultiplayerSynced synced))
        {
            return;
        }

        // If the main player recieves a request, send the Value to the requesting player
        if (!isResponse)
        {
            if (DEBUG_LOG)
            {
                monitor.Log($"Sending the value.");
            }
            synced.SendModelTo(new long[] { args.FromPlayerID });
            return;
        }

        // If the main player recieves a response from a remote player, they update their Value and broadcast the new value;
        // If a remote player recieves a response from the main player, they update their Value.
        synced.UpdateValueFrom(args);
        if (DEBUG_LOG)
        {
            monitor.Log($"Updating the value.");
        }

        // If this is the main player, broadcast the new value
        if (Context.IsMainPlayer)
        {
            synced.SendModelTo(null);
            if (DEBUG_LOG)
            {
                monitor.Log($"Propogating the new value.");
            }
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
        if (Context.IsMainPlayer)
        {
            foreach(var synced in managedValues.Values)
            {
                synced.Initialize();
            }
        }
        else
        {
            foreach((var name, var value) in managedValues)
            {
                if(!value.IsReady)
                {
                    helper.Multiplayer.SendMessage("", name + "Request", new string[] { ModId }, null);
                }
            }
        }
    }

    private void GameLoop_ReturnedToTitle(object sender, ReturnedToTitleEventArgs args)
    {
        foreach(var synced in managedValues.Values)
        {
            synced.Invalidate();
        }
    }

    /// <summary>
    /// If this is the host instance, send the connecting player the value.
    /// 
    /// This is to try and reduce the amount of time it takes before the value is ready for remote players.
    /// If they haven't received it when they get to their SaveLoaded event, they will still request it to be safe (idk if theres a chance for this event to not fire or messages to be dropped or smth).
    /// </summary>
    private void Multiplayer_PeerConnected(object sender, PeerConnectedEventArgs args)
    {
        if (!Context.IsMainPlayer || !args.Peer.HasSmapi || args.Peer.IsSplitScreen || args.Peer.IsHost || args.Peer.GetMod(ModId) is null)
        {
            return;
        }

        foreach(var synced in managedValues.Values)
        {
            if(synced.IsReady)
            {
                synced.SendModelTo(new long[] { args.Peer.PlayerID });
            }
        }
    }

    private void Initialize(IMultiplayerSynced synced)
    {
        if (Context.IsMainPlayer)
        {
            synced.Initialize();
            return;
        }

        // If the player is remote and they haven't already received the value, request the value from the host.
        if (!synced.IsReady)
        {
            helper.Multiplayer.SendMessage("", synced.Name + "Request", new string[] { ModId }, null);
        }
    }
}