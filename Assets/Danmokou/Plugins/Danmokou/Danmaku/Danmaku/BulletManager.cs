using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using BagoumLib.Cancellation;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Reflection;
using UnityEngine.Profiling;

namespace Danmokou.Danmaku {


public partial class BulletManager {
    public const string EMPTY = "empty";

    public static void CopyPool(string newPool, string from) {
        var src = simpleBulletPools[from];
        var p = src.MetaType == AbsSimpleBulletCollection.CollectionType.Empty ? 
            new EmptySBC(src.CopyBC(newPool)) : 
            src.CopyPool(activeCNpc, newPool);
        p.SetOriginal(src);
        AddSimpleStyle(p);
        p.Activate();
    }

    private static readonly Dictionary<string, string> playerPoolCopyCache = new Dictionary<string, string>();
    public static string GetOrMakePlayerCopy(string pool) {
        CheckOrCopyPool(pool, out _);
        //lmao i hate garbage
        if (!playerPoolCopyCache.TryGetValue(pool, out var playerPool)) {
            playerPool = playerPoolCopyCache[pool] = $"{PLAYERPREFIX}{pool}";
        }
        if (!simpleBulletPools.TryGetValue(playerPool, out var p)) {
            if (!simpleBulletPools.TryGetValue(pool, out var src)) 
                throw new Exception($"{pool} does not exist, cannot make a player variant of it");
            p = src.MetaType == AbsSimpleBulletCollection.CollectionType.Empty ? 
                new EmptySBC(src.CopyBC(playerPool)) : 
                src.CopySimplePool(activePlayer, playerPool);
            p.SetOriginal(src);
            p.SetPlayer();
            AddSimpleStyle(p);
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

    public static void AssertControls(string pool, IReadOnlyList<BulletControl> controls) => GetMaybeCopyPool(pool).AssertControls(controls);

    public static AbsSimpleBulletCollection GetMaybeCopyPool(string pool) {
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }
    public static AbsSimpleBulletCollection? NullableGetMaybeCopyPool(string? pool) {
        if (pool == null || string.IsNullOrWhiteSpace(pool) || pool == "_") 
            return null;
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }
    private static readonly ExFunction getMaybeCopyPool = 
        ExFunction.Wrap<string>(typeof(BulletManager), "GetMaybeCopyPool");
    private static readonly ExFunction nullableGetMaybeCopyPool = 
        ExFunction.Wrap<string?>(typeof(BulletManager), "NullableGetMaybeCopyPool");

    public override int UpdatePriority => UpdatePriorities.BM;
    public override void RegularUpdate() {
#if UNITY_EDITOR
        if (ETime.FirstUpdateForScreen && Input.GetKeyDown(KeyCode.RightBracket))
            DebugBulletNums();
#endif
        ResetSentry();
        SimpleBulletCollection sbc;
        //Temp-last set for control updates
        for (int ii = 0; ii< activeEmpty.Count; ++ii) {
            sbc = activeEmpty[ii];
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
        for (int ii = 0; ii < activeCulled.Count; ++ii) {
            sbc = activeCulled[ii];
            sbc.temp_last = sbc.Count;
        }
        //Velocity and control updates
        for (int ii = 0; ii < activeEmpty.Count; ++ii) {
            sbc = activeEmpty[ii];
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
        for (int ii = 0; ii < activeCulled.Count; ++ii) {
            sbc = activeCulled[ii];
            if (sbc.temp_last > 0) {
                sbc.UpdateVelocityAndControls();
            } else sbc.PruneControls();
        }
        
        
        if (bulletCollisionTarget.Active) {
            var hitbox = bulletCollisionTarget.Hitbox;
            Profiler.BeginSample("NPC-fired simple bullet collision checking");
            int dmg = 0; int graze = 0;
            for (int ii = 0; ii < activeEmpty.Count; ++ii) {
                //Empty bullets never collide
                activeEmpty[ii].NullCollisionCleanup();
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
            for (int ii = 0; ii < activeEmpty.Count; ++ii) {
                activeEmpty[ii].NullCollisionCleanup();
            }
            for (int ii = 0; ii < activeNpc.Count; ++ii) {
                activeNpc[ii].NullCollisionCleanup();
            }
            for (int ii = 0; ii < activeCNpc.Count; ++ii) {
                activeCNpc[ii].NullCollisionCleanup();
            }
        }
        for (int ii = 0; ii < activeCulled.Count; ++ii) {
            activeCulled[ii].NullCollisionCleanup();
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
        activeCulled.Clear();
        Bullet.OrphanAll();
        BehaviorEntity.DeInitializePools();
        CurvedTileRenderLaser.DeInitializePools();
    }

    public static void DestroyCopiedPools() {
        //Some empty pools are copied
        var newEmpty = new List<SimpleBulletCollection>();
        for (int ii = 0; ii < activeEmpty.Count; ++ii) {
            if (activeEmpty[ii].IsCopy) {
                DestroySimpleStyle(activeEmpty[ii].Style);
            } else {
                newEmpty.Add(activeEmpty[ii]);
            }
        }
        activeEmpty.Clear();
        activeEmpty.AddRange(newEmpty);

        for (int ii = 0; ii < activeCNpc.Count; ++ii) {
            DestroySimpleStyle(activeCNpc[ii].Style);
        }
        activeCNpc.Clear();
        //All player pools are copied
        for (int ii = 0; ii < activePlayer.Count; ++ii) {
            DestroySimpleStyle(activePlayer[ii].Style);
        }
        activePlayer.Clear();
        //Don't delete culled pools since they are linked from the base pools
    }

    public static void ClearEmptyBullets(bool clearPlayer) {
        for (int ii = 0; ii < activeEmpty.Count; ++ii) {
            if (clearPlayer || !activeEmpty[ii].IsPlayer)
                activeEmpty[ii].Reset();
        }
    }

    public static void ClearAllBullets() {
        foreach (var pool in simpleBulletPools.Values) {
            pool.Reset();
        }
        ClearNonSimpleBullets();
    }

    public static void ClearNonSimpleBullets() {
        Bullet.ClearAll();
    }
    /// <summary>
    /// Only call this for hard endings (like scene clear). Phase tokens should handle phase deletion.
    /// </summary>
    public static void ClearPoolControls() {
        foreach (var pool in simpleBulletPools.Values)
            pool.ClearControls();
        BehaviorEntity.ClearPoolControls();
        CurvedTileRenderLaser.ClearPoolControls();
    }
    
    private void OnDestroy() {
        ScriptableObject.Destroy(throwaway_gm);
        ScriptableObject.Destroy(throwaway_mpm);
    }
}
}