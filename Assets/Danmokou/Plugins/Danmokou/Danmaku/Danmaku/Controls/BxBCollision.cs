using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.SM;
using UnityEngine;
using UnityEngine.Profiling;
using static Danmokou.Danmaku.BulletManager;
using SBC = Danmokou.Danmaku.BulletManager.SimpleBulletCollection;

namespace Danmokou.Danmaku {
/// <summary>
/// Abstract base class for tracking collision between bullets.
/// <br/>When calling <see cref="BxBCollideLASM"/>, an object of this type is created, and
///  it checks for collisions between its designated bullet styles every frame.
/// </summary>
public abstract class BxBCollision : IRegularUpdater, IDisposable {
    protected readonly ICancellee cT;
    protected readonly IDeletionMarker lifetimeToken;

    public int UpdatePriority => UpdatePriorities.SLOW;

    public BxBCollision(ICancellee cT) {
        this.cT = cT;
        lifetimeToken = ETime.RegisterRegularUpdater(this);
    }

    public void RegularUpdate() {
        if (cT.Cancelled)
            Destroy();
    }
    public abstract void RegularUpdateCollision();

    public void Dispose() => Destroy();
    public virtual void Destroy() {
        lifetimeToken.MarkForDeletion();
    }
}

/// <summary>
/// Collision tracker for collisions between simple bullets and simple bullets.
/// </summary>
public class BxBCollisionSBOnSB : BxBCollision {
    private readonly List<(SBC sender, List<(SBC pool, bool isLeft)> receivers)> pairs;
    private readonly cBulletControl[] leftControls;
    private readonly cBulletControl[] rightControls;
    private readonly Pred leftPred;
    private readonly Pred rightPred;
    public BxBCollisionSBOnSB(ICancellee cT, IEnumerable<SBC> left, List<SBC> right,
        Pred leftPred, Pred rightPred, cBulletControl[] leftControls, cBulletControl[] rightControls) : base(cT) {
        this.leftPred = leftPred;
        this.rightPred = rightPred;
        this.leftControls = leftControls;
        this.rightControls = rightControls;
        pairs = new List<(SBC, List<(SBC, bool)>)>();
        var senders = new Dictionary<SBC, List<(SBC, bool)>>();
        foreach (var l in left) {
            if (l is NoCollSBC)
                continue;
            for (int ir = 0; ir < right.Count; ++ir) {
                var r = right[ir];
                if (r is NoCollSBC)
                    continue;
                //the one with the greater eccentricity becomes the sender (true collider) and the lesser becomes the receiver (circle approximation)
                var (send, recv, rIsLeft) = l.CircleCollider.Irregularity < r.CircleCollider.Irregularity ? (r, l, true) : (l, r, false);
                if (!senders.ContainsKey(send)) {
                    //only the sender is bucketed
                    send.RequestBucketing(lifetimeToken);
                    senders[send] = new();
                    pairs.Add((send, null!));
                }
                senders[send].Add((recv, rIsLeft));
            }
        }
        for (int ip = 0; ip < pairs.Count; ++ip)
            pairs[ip] = (pairs[ip].sender, senders[pairs[ip].sender]);
    }
    
    public override void RegularUpdateCollision() {
        Profiler.BeginSample("BxB SBxSB collision");
        for (int ip = 0; ip < pairs.Count; ++ip) {
            var (send, recvs) = pairs[ip];
            var fmt = send.GetCollisionFormat();
            var sendBuckets = send.bucketsSpan;
            //If sender pool is set to no-collisions, continue (TODO: this is not symmetrical)
            if (!fmt.Try(out var _bucketing)) continue;
            if (!_bucketing.Valid)
                throw new Exception($"Bullet-on-bullet collision: pool {send.Style} was not bucketed");
            var bucketing = _bucketing.Value;
            var mcdLeft = bucketing.maxScale * send.Collider.MaxRadius;
            Profiler.BeginSample("Pair handling");
            for (int ir = 0; ir < recvs.Count; ++ir) {
                var (recv, recvIsLeft) = recvs[ir];
                var (recvCtrls, sendCtrls) = recvIsLeft ? (leftControls, rightControls) : (rightControls, leftControls);
                var (recvPred, sendPred) = recvIsLeft ? (leftPred, rightPred) : (rightPred, leftPred);
                var recvCircleRad = recv.CircleCollider.Radius;
                //TODO this would be more efficient if you bucketed both sides, then iterated over buckets instead of bullets on the receiver side, using minLoc = bucket.minPos - mcdLeft - Recv.MaxRadius * Recv.frame.maxScale, etc.
                for (int iRecv = 0; iRecv < recv.Count; ++iRecv) {
                    ref var sbRecv = ref recv[iRecv];
                    if (recv.Deleted[iRecv] || !recvPred(sbRecv.bpi)) continue;
                    var mcd = mcdLeft + sbRecv.scale * recvCircleRad;
                    var minBucket = send.BucketIndexPair(new Vector2(sbRecv.bpi.loc.x - mcd, sbRecv.bpi.loc.y - mcd));
                    var maxBucket = send.BucketIndexPair(new Vector2(sbRecv.bpi.loc.x + mcd, sbRecv.bpi.loc.y + mcd));
                    Profiler.BeginSample("Sendbucket iteration");
                    for (int iy = minBucket.y; iy <= maxBucket.y; ++iy)
                    for (int ix = minBucket.x; ix <= maxBucket.x; ++ix) {
                        var bucketSend = sendBuckets[iy, ix];
                        //Profiler.BeginSample("Bucket looping");
                        for (int ibSend = 0; ibSend < bucketSend.Count; ++ibSend) {
                            var iSend = bucketSend[ibSend];
                            ref var sbSend = ref send[iSend];
                            if (send.Deleted[iSend] || !sendPred(sbSend.bpi)) continue;
                            if (send.Collider.CheckCollision(in sbSend.bpi.loc.x, in sbSend.bpi.loc.y,
                                    in sbSend.direction, in sbSend.scale, in sbRecv.bpi.loc.x, in sbRecv.bpi.loc.y,
                                    recvCircleRad * sbRecv.scale)) {
                                Profiler.BeginSample("Control execution");
                                //Execute sender controls
                                var stSend = new SimpleBulletCollection.VelocityUpdateState(send, 0, 0) { ii = iSend };
                                for (int pi = 0; pi < sendCtrls.Length && !send.Deleted[iSend]; ++pi)
                                    sendCtrls[pi].func(in stSend, in sbSend.bpi, in cT);
                                
                                //Execute receiver controls
                                var stRecv = new SimpleBulletCollection.VelocityUpdateState(recv, 0, 0) { ii = iRecv };
                                for (int pi = 0; pi < recvCtrls.Length; ++pi) {
                                    recvCtrls[pi].func(in stRecv, in sbRecv.bpi, in cT);
                                    if (recv.Deleted[iRecv]) {
                                        Profiler.EndSample();
                                        //Profiler.EndSample();
                                        goto next_recv;
                                    }
                                }
                                Profiler.EndSample();
                            }
                        }
                        //Profiler.EndSample();
                    }
                    next_recv: ;
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();
        }
        Profiler.EndSample();
    }


    public override void Destroy() {
        for (int ii = 0; ii < pairs.Count; ++ii)
            pairs[ii].receivers.Clear();
        pairs.Clear();
        base.Destroy();
    }
}

}