using System;
using System.Reactive;
using Danmokou.Core;

namespace Danmokou.Achievements {

/// <summary>
/// Always returns COMPLETED.
/// </summary>
public class CompletedFixedReq : Requirement {
    public override State EvalState() => State.Completed;
}

/// <summary>
/// Always returns IN PROGRESS.
/// </summary>
public class InProgressFixedReq : Requirement {
    public override State EvalState() => State.InProgress;
}

/// <summary>
/// Always returns LOCKED.
/// </summary>
public class LockedFixedReq : Requirement {
    public override State EvalState() => State.Locked;
}

/// <summary>
/// If the nested requirement is completed, returns COMPLETED. Else returns LOCKED.
/// </summary>
public class LockedUntilAchievedReq : Requirement {
    private readonly Requirement req;
    public LockedUntilAchievedReq(Requirement req) {
        this.req = req.SetWatcher(this);
    }

    public override State EvalState() => 
        (req.EvalState() == State.Completed) ? State.Completed : State.Locked;
}

/// <summary>
/// Returns the state of the nested achievement.
/// </summary>
public class AchievementRequirement : Requirement {
    private readonly Achievement acv;
    public AchievementRequirement(Achievement acv) {
        this.acv = acv;
        acv.AttachCallback(_ => RequirementUpdated());
    }

    public AchievementRequirement(string acvKey, AchievementManager repo) : this(repo.FindByKey(acvKey)) { }

    public override State EvalState() => acv.State;
}

public class ListeningRequirement : Requirement {
    private readonly Func<State> eval;

    public ListeningRequirement(Func<State> eval, params IObservable<Unit>[] evs) {
        this.eval = eval;
        foreach (var ev in evs)
            Listen(ev);
    }

    public ListeningRequirement(Func<bool> eval, params IObservable<Unit>[] evs) : 
        this(() => eval().ToACVState(), evs) { }

    public override State EvalState() => eval();
}

}