using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Runtime.CompilerServices;
using BagoumLib.Culture;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.DMath;

namespace Danmokou.Achievements {

public enum State {
    Locked = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>
/// An entity that can receive a callback when an achievement requirement is updated.
/// <br/>Run SetWatcher on the requirement to receive callbacks.
/// </summary>
public interface IRequirementWatcher {
    /// <summary>
    /// Called when the requirement has been updated.
    /// </summary>
    void RequirementUpdated();
}


public abstract class Requirement : IRequirementWatcher {
    private IRequirementWatcher? watcher = null;
    public abstract State EvalState();
    
    //Whenever a change is triggered anywhere in the entire achievement tree,
    // the tree from the top-down is re-checked once. 
    public void RequirementUpdated() => watcher?.RequirementUpdated();

    protected void Listen<T, E>(EventProxy<T> obj, Func<T, IBObservable<E>> ev) => 
        obj.Subscribe(ev, _ => RequirementUpdated());
    protected void Listen(IObservable<Unit> ev) => ev.Subscribe(_ => RequirementUpdated());
    protected void Listen<T>(IBObservable<T> ev) => ev.Subscribe(_ => RequirementUpdated());

    public Requirement SetWatcher(IRequirementWatcher w) {
        watcher = w;
        return this;
    }

    public Requirement SelfLock() => new LockedUntilAchievedReq(this);
}

public class Achievement : IRequirementWatcher {
    public static DisturbedAnd ACHIEVEMENT_PROGRESS_ENABLED { get; } = new DisturbedAnd();
    public string Key { get; }
    public LString Title { get; }
    public LString Description { get; }
    public LString VisibleDescription =>
        State == State.Locked ? LocalizedStrings.UI.achievements_locked : Description;
    public Requirement Req { get; }
    public State State { get; private set; }
    public bool Completed => State == State.Completed;

    /// <summary>
    /// When the achievement is obtained, it will not be displayed on screen for this many seconds.
    /// This is useful for cases where the scene changes at the same time as an achievment is obtained
    /// (eg. "Play the practice mode" is achieved right before the scene transitions from main menu to practice scene,
    ///  so we add a 1 second delay via the Delay function that makes it render after the scene transition).
    /// </summary>
    public float DelayTime { get; private set; } = 0;

    public Achievement Delay(float by = 1f) {
        DelayTime = by;
        return this;
    }

    public Achievement(string key, LString title, LString descr, Func<Requirement> req, AchievementRepo? repo=null) {
        Key = key;
        Title = title;
        Description = descr;
        var qstate = repo?.SavedAchievementState(Key);
        Req = (qstate == State.Completed ? new CompletedFixedReq() : req()).SetWatcher(this);
        State = qstate ?? State.Locked;
    }

    public void RequirementUpdated() {
        if (!ACHIEVEMENT_PROGRESS_ENABLED) return;
        //Don't bother checking if the achievement is already finished
        if (State == State.Completed) return;
        var nState = Req.EvalState();
        if (nState <= State)
            return;
        Logs.Log($"Achievement {Key} progressed from {State} to {nState}");
        State = nState;
        SendCallbacks();
        AchievementStateUpdated.OnNext(this);
    }

    public static readonly Event<Achievement> AchievementStateUpdated = new Event<Achievement>();

    private readonly List<Action<State>> cbs = new List<Action<State>>();
    private void SendCallbacks() {
        for (int ii = 0; ii < cbs.Count; ++ii) {
            cbs[ii](State);
        }
    }

    public void AttachCallback(Action<State> cb) {
        cbs.Add(cb);
    }

    public Requirement Lock(Requirement r) {
        if (State == State.Completed) return r;
        return new LockedReq(new AchievementRequirement(this), r);
    }

}

public static class AchievementHelpers {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static State ToACVState(this bool b) => b ? State.Completed : State.InProgress;
}

}