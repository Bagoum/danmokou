using System;
using System.Collections.Generic;
using Danmaku;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// ReSharper disable StaticMemberInGenericType

public static class Pooler<T> where T : Pooled<T>  {
    //Note: these dicts are specific to each typing T. They are not shared between Pooler<ParticlePooled> and Pooler<BEH>.
    private static readonly Dictionary<GameObject, HashSet<T>> active = 
        new Dictionary<GameObject, HashSet<T>>();
    private static readonly Dictionary<GameObject, Queue<T>> free = new Dictionary<GameObject, Queue<T>>();
    private static Transform particleContainer;
    private static string typName;
    //I don't use a static constructor since I need the delegates to be loaded immediately,
    //but static constructors don't guarantee when they're run. 
    public static void Prepare() {
        typName = typeof(T).Name;
        SceneIntermediary.RegisterSceneLoad(CreateParticleContainer);
        SceneIntermediary.RegisterSceneUnload(OrphanAll);
        Pooled<T>.Prepare(GetContainer);
    }

    private static Transform GetContainer() {
        return particleContainer;
    }

    private static void CreateParticleContainer() {
        GameObject go = new GameObject {
            name = typName + " Pool Container"
        };
        particleContainer = go.transform;
        particleContainer.position = Vector3.zero;
    }

    public static T Request(GameObject prefab, out bool isNew) {
        T created;
        if (!active.ContainsKey(prefab)) {
            active[prefab] = new HashSet<T>();
            free[prefab] = new Queue<T>();
        }
        if (free[prefab].Count > 0) {
            created = free[prefab].Dequeue();
            created.ResetV();
            isNew = false;
        } else {
            GameObject go = Object.Instantiate(prefab);
            created = go.GetComponent<T>() ?? go.AddComponent<T>();
            isNew = true;
        }
        active[prefab].Add(created);
        //We have two initialize functions since subimplementations may need to pass extra data
        created.SetPooled(active[prefab], free[prefab], created);
        return created;
    }

    private static void OrphanAll() {
        active.Clear();
        free.Clear();
    }
    
    public static void ForAllActive(Action<T> iter) {
        foreach (var kv in active) {
            foreach (var x in kv.Value) {
                iter(x);
            }
        }
    }
}

public static class ParticlePooler {
    public static void Prepare() {
        Pooler<ParticlePooled>.Prepare();
    }
    public static ParticlePooled Request(GameObject prefab, Vector2 location) {
        ParticlePooled n = Pooler<ParticlePooled>.Request(prefab, out bool _);
        n.Initialize(location);
        return n;
    }
}

namespace Danmaku {
public static class BEHPooler {
    private static GameObject inodePrefab;

    public static void Prepare(GameObject inodePref) {
        inodePrefab = inodePref;
        Pooler<BehaviorEntity>.Prepare();
    }

    public static BehaviorEntity RequestUninitialized(GameObject prefab, out bool isNew) => Pooler<BehaviorEntity>.Request(prefab, out isNew);

    public static BehaviorEntity INode(Vector2 parentLoc, DMath.V2RV2 localLoc, Vector2 rotation, int firingIndex, uint? bpiid, string behName) {
        var beh = RequestUninitialized(inodePrefab, out _);
        beh.Initialize(parentLoc, localLoc, SMRunner.Null, firingIndex, bpiid, behName);
        beh.FaceInDirection(rotation);
        return beh;
    }
}

public static class GhostPooler {
    private static GameObject ghostPrefab;

    public static void Prepare(GameObject ghost) {
        ghostPrefab = ghost;
        Pooler<CutinGhost>.Prepare();
    }

    public static CutinGhost Request(Vector2 loc, Vector2 dir, Cutin.GhostConfig cfg) {
        var cg = Pooler<CutinGhost>.Request(ghostPrefab, out _);
        cg.Initialize(loc, dir, cfg);
        return cg;
    } 
}

public static class ItemPooler {
    private static GameObject lifeItemPrefab;
    private static GameObject valueItemPrefab;
    private static GameObject pointppItemPrefab;
    private static GameObject powerItemPrefab;
    
    public static void Prepare(GameObject life, GameObject value, GameObject pointpp, GameObject power) {
        lifeItemPrefab = life;
        valueItemPrefab = value;
        pointppItemPrefab = pointpp;
        powerItemPrefab = power;
        Pooler<Item>.Prepare();
    }

    private static Item Request(GameObject prefab, Vector2 initialLoc) {
        var i = Pooler<Item>.Request(prefab, out _);
        i.Initialize(initialLoc);
        return i;
    }

    public static Item RequestLife(Vector2 initialLoc) => Request(lifeItemPrefab, initialLoc);
    public static Item RequestValue(Vector2 initialLoc) => Request(valueItemPrefab, initialLoc);
    public static Item RequestPointPP(Vector2 initialLoc) => Request(pointppItemPrefab, initialLoc);
    [CanBeNull]
    public static Item RequestPower(Vector2 initialLoc) {
        return CampaignData.PowerMechanicActive ?  Request(powerItemPrefab, initialLoc) : null;
    }

    [CanBeNull]
    public static Item RequestItem(Vector2 initialLoc, ItemType t) {
        if (t == ItemType.VALUE) return RequestValue(initialLoc);
        else if (t == ItemType.PPP) return RequestPointPP(initialLoc);
        else if (t == ItemType.LIFE) return RequestLife(initialLoc);
        else if (t == ItemType.POWER) return RequestPower(initialLoc);
        throw new Exception($"No drop handling for item type {t}");
    }
}


}


/*
public static class AudioPooler {
    private static GameObject prefab;
    public static void Prepare(GameObject prefab) {
        AudioPooler.prefab = prefab;
        Pooler<OneShotAudioPooled>.Prepare();
    }
    public static void Request(Vector2 location, AudioClipInfo aci) {
        OneShotAudioPooled n = Pooler<OneShotAudioPooled>.Request(prefab, location);
        n.Initialize(aci);
    }
}*/
