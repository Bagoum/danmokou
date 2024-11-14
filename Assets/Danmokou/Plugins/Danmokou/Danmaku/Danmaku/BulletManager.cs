using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Expressions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Danmaku.Descriptors;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.Graphics;
using Danmokou.Player;
using Danmokou.Reflection;
using UnityEngine.Profiling;

namespace Danmokou.Danmaku {
public partial class BulletManager {
    public const string EMPTY = "empty";

    public static SimpleBulletCollection CopyPool(string newPool, string from, bool isPlayer) {
        var src = simpleBulletPools.GetValueOrDefault(from) ??
                  throw new Exception($"Simple bullet style {from} does not exist; cannot make a copy of it");
        var p = src.MetaType == SimpleBulletCollection.CollectionType.Empty ?
            new EmptySBC(src.CopyBC(newPool)) :
            src.CopyPool(isPlayer ? activePlayer : activeNpc, newPool);
        p.SetOriginal(src);
        if (isPlayer)
            p.SetPlayer();
        AddSimpleStyle(p);
        p.Activate();
        return p;
    }

    public static BehaviorEntity.StyleMetadata CopyComplexPool(string newPool, string from, bool isPlayer) {
        var src = behPools.GetValueOrDefault(from) ??
            throw new Exception($"Complex bullet style {from} does not exist, cannot make a copy of it");
        var p = src.MakeCopy(newPool, isPlayer);
        AddComplexStyle(p);
        p.Activate();
        return p;
    }

    private static readonly Dictionary<string, string> playerPoolCopyCache = new();

    private const string PLAYERPREFIX = "p-";

    public static string GetOrMakePlayerCopy(string basePool) {
        CheckOrCopyPool(basePool, out _);
        //lmao i hate garbage
        if (!playerPoolCopyCache.TryGetValue(basePool, out var playerPool))
            playerPool = playerPoolCopyCache[basePool] = $"{PLAYERPREFIX}{basePool}";
        if (!simpleBulletPools.ContainsKey(playerPool))
            CopyPool(playerPool, basePool, true);
        return playerPool;
    }

    public static string GetOrMakeComplexPlayerCopy(string basePool) {
        CheckOrCopyComplexPool(basePool, out _);
        if (!playerPoolCopyCache.TryGetValue(basePool, out var playerPool))
            playerPool = playerPoolCopyCache[basePool] = $"{PLAYERPREFIX}{basePool}";
        if (!behPools.ContainsKey(playerPool)) 
            CopyComplexPool(playerPool, basePool, true);
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
            sbc.Activate();
            return true;
        } else if (IsPlayerPoolString(pool))
            return CheckOrCopyPool(GetOrMakePlayerCopy(pool[PLAYERPREFIX.Length..]), out sbc);
        int splitAt = pool.IndexOf('.');
        if (splitAt == -1) return false;
        string basePool = pool[..splitAt];
        if (!simpleBulletPools.ContainsKey(basePool)) return false;
        sbc = CopyPool(pool, basePool, false);
        return true;
    }

    public static bool CheckOrCopyComplexPool(string pool, out BehaviorEntity.StyleMetadata bsm) {
        if (behPools.TryGetValue(pool, out bsm)) {
            bsm.Activate();
            return true;
        } else if (IsPlayerPoolString(pool))
            return CheckOrCopyComplexPool(GetOrMakeComplexPlayerCopy(pool[PLAYERPREFIX.Length..]), out bsm);
        int splitAt = pool.IndexOf('.');
        if (splitAt == -1) return false;
        string basePool = pool[..splitAt];
        if (!behPools.ContainsKey(basePool)) return false;
        bsm = CopyComplexPool(pool, basePool, false);
        return true;
    }

    public static void AssertControls(string pool, IReadOnlyList<BulletControl> controls) =>
        GetMaybeCopyPool(pool).AssertControls(controls);

    public static SimpleBulletCollection GetMaybeCopyPool(string pool) {
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }

    public static SimpleBulletCollection? NullableGetMaybeCopyPool(string? pool) {
        if (pool == null || string.IsNullOrWhiteSpace(pool) || pool == "_")
            return null;
        if (CheckOrCopyPool(pool, out var sbc)) return sbc;
        throw new Exception($"Could not find simple bullet style by name \"{pool}\".");
    }

    private static readonly ExFunction getMaybeCopyPool =
        ExFunction.Wrap<string>(typeof(BulletManager), nameof(BulletManager.GetMaybeCopyPool));
    private static readonly ExFunction nullableGetMaybeCopyPool =
        ExFunction.Wrap<string?>(typeof(BulletManager), nameof(BulletManager.NullableGetMaybeCopyPool));

    public override int UpdatePriority => UpdatePriorities.BM;

    public static bool NPCBulletsRequireBucketing { get; private set; } = false;

    public override void RegularUpdate() {
        ResetSentry();
        //Temp-last set for control updates
        foreach (var sbc in activeEmpty)
            sbc.temp_last = sbc.Count;
        foreach (var sbc in activeNpc) 
            sbc.temp_last = sbc.Count;
        foreach (var sbc in activePlayer) 
            sbc.temp_last = sbc.Count;
        foreach (var sbc in activeCulled) 
            sbc.temp_last = sbc.Count;
        NPCBulletsRequireBucketing = ServiceLocator.FindAll<IEnemySimpleBulletCollisionReceiver>().NumberAlive() > 1;
        //Velocity and control updates
        for (int ii = 0; ii < activeEmpty.Count; ++ii)
            activeEmpty[ii].UpdateVelocityAndControls();
        Profiler.BeginSample("NPC-fired simple bullet velocity updates");
        for (int ii = 0; ii < activeNpc.Count; ++ii)
            activeNpc[ii].UpdateVelocityAndControls(NPCBulletsRequireBucketing);
        Profiler.EndSample();
        Profiler.BeginSample("Player simple bullet velocity updates");
        for (int ii = 0; ii < activePlayer.Count; ++ii)
            activePlayer[ii].UpdateVelocityAndControls();
        Profiler.EndSample();
        for (int ii = 0; ii < activeCulled.Count; ++ii)
            activeCulled[ii].UpdateVelocityAndControls();
    }

    public override void RegularUpdateCollision() {
        var collidees = ServiceLocator.FindAll<IEnemySimpleBulletCollisionReceiver>();
        Profiler.BeginSample("NPC simple bullet collisions");
        for (int ii = 0; ii < activeNpc.Count; ++ii) {
            var sbc = activeNpc[ii];
            if (sbc.Count > 0)
                for (int ic = 0; ic < collidees.Count; ++ic)
                    if (collidees.GetIfExistsAt(ic, out var receiver))
                        sbc.CheckCollisions(receiver);
        }
        Profiler.EndSample();

        Profiler.BeginSample("Player simple bullet collisions");
        var enemies = ServiceLocator.FindAll<IPlayerSimpleBulletCollisionReceiver>();
        for (int ii = 0; ii < activePlayer.Count; ++ii) {
            var sbc = activePlayer[ii];
            if (sbc.Count > 0)
                for (int ie = 0; ie < enemies.Count; ++ie)
                    if (enemies.GetIfExistsAt(ie, out var receiver))
                        sbc.CheckCollisions(receiver);
        }
        Profiler.EndSample();
    }

    public override void RegularUpdateFinalize() {
        //Do this in the late step so it occurs after any custom collision handling, and before rendering
        foreach (var c in collections) {
            foreach (var grp in c) {
                grp.CompactAndSort();
            }
        }
    }

    private void StartScene() {
        simpleBulletPools[EMPTY].Activate();
        GameObject go = new() { name = "Bullet Spam Container" };
        spamContainer = go.transform;
        spamContainer.position = Vector3.zero;
    }

    /// <summary>
    /// Clear pool controls, reset all simple bullet pools, and destroy copied pools.
    /// </summary>
    public static void OrphanAll() {
        //clear pool controls
        foreach (var pool in simpleBulletPools.Values)
            pool.ClearControls();
        BehaviorEntity.ClearPoolControls();
        CurvedTileRenderLaser.ClearPoolControls();
        
        //reset all pools
        foreach (var pool in simpleBulletPools.Values) {
            pool.Reset();
            pool._Deactivate();
        }
        BehaviorEntity.DeInitializePools(); //clears activeBEH list
        CurvedTileRenderLaser.DestroyPools(); //laser pools are just wrappers around BEHStyle, so we destroy them
        
        //destroy copied pools and clear activeSBC lists
        foreach (var activeList in new[]{activeEmpty, activeNpc, activePlayer}) {
            foreach (var sbc in activeList) { 
                if (sbc.IsCopy) {
                    sbc.Destroy(); //also deletes culled pool 
                    simpleBulletPools.Remove(sbc.Style);
                }
            }
            activeList.Clear();
        }
        activeCulled.Clear();
        
        foreach (var (key, style) in behPools.ToArray()) {
            if (style.IsCopy) {
                style.Destroy();
                behPools.Remove(key);
            }
        }
    }

    public static void ClearEmptyBullets(bool clearPlayer) {
        foreach (var sbc in activeEmpty)
            if (clearPlayer || !sbc.IsPlayer)
                sbc.Reset();
    }

    public static void ClearAllBullets() {
        foreach (var pool in simpleBulletPools.Values)
            pool.Reset();
        Bullet.ClearAll();
    }

    private void OnDestroy() {
        ScriptableObject.Destroy(throwaway_gm);
        ScriptableObject.Destroy(throwaway_mpm);
    }
}
}