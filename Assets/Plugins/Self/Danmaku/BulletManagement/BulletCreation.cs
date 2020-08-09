using System;
using DMath;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {

//This must be a struct type, in order to allow hijacking `style` in syncPatterns
public struct DelegatedCreator {
    public readonly BehaviorEntity parent;
    private bool cacheLoc;
    private Vector2 cachedLoc;
    /// <summary>
    /// Bullets (currently only LASERS and maybe summons (untested)) can be parented by other entities.
    /// </summary>
    [CanBeNull] public BehaviorEntity transformParent;
    public string style;
    /// <summary>
    /// Exists iff using world coordinates instead of parent-offset.
    /// </summary>
    private Vector2? forceRoot;
    private bool useParentMoveMod;
    public Facing facing;
    public MovementModifiers modifiers;

    //Constructor may be called from any thread; .transform is unsafe. Variable reference is safe.
    public DelegatedCreator(BehaviorEntity parent, string style, Vector2? forceRoot=null) {
        this.parent = parent;
        this.style = style;
        this.forceRoot = forceRoot;
        transformParent = null;
        cacheLoc = false;
        cachedLoc = Vector2.zero;
        useParentMoveMod = true;
        facing = Facing.ORIGINAL;
        modifiers = MovementModifiers.Default;
    }

    private float ResolveFacing() {
        if (facing == Facing.DEROT) return 0f;
        if (facing == Facing.VELOCITY) return parent.RotationDeg;
        if (facing == Facing.ROTVELOCITY) return parent.RotationDeg + parent.original_angle;
        return parent.original_angle;
    }

    public V2RV2 FacedRV2(V2RV2 rv2) => rv2.RotateAll(ResolveFacing());

    public void CacheLoc() {
        cacheLoc = true;
        cachedLoc = parent.GlobalPosition();
    }

    public void Root(Vector2? root) {
        forceRoot = root;
    }

    public void IgnoreParentMoveMod() => useParentMoveMod = false;

    public void SFX() => SFXService.Request(style);

    public void Simple(SyncHandoff sbh, [CanBeNull] BPY scale, [CanBeNull] TP dir, VTP path, uint? id) {
        BulletManager.RequestSimple(style, scale, dir, new Velocity(path, ParentOffset, FacedRV2(sbh.rv2), Modifiers), 
            sbh.index, sbh.timeOffset, id);
    }

    private const float DEFAULT_REMEMBER = 3f;
    public void Pather(SyncHandoff sbh, float? maxLength, BPY remember, VTP path, uint bpiid, BehOptions options) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var m = Modifiers.ApplyOver(options.modifiers);
        var opts = new RealizedBehOptions(options, sbh.GCX, ParentOffset, lrv2, sbh.ch.cT);
        BulletManager.RequestPather(style, new Velocity(path, ParentOffset, lrv2, m), sbh.index, bpiid, 
            maxLength.GetValueOrDefault(DEFAULT_REMEMBER), remember, ref opts);
    }

    public void Laser(SyncHandoff sbh, VTP path, float cold, float hot, uint bpiid, LaserOptions options) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var opts = new RealizedLaserOptions(options, sbh.GCX, bpiid, ParentOffset, lrv2, Modifiers, sbh.ch.cT);
        BulletManager.RequestLaser(transformParent, style, new Velocity(path, ParentOffset, lrv2, Modifiers), 
            sbh.index, bpiid, cold, hot, ref opts);
    }

    public void Summon(bool pooled, SyncHandoff sbh, BehOptions options, VTP path, SMRunner sm, uint bpiid) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var m = Modifiers.ApplyOver(options.modifiers);
        Velocity vel = new Velocity(path, ParentOffset, lrv2, m);
        BulletManager.RequestSummon(pooled, style, m, vel, sbh.index, bpiid, options.ID, transformParent, sm,
            new RealizedBehOptions(options, sbh.GCX, ParentOffset, lrv2, sbh.ch.cT));
    }

    public void SummonRect(SyncHandoff sbh, string behid, TP4 color, BPRV2 loc, SMRunner sm, uint bpiid) {
        BulletManager.RequestRect(color, loc, sbh.index, bpiid, behid,transformParent, sm);
    }
    public void SummonPowerup(SyncHandoff sbh, TP4 color, float time, float itrs, uint bpiid) {
        BulletManager.RequestPowerup(style, color, time, itrs, sbh.GCX.exec, sbh.index, bpiid);
    }

    private MovementModifiers Modifiers =>
        modifiers.ApplyOver(useParentMoveMod ?
            parent.movementModifiers : MovementModifiers.Default);

    //If there is a transform-parent, then the effective parent offset is zero.
    public Vector2 ParentOffset => 
        forceRoot ?? ((transformParent != null) ? 
            Vector2.zero : 
            cacheLoc ? cachedLoc : parent.GlobalPosition());

    /// <summary>
    /// Find where a bullet would be fired for a given offset.
    /// This function is not used in the main summoning functions since the task
    /// of locating relative to parent is handled by Velocity struct.
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    public Vector2 ToRawPosition(V2RV2 offset) {
        return ParentOffset + FacedRV2(offset).TrueLocation;
    }
}

public partial class BulletManager {
    public static void RequestSimple(string styleName, [CanBeNull] BPY scale, [CanBeNull] TP dir, Velocity velocity, int firingIndex,
        float timeOffset, uint? bpiid) {
        SimpleBullet sb = new SimpleBullet(scale, dir, velocity, firingIndex, bpiid ?? RNG.GetUInt(), timeOffset);
        GetMaybeCopyPool(styleName).Add(ref sb, true);
    }

    private static void CopySimple(string newStyle, AbsSimpleBulletCollection sbc, int ii) {
        ref var sb = ref sbc[ii];
        SimpleBullet nsb = new SimpleBullet(sb.scaleFunc, sb.dirFunc, sb.velocity, sb.bpi.index, 
            PrivateDataHoisting.Copy(sb.bpi.id, RNG.GetUInt()), 0f);
        nsb.bpi.loc = sb.bpi.loc;
        nsb.bpi.t = sb.bpi.t;
        GetMaybeCopyPool(newStyle).Add(ref nsb, true);
    }

    public static void RequestNullSimple(string styleName, Vector2 loc, Vector2 dir) =>
        RequestSimple(styleName, null, null, new Velocity(loc, dir), 0, 0, null);

    public static void RequestPather(string styleName, Velocity velocity, int firingIndex, uint bpiid, float maxRemember, BPY remember, ref RealizedBehOptions opts) {
        if (bulletStyles.ContainsKey(styleName)) {
            Pather.Request(bulletStyles[styleName].GetOrLoadRecolor(), velocity, firingIndex, bpiid, maxRemember, remember, main.bulletCollisionTarget, ref opts);
        } else throw new Exception("Pather must be an faBulletStyle: " + styleName);
    }
    public static void RequestLaser(BehaviorEntity parent, string style, Velocity vel, int firingIndex,
        uint bpiid, float cold, float hot, ref RealizedLaserOptions options) {
        if (bulletStyles.ContainsKey(style)) {
            Laser.Request(bulletStyles[style].GetOrLoadRecolor(), parent, vel, firingIndex, bpiid, cold, hot, main.bulletCollisionTarget, ref options);
        } else throw new Exception("Laser must be an faBulletStyle: " + style);
    }
    
    public static BehaviorEntity RequestSummon(bool pooled, string prefabName, MovementModifiers m, Velocity path, 
        int firingIndex, uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm, RealizedBehOptions? opts) {
        BehaviorEntity beh = pooled ?
            BEHPooler.RequestUninitialized(ResourceManager.GetBEHPrefab(prefabName), out _) :
            GameObject.Instantiate(ResourceManager.GetBEHPrefab(prefabName)).GetComponent<BehaviorEntity>();
        beh.Initialize(path, m, sm, firingIndex, bpiid, parent, behName, opts);
        return beh;
    }

    public static BehaviorEntity RequestRawSummon(string prefabName) {
        return GameObject.Instantiate(ResourceManager.GetBEHPrefab(prefabName)).GetComponent<BehaviorEntity>();
    }

    public static RectDrawer RequestRect(TP4 color, BPRV2 locScaleRot, int firingIndex, 
        uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm) {
        var rect = RequestSummon(true, "rect", MovementModifiers.Default, Velocity.None, firingIndex, bpiid, behName,
            parent, sm, null).GetComponent<RectDrawer>();
        rect.Initialize(color, locScaleRot);
        return rect;
    }

    public static PowerUp RequestPowerup(string style, TP4 color, float time, float itrs, [CanBeNull] BehaviorEntity parent, 
        int firingIndex, uint bpiid) {
        var pw = RequestSummon(true, style, MovementModifiers.Default, Velocity.None, firingIndex, bpiid, "_", parent,
            new SMRunner(), null).GetComponent<PowerUp>();
        pw.Initialize(color, time, itrs);
        return pw;
    }
}

}