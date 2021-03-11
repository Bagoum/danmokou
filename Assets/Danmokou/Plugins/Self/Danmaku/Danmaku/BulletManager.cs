using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using DMK.Behavior;
using DMK.Core;
using DMK.Danmaku.Descriptors;
using DMK.DMath;
using DMK.Expressions;
using DMK.Graphics;
using DMK.Reflection;
using UnityEngine.Profiling;
using ExSBCF = System.Func<DMK.Expressions.TExSBC, DMK.Expressions.TEx<int>, DMK.Expressions.TExArgCtx, DMK.Expressions.TEx>;

namespace DMK.Danmaku {

public readonly struct SBCFp {
    public readonly ExSBCF? func;
    public readonly int priority;
    public readonly Func<ICancellee, SBCF>? lazyFunc;
    public SBCFp(ExSBCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }

    public SBCFp(Func<ICancellee, SBCF> lazy, int p) {
        func = null;
        priority = p;
        lazyFunc = lazy;
    }
}
public readonly struct SBCFc {
    private readonly SBCF? func;
    private readonly Func<ICancellee, SBCF>? lazyFunc;
    public readonly int priority;
    public SBCFc(SBCFp p) {
        func = p.func == null ? null : Compilers.SBCF(p.func);
        priority = p.priority;
        lazyFunc = p.lazyFunc;
    }

    public SBCF Func(ICancellee cT) => func ?? lazyFunc?.Invoke(cT) ?? throw new Exception("No resolution for SBCFc");

    private SBCFc(SBCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }

    public static SBCFc Manual(SBCF f, int p) => new SBCFc(f, p);
}
//Not compiled but using this for priority and lazy alternates
public readonly struct BehCFc {
    private readonly BehCF? func;
    private readonly Func<ICancellee, BehCF>? lazyFunc;
    public readonly int priority;

    public BehCFc(BehCF f, int p) {
        func = f;
        priority = p;
        lazyFunc = null;
    }
    public BehCFc(Func<ICancellee, BehCF> f, int p) {
        func = null;
        priority = p;
        lazyFunc = f;
    }
    
    public BehCF Func(ICancellee cT) => func ?? lazyFunc?.Invoke(cT) ?? throw new Exception("No resolution for BehCFc");
    
}

public partial class BulletManager {
    public const string EMPTY = "empty";

    public static void CopyPool(string newPool, string from) {
        var p = simpleBulletPools[from].CopyPool((from == "empty") ? activeCEmpty : activeCNpc, newPool);
        AddSimpleStyle(p);
        p.Activate();
    }

    private static readonly Dictionary<string, string> playerPoolCopyCache = new Dictionary<string, string>();
    public static string GetOrMakePlayerCopy(string pool) {
        //lmao i hate garbage
        if (!playerPoolCopyCache.TryGetValue(pool, out var playerPool)) {
            playerPool = playerPoolCopyCache[pool] = $"{PLAYERPREFIX}{pool}";
        }
        if (!simpleBulletPools.TryGetValue(playerPool, out var p)) {
            if (!simpleBulletPools.TryGetValue(pool, out var po)) 
                throw new Exception($"{pool} does not exist, cannot make a player variant of it");
            p = po.CopySimplePool(activePlayer, playerPool);
            AddSimpleStyle(p);
            p.SetPlayer();
        }
        if (!p.Active) p.Activate();
        return playerPool;
    }

    private const string PLAYERPREFIX = "p-";
    public static string GetOrMakeComplexPlayerCopy(string pool) {
        if (!playerPoolCopyCache.TryGetValue(pool, out var playerPool)) {
            playerPool = playerPoolCopyCache[pool] = $"{PLAYERPREFIX}{pool}";
        }
        if (!behPools.TryGetValue(playerPool, out var p)) {
            if (!behPools.TryGetValue(pool, out var po)) 
                throw new Exception($"{pool} does not exist, cannot make a player variant of it");
            p = po.MakePlayerCopy(playerPool);
            AddComplexStyle(p);
        }
        return playerPool;
    }

    private static bool IsPlayerPoolString(string pool) {
        if (pool.Length < PLAYERPREFIX.Length) return false;
        for (int ii = 0; ii < PLAYERPREFIX.Length; ++ii) {
            if (pool[ii] != PLAYERPREFIX[ii]) return false;
        }
        return true;
    }
    private static bool CheckOrCopyPool(string pool, out SimpleBulletCollection sbc) {
        if (simpleBulletPools.TryGetValue(pool, out sbc)) {
            if (!sbc.Active) sbc.Activate();
            return true;
        } else if (IsPlayerPoolString(pool)) {
            return CheckOrCopyPool(GetOrMakePlayerCopy(pool.Substring(PLAYERPREFIX.Length)), out sbc);
        }
        int splitAt = pool.IndexOf('.');
        if (splitAt == -1) return false;
        string basePool = pool.Substring(0, splitAt);
        if (!simpleBulletPools.ContainsKey(basePool)) return false;
        CopyPool(pool, basePool);
        sbc = simpleBulletPools[pool];
        return true;
    }

    //No copy functionality
    public static bool CheckComplexPool(string pool, out BehaviorEntity.BEHStyleMetadata bsm) {
        if (behPools.TryGetValue(pool, out bsm)) {
            if (!bsm.Active) bsm.Activate();
            return true;
        } else if (IsPlayerPoolString(pool)) {
            return CheckComplexPool(GetOrMakeComplexPlayerCopy(pool.Substring(PLAYERPREFIX.Length)), out bsm);
        } else return false;
    }

    public static bool PoolExists(string pool) => simpleBulletPools.ContainsKey(pool);

    public static void AssertControls(string pool, IReadOnlyList<BulletControl> controls) => GetMaybeCopyPool(pool).AssertControls(controls);

    public static AbsSimpleBulletCollection GetMaybeCopyPool(string pool) {
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }
    private static readonly ExFunction getMaybeCopyPool = ExUtils.Wrap<string>(typeof(BulletManager), "GetMaybeCopyPool");

    public override int UpdatePriority => UpdatePriorities.BM;
    public override void RegularUpdate() {
        ResetSentry();
        SimpleBulletCollection sbc;
        //Temp-last set for control updates
        for (int ii = 0; ii< activeCEmpty.Count; ++ii) {
            sbc = activeCEmpty[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii< activeNpc.Count; ++ii) {
            sbc = activeNpc[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii<activeCNpc.Count; ++ii) {
            sbc = activeCNpc[ii];
            sbc.temp_last = sbc.Count;
        }
        for (int ii = 0; ii < activePlayer.Count; ++ii) {
            sbc = activePlayer[ii];
            sbc.temp_last = sbc.Count;
        }
        //Velocity and control updates
        for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
            sbc = activeCEmpty[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        Profiler.BeginSample("NPC-fired simple bullet velocity updates");
        for (int ii = 0; ii < activeNpc.Count; ++ii) {
            sbc = activeNpc[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        Profiler.EndSample();
        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            sbc = activeCNpc[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        for (int ii = 0; ii < activePlayer.Count; ++ii) {
            sbc = activePlayer[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        if (bulletCollisionTarget.Active) {
            var hitbox = bulletCollisionTarget.Hitbox;
            Profiler.BeginSample("NPC-fired simple bullet collision checking");
            int dmg = 0; int graze = 0;
            for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
                sbc = activeCEmpty[ii];
                if (sbc.Count > 0) {
                    CollisionCheckResults ccr = sbc.CheckCollision(in hitbox);
                    dmg = Math.Max(dmg, ccr.damage);
                    graze += ccr.graze;
                }
            }
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                sbc = activeNpc[ii];
                if (sbc.Count > 0) {
                    CollisionCheckResults ccr = sbc.CheckCollision(in hitbox);
                    dmg = Math.Max(dmg, ccr.damage);
                    graze += ccr.graze;
                }
            }
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                sbc = activeCNpc[ii];
                if (sbc.Count > 0) {
                    CollisionCheckResults ccr = sbc.CheckCollision(in hitbox);
                    dmg = Math.Max(dmg, ccr.damage);
                    graze += ccr.graze;
                }
            }
            Profiler.EndSample();
            bulletCollisionTarget.Player.Hit(dmg);
            bulletCollisionTarget.Player.Graze(graze);
        } else {
            //Collision checker also does compacting/culling, which needs to occur even if there's no target
            for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
                activeCEmpty[ii].NullCollisionCleanup();
            }
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                activeNpc[ii].NullCollisionCleanup();
            }
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                activeCNpc[ii].NullCollisionCleanup();
            }
        }
        
        //Collision check (player bullets)
        var fci = Enemy.FrozenEnemies;
        for (int ii = 0; ii < activePlayer.Count; ++ii) {
            sbc = activePlayer[ii];
            if (sbc.Count > 0) sbc.CheckCollision(fci);
        }
        
    }

    private void StartScene() {
        simpleBulletPools[EMPTY].Activate();
        CreateBulletContainer();
    }
    private void CreateBulletContainer() {
        GameObject go = new GameObject {name = "Bullet Spam Container"};
        spamContainer = go.transform;
        spamContainer.position = Vector3.zero;
    }

    public static void OrphanAll() {
        ClearPoolControls();
        foreach (var pool in simpleBulletPools.Values) {
            pool.Reset();
            pool.Deactivate();
        }
        DestroyCopiedPools();
        activeNpc.Clear();
        activePlayer.Clear();
        Bullet.OrphanAll();
        BehaviorEntity.DeInitializePools();
        CurvedTileRenderLaser.DeInitializePools();
    }

    public static void DestroyCopiedPools() {
        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            DestroySimpleStyle(activeCNpc[ii].Style);
        }
        activeCNpc.Clear();
        for (int ii = 0; ii < activeCEmpty.Count; ++ii) {
            DestroySimpleStyle(activeCEmpty[ii].Style);
        }
        activeCEmpty.Clear();
    }

    public static void ClearEmpty() => simpleBulletPools[EMPTY].Reset();

    public static void ClearAllBullets() {
        foreach (string key in simpleBulletPools.Keys) {
            simpleBulletPools[key].Reset();
        }
        ClearNonSimpleBullets();
    }

    public static void ClearNonSimpleBullets() {
        Bullet.ClearAll();
    }
    /// <summary>
    /// While most controls are bounded by ICancellee, some aren't, so they need to be destroyed.
    /// </summary>
    public static void ClearPoolControls(bool clearPlayer=true) {
        foreach (var pool in simpleBulletPools.Values) {
            if (clearPlayer || !pool.IsPlayer) {
                pool.ClearControls();
                pool.ResetPoolMetadata();
            }
        }
        BehaviorEntity.ClearPoolControls(clearPlayer);
        CurvedTileRenderLaser.ClearPoolControls(clearPlayer);
    }
    
    private void OnDestroy() {
        ScriptableObject.Destroy(throwaway_gm);
    }
}
}