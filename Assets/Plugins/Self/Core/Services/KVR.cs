using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IKVRWatcher {
    void KVRReevaluate();
}

/// <summary>
/// A key-value repository in which all game data is stored. 
/// </summary>
public static class KVR {
    [Serializable]
    public struct Restraint {
        public enum Relation: byte {
            EQUAL,
            NEQ,
            GT,
            GEQ,
            LT,
            LEQ
        }

        public Relation relation;
        public RString key;
        public RInt value;

        public bool Satisfied() {
            int actual = GetValue(key);
            if (relation == Relation.EQUAL) return actual == value;
            if (relation == Relation.NEQ) return actual != value;
            if (relation == Relation.GT) return actual > value;
            if (relation == Relation.GEQ) return actual >= value;
            if (relation == Relation.LT) return actual < value;
            if (relation == Relation.LEQ) return actual <= value;
            return false;
        }

        public void Watch(IKVRWatcher watcher) {
            KVR.Watch(key, watcher);
        }
    }
    private static readonly Dictionary<string, int> kvr = new Dictionary<string, int>();
    private static readonly Dictionary<string, HashSet<IKVRWatcher>> watchers = 
        new Dictionary<string, HashSet<IKVRWatcher>>();

    //TODO run from GameManagement
    public static void InitializeFromFile() {
        
    }

    public static void SaveToFile() {
        
    }

    /// <summary>
    /// Get a value from the KVR.
    /// </summary>
    /// <param name="key">Key to retrieve.</param>
    /// <returns>The value mapped to a key; 0 if it does not exist.</returns>
    public static int GetValue(string key) {
        /*if (kvr.ContainsKey(key)) {
            Debug.Log($"Requested KVR key {key}, returning {kvr[key]}");
        } else {
            Debug.Log($"KVR key {key} does not exist, returning default 0");
        }*/
        return kvr.TryGetValue(key, out int val) ? val : 0;
    }

    /// <summary>
    /// Set a value to the KVR.
    /// </summary>
    /// <param name="key">Key to set.</param>
    /// <param name="value">Value to set.</param>
    /// <param name="_override">If the key already exists, it will be rewritten iff this is true.</param>
    /// <returns></returns>
    public static bool SetValue(string key, int value, bool _override) {
        if (!_override && kvr.ContainsKey(key)) return false;
        kvr[key] = value;
        TriggerWatchers(key);
        return true;
    }

    private static void TriggerWatchers(string key) {
        if (watchers.ContainsKey(key)) {
            foreach (var watcher in watchers[key]) {
                watcher.KVRReevaluate();
            }
        }
    }

    public static void Watch(string key, IKVRWatcher watcher) {
        if (!watchers.ContainsKey(key)) {
            watchers[key] = new HashSet<IKVRWatcher>();
        }
        watchers[key].Add(watcher);
    }

    public static void ClearWatchers() {
        watchers.Clear();
    }
}