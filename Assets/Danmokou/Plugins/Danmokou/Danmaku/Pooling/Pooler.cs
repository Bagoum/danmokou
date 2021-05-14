using System;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Behavior.Items;
using Danmokou.Core;
using Danmokou.Danmaku;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable StaticMemberInGenericType

namespace Danmokou.Pooling {
public static class Pooler<T> where T : Pooled<T> {
    //Note: these dicts are specific to each typing T. They are not shared between Pooler<ParticlePooled> and Pooler<BEH>.
    private static readonly Dictionary<GameObject, HashSet<T>> active = new Dictionary<GameObject, HashSet<T>>();
    private static readonly Dictionary<GameObject, Queue<T>> free = new Dictionary<GameObject, Queue<T>>();
    private static Transform particleContainer = null!;
    private static string typName = "";

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
            created.ResetValuesEnableUpdates();
            isNew = false;
        } else {
            GameObject go = Object.Instantiate(prefab);
            created = go.GetComponent<T>() ?? go.AddComponent<T>();
            //ResetValues is called from OnEnable
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
public static class BEHPooler {
    private static GameObject inodePrefab = null!;

    public static void Prepare(GameObject inodePref) {
        inodePrefab = inodePref;
        Pooler<BehaviorEntity>.Prepare();
    }

    public static BehaviorEntity RequestUninitialized(GameObject prefab, out bool isNew) =>
        Pooler<BehaviorEntity>.Request(prefab, out isNew);

    public static BehaviorEntity INode(Movement mov, ParametricInfo pi, Vector2 rotation, string behName) {
        var beh = RequestUninitialized(inodePrefab, out _);
        beh.Initialize(mov, pi, SMRunner.Null, behName);
        beh.SetDirection(rotation);
        return beh;
    }
}

public static class GhostPooler {
    private static GameObject ghostPrefab = null!;

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

public readonly struct ItemRequestContext {
    public readonly Vector2 source;
    public readonly Vector2 offset;

    public ItemRequestContext(Vector2 src, Vector2 off) {
        source = src;
        offset = off;
    }
}

public readonly struct LabelRequestContext {
    public readonly Vector2 root;
    public readonly float radius;
    public readonly float speed;
    public readonly float angle;
    public readonly float timeToLive;
    public readonly IGradient color;
    public readonly string text;

    public LabelRequestContext(Vector2 root, float radius, float speed, float angle, float timeToLive, IGradient color,
        string text) {
        this.root = root;
        this.radius = radius;
        this.speed = speed;
        this.angle = angle;
        this.timeToLive = timeToLive;
        this.color = color;
        this.text = text;
    }
}

public static class ItemPooler {
    private static ItemReferences items = null!;

    public static void Prepare(ItemReferences itemRefs) {
        items = itemRefs;
        Pooler<Item>.Prepare();
        Pooler<DropLabel>.Prepare();
    }

    private static Item Request(GameObject prefab, ItemRequestContext ctx) {
        var i = Pooler<Item>.Request(prefab, out _);
        i.Initialize(ctx.source, ctx.offset);
        return i;
    }

    public static DropLabel RequestLabel(LabelRequestContext ctx) {
        var l = Pooler<DropLabel>.Request(items.dropLabel, out _);
        l.Initialize(ctx);
        return l;
    }

    public static Item RequestLife(ItemRequestContext ctx) => Request(items.lifeItem, ctx);
    public static Item RequestValue(ItemRequestContext ctx) => Request(items.valueItem, ctx);
    public static Item RequestSmallValue(ItemRequestContext ctx) => Request(items.smallValueItem, ctx);
    public static Item RequestPointPP(ItemRequestContext ctx) => Request(items.pointppItem, ctx);

    public static Item? RequestGem(ItemRequestContext ctx) =>
        GameManagement.Instance.Difficulty.meterEnabled ? Request(items.gemItem, ctx) : null;

    public static Item RequestPowerupShift(ItemRequestContext ctx) => Request(items.powerupShift, ctx);
    public static Item RequestPowerupD(ItemRequestContext ctx) => Request(items.powerupD, ctx);
    public static Item RequestPowerupM(ItemRequestContext ctx) => Request(items.powerupM, ctx);
    public static Item RequestPowerupK(ItemRequestContext ctx) => Request(items.powerupK, ctx);

    public static Item? RequestPower(ItemRequestContext ctx) {
        return InstanceConsts.PowerMechanicEnabled ? Request(items.powerItem, ctx) : null;
    }

    public static Item? RequestFullPower(ItemRequestContext ctx) {
        return InstanceConsts.PowerMechanicEnabled ? Request(items.fullPowerItem, ctx) : null;
    }

    public static Item Request1UP(ItemRequestContext ctx) {
        return Request(items.oneUpItem, ctx);
    }

    public static Item? RequestItem(ItemRequestContext ctx, ItemType t) =>
        t switch {
            ItemType.VALUE => RequestValue(ctx),
            ItemType.SMALL_VALUE => RequestSmallValue(ctx),
            ItemType.PPP => RequestPointPP(ctx),
            ItemType.LIFE => RequestLife(ctx),
            ItemType.POWER => RequestPower(ctx),
            ItemType.FULLPOWER => RequestFullPower(ctx),
            ItemType.ONEUP => Request1UP(ctx),
            ItemType.GEM => RequestGem(ctx),
            ItemType.POWERUP_SHIFT => RequestPowerupShift(ctx),
            ItemType.POWERUP_D => RequestPowerupD(ctx),
            ItemType.POWERUP_M => RequestPowerupM(ctx),
            ItemType.POWERUP_K => RequestPowerupK(ctx),
            _ => throw new Exception($"No drop handling for item type {t}")
        };
}
}

