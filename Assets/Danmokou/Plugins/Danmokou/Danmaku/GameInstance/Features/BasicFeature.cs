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
    int Continues { get;  }
    int ContinuesUsed { get; }
    int HitsTaken { get; }
    int LastTookHitFrame { get; }

    bool TryContinue();
    
    /// <summary>
    /// Delta should be negative.
    /// Note: powerbombs do not call this routine.
    /// </summary>
    bool TryConsumeBombs(int delta);

    /// <summary>
    ///  Add a delta, positive or negative, to the number of lives the player has.
    /// </summary>
    /// <param name="delta">The change in lives (negative if losing a life).</param>
    /// <param name="asHit">True if this was the result of taking damage.</param>
    /// <returns>True iff the player has </returns>
    void AddLives(int delta, bool asHit);

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
    
    private readonly int startLives;
    private readonly int startBombs;

    public BasicFeature(InstanceData inst, BasicFeatureCreator c) {
        Inst = inst;
        Lives = new(this.startLives = inst.Difficulty.startingLives ?? c.StartLives ?? (inst.mode.OneLife() ? 1 : 7));
        Bombs = new(this.startBombs = c.StartBombs ?? (inst.mode.OneLife() ? 0 : 3));
        Continues = c.Continues ?? (inst.mode.OneLife() ? 0 : 42);
    }

    public bool TryContinue() {
        if (Continues > 0) {
            //We can allow continues in replays! But in the current impl, the watcher will have to press continue.
            //Replayer.Cancel();
            --Continues;
            ++ContinuesUsed;
            Inst.CardHistory.Clear();//Partial game is saved when lives=0. Don't double on captures.
            Lives.Value = startLives;
            Bombs.Value = startBombs;
            foreach (var f in Inst.Features)
                f.OnContinue();
            return true;
        } else return false;
    }
    
    public bool TryConsumeBombs(int delta) {
        if (Bombs + delta >= 0) {
            Bombs.Value += delta;
            return true;
        }
        return false;
    }
    
    public void AddLives(int delta, bool asHit) {
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
        Lives.Value = Math.Max(0, Lives + delta);
    }

    public void LifeExtend(ExtendType method) {
        ++Lives.Value;
        Inst.ExtendAcquired.OnNext(method);
    }
}

public record BasicFeatureCreator : IFeatureCreator<IBasicFeature> {
    public int? Continues { get; init; }
    public int? StartLives { get; init; }
    public int? StartBombs { get; init; }
    public IBasicFeature Create(InstanceData instance) => new BasicFeature(instance, this);
}
}