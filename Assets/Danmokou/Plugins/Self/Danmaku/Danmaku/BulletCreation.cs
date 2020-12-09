using System;
using DMK.Behavior;
using DMK.Behavior.Display;
using DMK.Core;
using DMK.Danmaku.Descriptors;
using DMK.Danmaku.Options;
using DMK.Danmaku.Patterns;
using DMK.DataHoist;
using DMK.DMath;
using DMK.Pooling;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Danmaku {

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
    public Facing facing;

    //Constructor may be called from any thread; .transform is unsafe. Variable reference is safe.
    public DelegatedCreator(BehaviorEntity parent, string style, Vector2? forceRoot=null) {
        this.parent = parent;
        this.style = style;
        this.forceRoot = forceRoot;
        transformParent = null;
        cacheLoc = false;
        cachedLoc = Vector2.zero;
        facing = Facing.ORIGINAL;
    }

    private float ResolveFacing() {
        if (facing == Facing.DEROT) return 0f;
        if (facing == Facing.VELOCITY) return parent.DirectionDeg;
        if (facing == Facing.ROTVELOCITY) return parent.DirectionDeg + parent.original_angle;
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

    public void SFX() => SFXService.Request(style);

    public void Simple(SyncHandoff sbh, [CanBeNull] BPY scale, [CanBeNull] SBV2 dir, VTP path, uint? id) {
        BulletManager.RequestSimple(style, scale, dir, new Movement(path, ParentOffset, FacedRV2(sbh.rv2)), 
            sbh.index, sbh.timeOffset, id);
    }

    public void Complex(SyncHandoff sbh, VTP path, uint id, BehOptions options) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var opts = new RealizedBehOptions(options, sbh.GCX, id, ParentOffset, lrv2, sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestComplex(style, new Movement(path, ParentOffset, lrv2), sbh.index, id, ref opts);
    }

    private const float DEFAULT_REMEMBER = 3f;
    public void Pather(SyncHandoff sbh, float? maxLength, BPY remember, VTP path, uint id, BehOptions options) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var opts = new RealizedBehOptions(options, sbh.GCX, id, ParentOffset, lrv2, sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestPather(style, new Movement(path, ParentOffset, lrv2), sbh.index, id, 
            maxLength.GetValueOrDefault(DEFAULT_REMEMBER), remember, ref opts);
    }

    public void Laser(SyncHandoff sbh, VTP path, float cold, float hot, uint id, LaserOptions options) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        var opts = new RealizedLaserOptions(options, sbh.GCX, id, ParentOffset, lrv2, sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestLaser(transformParent, style, new Movement(path, ParentOffset, lrv2), 
            sbh.index, id, cold, hot, ref opts);
    }

    public void Summon(bool pooled, SyncHandoff sbh, BehOptions options, VTP path, SMRunner sm, uint bpiid) {
        V2RV2 lrv2 = FacedRV2(sbh.rv2);
        Movement vel = new Movement(path, ParentOffset, lrv2);
        BulletManager.RequestSummon(pooled, style, vel, sbh.index, bpiid, options.ID, transformParent, sm,
            new RealizedBehOptions(options, sbh.GCX, bpiid, ParentOffset, lrv2, sbh.ch.cT));
    }

    public void SummonRect(SyncHandoff sbh, string behid, TP4 color, BPRV2 loc, SMRunner sm, uint bpiid) {
        BulletManager.RequestRect(color, loc, sbh.index, bpiid, behid,transformParent, sm);
    }
    public void SummonCirc(SyncHandoff sbh, string behid, TP4 color, BPRV2 loc, SMRunner sm, uint bpiid) {
        BulletManager.RequestCirc(color, loc, sbh.index, bpiid, behid,transformParent, sm);
    }
    public void SummonPowerup(SyncHandoff sbh, TP4 color, float time, float itrs, uint bpiid) {
        BulletManager.RequestPowerup(style, color, time, itrs, sbh.GCX.exec, sbh.index, bpiid);
    }
    public void SummonPowerupStatic(SyncHandoff sbh, TP4 color, float time, float itrs, uint bpiid) {
        BulletManager.RequestPowerup(style, color, time, itrs, null, sbh.index, bpiid, FacedRV2(sbh.rv2).TrueLocation + ParentOffset);
    }
    public void SummonDarkness(SyncHandoff sbh, string behid, TP loc, BPY radius, TP4 color, SMRunner sm, uint bpiid) {
        BulletManager.RequestDarkness(loc, radius, color, sbh.index, bpiid, behid, transformParent, sm);
    }

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
    private static int sentryMax = 10000;
    private static int sentry = 0;

    private static void ResetSentry() {
        sentry = 0;
    }
    private static void CheckSentry() {
        if (++sentry > sentryMax) 
            throw new Exception($"You have summoned more than {sentryMax} bullets in one frame! " +
                                $"You probably made an error somewhere. " +
                                $"If you didn't, remove this exception in the code.");
    }
    public static void RequestSimple(string styleName, [CanBeNull] BPY scale, [CanBeNull] SBV2 dir, Movement movement, int firingIndex, float timeOffset, uint? bpiid, bool checkSentry=true) {
        if (checkSentry) CheckSentry();
        SimpleBullet sb = new SimpleBullet(scale, dir, movement, firingIndex, bpiid ?? RNG.GetUInt(), timeOffset);
        GetMaybeCopyPool(styleName).Add(ref sb, true);
    }

    private static void CopySimple(string newStyle, AbsSimpleBulletCollection sbc, int ii) {
        CheckSentry();
        ref var sb = ref sbc[ii];
        SimpleBullet nsb = new SimpleBullet(sb.scaleFunc, sb.dirFunc, sb.movement, sb.bpi.index, 
            PrivateDataHoisting.Copy(sb.bpi.id, RNG.GetUInt()), 0f);
        nsb.bpi.loc = sb.bpi.loc;
        nsb.bpi.t = sb.bpi.t;
        GetMaybeCopyPool(newStyle).Add(ref nsb, true);
    }

    public static void RequestNullSimple(string styleName, Vector2 loc, Vector2 dir) =>
        RequestSimple(styleName, null, null, new Movement(loc, dir), 0, 0, null, false);

    public static void RequestComplex(string style, Movement movement, int firingIndex, uint bpiid, ref RealizedBehOptions opts) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            var bullet = (Bullet) BEHPooler.RequestUninitialized(bsm.recolor.GetOrLoadRecolor().prefab, out _);
            bullet.Initialize(bsm, opts, null, movement, firingIndex, bpiid, main.bulletCollisionTarget, out _);
        } else throw new Exception("Could not find complex bullet style: " + style);
    }
    public static void RequestPather(string style, Movement movement, int firingIndex, uint bpiid, float maxRemember, BPY remember, ref RealizedBehOptions opts) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            Pather.Request(bsm, movement, firingIndex, bpiid, maxRemember, remember, main.bulletCollisionTarget, ref opts);
        } else throw new Exception("Pather must be an faBulletStyle: " + style);
    }
    public static void RequestLaser(BehaviorEntity parent, string style, Movement vel, int firingIndex,
        uint bpiid, float cold, float hot, ref RealizedLaserOptions options) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            Laser.Request(bsm, parent, vel, firingIndex, bpiid, cold, hot, main.bulletCollisionTarget, ref options);
        } else throw new Exception("Laser must be an faBulletStyle: " + style);
    }
    
    public static BehaviorEntity RequestSummon(bool pooled, string prefabName, Movement path, 
        int firingIndex, uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm, RealizedBehOptions? opts) {
        CheckSentry();
        if (CheckComplexPool(prefabName, out var bsm)) {
            BehaviorEntity beh = pooled ?
                BEHPooler.RequestUninitialized(ResourceManager.GetSummonable(prefabName), out _) :
                GameObject.Instantiate(ResourceManager.GetSummonable(prefabName)).GetComponent<BehaviorEntity>();
            beh.Initialize(bsm, path, sm, firingIndex, bpiid, parent, behName, opts);
            return beh;
        } else throw new Exception("No valid summonable by name: " + prefabName);
    }

    public static BehaviorEntity RequestRawSummon(string prefabName) =>
        GameObject.Instantiate(ResourceManager.GetSummonable(prefabName)).GetComponent<BehaviorEntity>();

    public static ShapeDrawer RequestDrawer(string kind, int firingIndex, 
        uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm) => RequestSummon(true, kind, Movement.None, firingIndex, bpiid, behName,
            parent, sm, null).GetComponent<ShapeDrawer>();

    public static RectDrawer RequestRect(TP4 color, BPRV2 locScaleRot, int firingIndex,
        uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm) {
        var rect = RequestDrawer("rect", firingIndex, bpiid, behName, parent, sm) as RectDrawer;
        rect.Initialize(color, locScaleRot);
        return rect;
    }
    public static CircDrawer RequestCirc(TP4 color, BPRV2 locScaleRot, int firingIndex,
        uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm) {
        var circ = RequestDrawer("circ", firingIndex, bpiid, behName, parent, sm) as CircDrawer;
        circ.Initialize(color, locScaleRot);
        return circ;
    }

    public static DarknessDrawer RequestDarkness(TP loc, BPY radius, TP4 color, int firingIndex, 
        uint bpiid, string behName, [CanBeNull] BehaviorEntity parent, SMRunner sm) {
        var dark = RequestSummon(false, "darkness", Movement.None, firingIndex, 
                bpiid, behName, parent, sm, null).GetComponent<DarknessDrawer>();
        dark.Initialize(loc, radius, color);
        return dark;
    }

    public static PowerUp RequestPowerup(string style, TP4 color, float time, float itrs, [CanBeNull] BehaviorEntity parent, 
        int firingIndex, uint bpiid, Vector2? offset = null) {
        Movement vel = offset.HasValue ? new Movement(offset.Value, 0f) : Movement.None;
        var pw = RequestSummon(true, style, vel, firingIndex, bpiid, "_", parent,
            new SMRunner(), null).GetComponent<PowerUp>();
        pw.Initialize(color, time, itrs);
        return pw;
    }
}

}