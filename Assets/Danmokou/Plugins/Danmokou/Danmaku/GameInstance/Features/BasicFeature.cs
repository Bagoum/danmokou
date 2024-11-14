using System;
using System.Reactive;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.Danmaku;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling basic aspects of a danmaku game, such as life and bombs.
/// </summary>
public interface IBasicFeature : IInstanceFeature {
    Evented<int> Lives { get; }
    Evented<int> Bombs { get; }
    Event<Unit> PlayerTookHit { get; }
    int Continues { get; }
    int ContinuesUsed { get; }
    int HitsTaken { get; }
    int LastTookHitFrame { get; }
    int StartLives { get; }

    bool ContinuesAllowed => Continues + ContinuesUsed > 0;
    bool ContinuesRemaining => Continues > 0;

    bool TryContinue();
    
    #if UNITY_EDITOR
    bool ForceContinue();
    #endif
    
    /// <summary>
    /// Delta should be negative.
    /// Note: powerbombs do not call this routine.
    /// </summary>
    bool TryConsumeBombs(int delta);

    /// <summary>
    /// Add a delta, positive or negative, to the number of lives the player has. If this results in
    ///  zero lives, also handle firing a GameOver event.
    /// </summary>
    /// <param name="delta">The change in lives (negative if losing a life).</param>
    /// <param name="asHit">True if this was the result of taking damage.</param>
    void AddLives(int delta, bool asHit = true);

    void LifeExtend(ExtendType method);
}

public class BasicFeature : BaseInstanceFeature, IBasicFeature {
    private InstanceData Inst { get; }
    public Evented<int> Lives { get; }
    public Evented<int> Bombs { get; }
    public Event<Unit> PlayerTookHit { get; } = new();
    public int Continues { get; private set; }
    public int ContinuesUsed { get; private set; } = 0;
    public int HitsTaken { get; private set; }
    public int LastTookHitFrame { get; private set; }
    
    public int StartLives { get; }
    private readonly int startBombs;

    public BasicFeature(InstanceData inst, BasicFeatureCreator c) {
        Inst = inst;
        Lives = new(this.StartLives = inst.Difficulty.startingLives ?? c.StartLives ?? (inst.mode.OneLife() ? 1 : 7));
        Bombs = new(this.startBombs = c.StartBombs ?? (inst.mode.OneLife() ? 0 : 3));
        Continues = c.Continues ?? (inst.mode.OneLife() ? 0 : 42);
    }
    

    public void OnContinueOrCheckpoint() {
        Inst.CardHistory.Clear();//Partial game is saved when lives=0. Don't double on captures.
        Lives.Value = StartLives;
        Bombs.Value = startBombs;
    }

    public bool TryContinue() {
        if (Continues > 0) {
            DoContinue();
            return true;
        } else return false;
    }
    
    #if UNITY_EDITOR
    public bool ForceContinue() {
        DoContinue();
        return true;
    }
    #endif

    private void DoContinue() {
        //We can allow continues in replays! But in the current impl, the watcher will have to press continue.
        //Replayer.Cancel();
        --Continues;
        ++ContinuesUsed;
        foreach (var f in Inst.Features)
            f.OnContinueOrCheckpoint();
    }
    
    public void AddLives(int delta, bool asHit = true) {
        //if (mode == CampaignMode.NULL) return;
        Logs.Log($"Adding player lives: {delta}");
        if (delta < 0 && asHit) {
            ++HitsTaken;
            LastTookHitFrame = ETime.FrameNumber;
            Bombs.Value = Math.Max(Bombs, startBombs);
            foreach (var f in Inst.Features)
                f.OnDied();
            PlayerTookHit.OnNext(default);
        }
        if ((Lives.Value = Math.Max(0, Lives + delta)) == 0) {
            //Record failure
            if (Inst.Request?.Saveable == true) {
                //Special-case boss practice handling
                if (Inst.Request.lowerRequest is BossPracticeRequest bpr) {
                    Inst.CardHistory.Add(new CardRecord() {
                        campaign = bpr.boss.campaign.Key,
                        boss = bpr.boss.boss.key,
                        phase = bpr.phase.index,
                        stars = 0,
                        hits = 1,
                        method = null
                    });
                }
                SaveData.r.RecordGame(new InstanceRecord(Inst.Request, Inst, false));
            }
            Inst.GameOver.OnNext(default);
        }
    }

    public void LifeExtend(ExtendType method) {
        ++Lives.Value;
        Inst.ExtendAcquired.OnNext(method);
    }
    
    public bool TryConsumeBombs(int delta) {
        if (Bombs + delta >= 0) {
            Bombs.Value += delta;
            return true;
        }
        return false;
    }
}

public record BasicFeatureCreator : IFeatureCreator<IBasicFeature> {
    public int? Continues { get; init; }
    public int? StartLives { get; init; }
    public int? StartBombs { get; init; }
    public IBasicFeature Create(InstanceData instance) => new BasicFeature(instance, this);
}
}