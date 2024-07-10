using StardewModdingAPI.Events;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Linq;

namespace Common.Multiplayer;

/// <summary>
/// A special type of MultiplayerSynced for dictionaries that reduces the amount of data that needs to be sent through mod messages.
/// It is reccomended that you avoid using the Value field as changes to it will not be synced automatically, and syncing it through
/// PushChanges will generally require transferring more data than using the dictionary methods, which only transfer the relevant kvp.
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <typeparam name="TValue"></typeparam>
public class MultiplyerSyncedDict<TKey, TValue> : MultiplayerSynced<Dictionary<TKey, TValue>>, IDictionary<TKey, TValue>
{
    public EventHandler<KeyChangedArgs> OnKeyChanged;

    /// <summary>
    /// Creates a variable synced across all players in multiplayer.
    /// 
    /// Note that its value will be undefined until a save is loaded.
    /// </summary>
    /// <param name="helper">The IModHelper for the mod that owns this object.</param>
    /// <param name="name">A name for the value. Should be unique for this mod.</param>
    /// <param name="initializer">A function to initialize the value upon save load.</param>
    public MultiplyerSyncedDict(IModHelper helper, string name, Func<Dictionary<TKey, TValue>> initializer) : base(helper, name, initializer) { }

    protected override void Multiplayer_ModMessageRecieved(object sender, ModMessageReceivedEventArgs args)
    {
        // The base method will handle Requests and Responses.
        base.Multiplayer_ModMessageRecieved(sender, args);

        if (args.FromModID != ModId)
        {
            return;
        }

        if(args.Type == Name + "KeyChange")
        {
            KeyMessage data = args.ReadAs<KeyMessage>();

            // Main players ignore other main players (impossible situation) and remote players ignore other remote players (in case of desyncs??? idk if that can happen, but the main player should be ground truth)
            if ((data.IsFromMainPlayer && !Context.IsOnHostComputer) || (!data.IsFromMainPlayer && Context.IsMainPlayer))
            {
                TValue oldValue = internalValue.TryGetValue(data.Key, out TValue value) ? value : default;

                if(data.ShouldRemove)
                {
                    internalValue.Remove(data.Key);
                }
                else
                {
                    internalValue[data.Key] = data.Value;
                }

                if (OnKeyChanged is not null)
                    OnKeyChanged(this, new KeyChangedArgs(data.Key, oldValue, data.Value, false));
            }

            // If this is the main player, broadcast the new value
            if (Context.IsMainPlayer)
            {
                SendKeyChangeTo(data.Key, null);
            }
        }
    }

    /// <summary>
    /// Sends the current value of the given key to the given players
    /// </summary>
    /// <param name="key">The key to send.</param>
    /// <param name="uniqueMultiplayerIds">The players to send to, or null for all players. </param>
    private void SendKeyChangeTo(TKey key, long[] uniqueMultiplayerIds)
    {
        SHelper.Multiplayer.SendMessage(
            new KeyMessage(key, internalValue.TryGetValue(key, out TValue value) ? value : default, Context.IsMainPlayer, shouldRemove: !internalValue.ContainsKey(key)),
            Name + "KeyChange",
            new string[] {ModId},
            uniqueMultiplayerIds
        );
    }

    public struct KeyMessage
    {
        public bool IsFromMainPlayer = false;
        public TKey Key = default;
        public TValue Value = default;
        public bool ShouldRemove = false;

        public KeyMessage(TKey key, TValue value, bool fromMainPlayer, bool shouldRemove = false)
        {
            IsFromMainPlayer = fromMainPlayer;
            Key = key;
            Value = value;
            ShouldRemove = shouldRemove;
        }
    }

    public struct KeyChangedArgs
    {
        public bool FromLocal = false;
        public TKey Key = default;
        public TValue OldValue = default;
        public TValue NewValue = default;

        public KeyChangedArgs(TKey key, TValue oldValue, TValue newValue, bool fromLocal)
        {
            FromLocal = fromLocal;
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    #region DictionaryMethods
    public TValue this[TKey key]
    {
        get => internalValue[key];

        set
        {
            TValue oldValue = internalValue.TryGetValue(key, out TValue o) ? o : default;
            internalValue[key] = value;
            SendKeyChangeTo(key, Context.IsMainPlayer ? null : new long[] {Game1.MasterPlayer.UniqueMultiplayerID});
            
            if(OnKeyChanged is not null)
                OnKeyChanged(this, new KeyChangedArgs(key, oldValue, value, true));
        }
    }

    public ICollection<TKey> Keys => internalValue.Keys;

    public ICollection<TValue> Values => internalValue.Values;

    public int Count => internalValue.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value)
    {
        if(internalValue.ContainsKey(key))
        {
            throw new ArgumentException($"The given key is already in the dictionary! {key}");
        }

        this[key] = value;
    }

    public void Add(KeyValuePair<TKey, TValue> item)
    {
        if (internalValue.ContainsKey(item.Key))
        {
            throw new ArgumentException($"The given key is already in the dictionary! {item.Key}");
        }

        this[item.Key] = item.Value;
    }

    public void Clear()
    {
        internalValue.Clear();
        if (OnValueChanged is not null)
            OnValueChanged(this, new ValueChangedArgs(internalValue, internalValue, true));

        SendModelTo(null);
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return internalValue.Contains(item);
    }

    public bool ContainsKey(TKey key)
    {
        return internalValue.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        int index = arrayIndex;
        foreach(var kvp in internalValue)
        {
            array[index] = kvp;
            index++;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return internalValue.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return internalValue.GetEnumerator();
    }

    public bool Remove(TKey key)
    {
        TValue oldValue = internalValue.TryGetValue(key, out TValue o) ? o : default;

        if(!internalValue.Remove(key))
            return false;

        SendKeyChangeTo(key, Context.IsMainPlayer ? null : new long[] { Game1.MasterPlayer.UniqueMultiplayerID });

        if (OnKeyChanged is not null)
            OnKeyChanged(this, new KeyChangedArgs(key, oldValue, default, true));

        return true;
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return Remove(item.Key);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if(internalValue.TryGetValue(key, out TValue value1))
        {
            value = value1;
            return true;
        }

        value = default; 
        return false;
    }
    #endregion
}