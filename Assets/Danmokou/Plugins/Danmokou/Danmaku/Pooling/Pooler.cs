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
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;
using static Danmokou.Services.GameManagement;

// ReSharper disable StaticMemberInGenericType

namespace Danmokou.Pooling {
public static class Pooler<T> where T : Pooled<T> {
    //Note: these dicts are specific to each typing T. They are not shared between Pooler<ParticlePooled> and Pooler<BEH>.
    private static readonly Dictionary<GameObject, HashSet<T>> active = new Dictionary<GameObject, HashSet<T>>();
    private static readonly Dictionary<GameObject, Queue<T>> free = new Dictionary<GameObject, Queue<T>>();

    static Pooler() {
        if (!Application.isPlaying) return;
        SceneIntermediary.SceneUnloaded.Subscribe(_ => OrphanAll());
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
}

public static class ParticlePooler {
    public static ParticlePooled Request(GameObject prefab, Vector2 location) {
        ParticlePooled n = Pooler<ParticlePooled>.Request(prefab, out bool _);
        n.Initialize(location);
        return n;
    }
}
public static class BEHPooler {
    public static BehaviorEntity RequestUninitialized(GameObject prefab, out bool isNew) =>
        Pooler<BehaviorEntity>.Request(prefab, out isNew);

    public static BehaviorEntity INode(Movement mov, ParametricInfo pi, Vector2 rotation, string behName) {
        var beh = RequestUninitialized(Prefabs.inode, out _);
        beh.Initialize(mov, pi, SMRunner.Null, behName);
        beh.SetMovementDelta(rotation);
        return beh;
    }
}

public static class GhostPooler {
    public static CutinGhost Request(Vector2 loc, Vector2 dir, Cutin.GhostConfig cfg) {
        var cg = Pooler<CutinGhost>.Request(Prefabs.cutinGhost, out _);
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
    private readonly float multiplier;

    public float Scale(float timeRatio) => 0.7f + (multiplier - 0.7f) * (float)Math.Sin(Math.PI * (0.25 + 0.75 * timeRatio));

    public LabelRequestContext(Vector2 root, float radius, float speed, float angle, float timeToLive, IGradient color,
        string text, float multiplier) {
        this.root = root;
        this.radius = radius;
        this.speed = speed;
        this.angle = angle;
        this.timeToLive = timeToLive * multiplier;
        this.multiplier = multiplier;
        this.color = color;
        this.text = text;
    }
}

public static class ItemPooler {
    private static ItemReferences items => References.items;

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

