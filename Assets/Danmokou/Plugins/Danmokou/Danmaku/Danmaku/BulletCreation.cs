using System;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.Danmaku.Options;
using Danmokou.Danmaku.Patterns;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Pooling;
using Danmokou.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Danmaku {

//This must be a struct type, in order to allow hijacking `style` in syncPatterns
public struct DelegatedCreator {
    public readonly BehaviorEntity parent;
    private bool cacheLoc;
    private Vector2 cachedLoc;
    /// <summary>
    /// Bullets (currently only LASERS and maybe summons (untested)) can be parented by other entities.
    /// </summary>
    public BehaviorEntity? transformParent;
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

    public void SFX() => ServiceLocator.SFXService.Request(style);

    private (Movement, ParametricInfo) PathHandlers(SyncHandoff sbh, GCXU<VTP> path, uint? id = null) {
        var fctx = FiringCtx.New(sbh.GCX);
        var mov = new Movement(path(sbh.GCX, fctx), ParentOffset, FacedRV2(sbh.RV2));
        var pi = new ParametricInfo(in mov, sbh.index, id, sbh.timeOffset, fctx);
        return (mov, pi);
    }
    public void Simple(SyncHandoff sbh, SBOptions options, GCXU<VTP> path, uint? id) {
        var (mov, pi) = PathHandlers(sbh, path, id);
        if (options.player.Try(out var bse)) {
            //TODO add cdframes to sb Player cmd
            pi.ctx.playerBullet = new PlayerBullet(new PlayerBulletCfg(1, bse.boss, bse.stage, bse.effStrat), pi.ctx.PlayerController);
        } else
            pi.ctx.playerBullet = null;
        BulletManager.RequestSimple(style, 
            options.scale?.Invoke(sbh.GCX, pi.ctx), 
            options.direction?.Invoke(sbh.GCX, pi.ctx), in mov, pi);
    }

    public void Complex(SyncHandoff sbh, GCXU<VTP> path, uint id, BehOptions options) {
        var (mov, pi) = PathHandlers(sbh, path, id);
        V2RV2 lrv2 = FacedRV2(sbh.RV2);
        var opts = new RealizedBehOptions(options, sbh.GCX, pi.ctx, ParentOffset, lrv2, sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestComplex(style, in mov, pi, ref opts);
    }

    private const float DEFAULT_REMEMBER = 3f;
    public void Pather(SyncHandoff sbh, float? maxLength, BPY remember, GCXU<VTP> path, uint id, BehOptions options) {
        var (mov, pi) = PathHandlers(sbh, path, id);
        var opts = new RealizedBehOptions(options, sbh.GCX, pi.ctx, ParentOffset, FacedRV2(sbh.RV2), sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestPather(style, in mov, pi, 
            maxLength.GetValueOrDefault(DEFAULT_REMEMBER), remember, ref opts);
    }

    public void Laser(SyncHandoff sbh, GCXU<VTP> path, float cold, float hot, uint id, LaserOptions options) {
        var (mov, pi) = PathHandlers(sbh, path, id);
        var opts = new RealizedLaserOptions(options, sbh.GCX, pi.ctx, ParentOffset, FacedRV2(sbh.RV2), sbh.ch.cT);
        if (opts.playerBullet != null) style = BulletManager.GetOrMakeComplexPlayerCopy(style);
        BulletManager.RequestLaser(transformParent, style, in mov, pi, cold, hot, ref opts);
    }

    public void Summon(bool pooled, SyncHandoff sbh, BehOptions options, GCXU<VTP> path, SMRunner sm, uint id) {
        var (mov, pi) = PathHandlers(sbh, path, id);
        BulletManager.RequestSummon(pooled, style, in mov, pi, options.ID, transformParent, sm,
            new RealizedBehOptions(options, sbh.GCX, pi.ctx, ParentOffset, FacedRV2(sbh.RV2), sbh.ch.cT));
    }

    public void SummonRect(SyncHandoff sbh, string behid, TP4 color, BPRV2 loc, SMRunner sm, uint bpiid) {
        BulletManager.RequestRect(color, loc, 
            new ParametricInfo(Vector2.zero, sbh.index, bpiid), behid, transformParent, sm);
    }
    public void SummonCirc(SyncHandoff sbh, string behid, TP4 color, BPRV2 loc, SMRunner sm, uint bpiid) {
        BulletManager.RequestCirc(color, loc, 
            new ParametricInfo(Vector2.zero, sbh.index, bpiid), behid, transformParent, sm);
    }
    public void SummonPowerAura(SyncHandoff sbh, PowerAuraOptions options, uint bpiid) {
        var _style = style;
        var index = sbh.index;
        Action SummonWithRealized(RealizedPowerAuraOptions rap) => () =>
            BulletManager.RequestPowerAura(_style!, index, bpiid, rap);
        
        BulletManager.RequestPowerAura(style, sbh.index, bpiid, 
            new RealizedPowerAuraOptions(options, sbh.GCX, ParentOffset, sbh.ch.cT, SummonWithRealized));
    }
    public void SummonDarkness(SyncHandoff sbh, string behid, TP loc, BPY radius, TP4 color, SMRunner sm, uint bpiid) {
        BulletManager.RequestDarkness(loc, radius, color, 
            new ParametricInfo(Vector2.zero, sbh.index, bpiid), behid, transformParent, sm);
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
    public static void RequestSimple(string styleName, BPY? scale, SBV2? dir, in Movement mov, ParametricInfo pi, bool checkSentry=true) {
        if (checkSentry) CheckSentry();
        SimpleBullet sb = new SimpleBullet(scale, dir, in mov, pi);
        GetMaybeCopyPool(styleName).Add(ref sb, true);
    }

    public static void RequestNullSimple(string styleName, Vector2 loc, Vector2 dir, float time=0) =>
        RequestSimple(styleName, null, null, new Movement(loc, dir), new ParametricInfo(loc, 0, t:time), false);

    public static void RequestComplex(string style, in Movement mov, ParametricInfo pi, ref RealizedBehOptions opts) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            var bullet = (Bullet) BEHPooler.RequestUninitialized(bsm.RecolorOrThrow.prefab, out _);
            bullet.Initialize(bsm, opts, null, mov, pi, main.bulletCollisionTarget, out _);
        } else throw new Exception("Could not find complex bullet style: " + style);
    }
    public static void RequestPather(string style, in Movement mov, ParametricInfo pi, float maxRemember, BPY remember, ref RealizedBehOptions opts) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            Pather.Request(bsm, mov, pi, maxRemember, remember, main.bulletCollisionTarget, ref opts);
        } else throw new Exception("Pather must be an faBulletStyle: " + style);
    }
    public static void RequestLaser(BehaviorEntity? parent, string style, in Movement mov, ParametricInfo pi, float cold, float hot, ref RealizedLaserOptions options) {
        CheckSentry();
        if (CheckComplexPool(style, out var bsm)) {
            Laser.Request(bsm, parent, mov, pi, cold, hot, main.bulletCollisionTarget, ref options);
        } else throw new Exception("Laser must be an faBulletStyle: " + style);
    }
    
    public static BehaviorEntity RequestSummon(bool pooled, string prefabName, in Movement mov, ParametricInfo pi, string behName, BehaviorEntity? parent, SMRunner sm, RealizedBehOptions? opts) {
        CheckSentry();
        if (CheckComplexPool(prefabName, out var bsm)) {
            BehaviorEntity beh = pooled ?
                BEHPooler.RequestUninitialized(ResourceManager.GetSummonable(prefabName), out _) :
                GameObject.Instantiate(ResourceManager.GetSummonable(prefabName)).GetComponent<BehaviorEntity>();
            beh.Initialize(bsm, mov, pi, sm, parent, behName, opts);
            return beh;
        } else throw new Exception("No valid summonable by name: " + prefabName);
    }

    public static BehaviorEntity RequestRawSummon(string prefabName) =>
        GameObject.Instantiate(ResourceManager.GetSummonable(prefabName)).GetComponent<BehaviorEntity>();

    public static ShapeDrawer RequestDrawer(string kind, ParametricInfo pi, string behName, BehaviorEntity? parent, SMRunner sm) => RequestSummon(true, kind, Movement.None, pi, behName, parent, sm, null).GetComponent<ShapeDrawer>();

    public static RectDrawer RequestRect(TP4 color, BPRV2 locScaleRot, ParametricInfo pi, string behName, BehaviorEntity? parent, SMRunner sm) {
        var rect = RequestDrawer("rect", pi, behName, parent, sm) as RectDrawer;
        rect!.Initialize(color, locScaleRot);
        return rect;
    }
    public static CircDrawer RequestCirc(TP4 color, BPRV2 locScaleRot, ParametricInfo pi, string behName, BehaviorEntity? parent, SMRunner sm) {
        var circ = RequestDrawer("circ", pi, behName, parent, sm) as CircDrawer;
        circ!.Initialize(color, locScaleRot);
        return circ;
    }

    public static DarknessDrawer RequestDarkness(TP loc, BPY radius, TP4 color, ParametricInfo pi, string behName, BehaviorEntity? parent, SMRunner sm) {
        var dark = RequestSummon(false, "darkness", Movement.None, pi, behName, parent, sm, null).GetComponent<DarknessDrawer>();
        dark.Initialize(loc, radius, color);
        return dark;
    }

    public static PowerAura RequestPowerAura(string style, int firingIndex, uint bpiid, in RealizedPowerAuraOptions opts) {
        Movement mov = new Movement(opts.offset, 0f);
        var pw = RequestSummon(true, style, mov, new ParametricInfo(in mov, firingIndex, bpiid), "_", opts.parent,
            new SMRunner(), null).GetComponent<PowerAura>();
        pw.Initialize(in opts);
        return pw;
    }
}

}