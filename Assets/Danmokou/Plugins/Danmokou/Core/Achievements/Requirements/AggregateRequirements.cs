using System;
using System.Linq;

namespace Danmokou.Achievements {

public abstract class AggregateReq : Requirement {
    public AggregateReq(params Requirement[] reqs) {
        for (int ii = 0; ii < reqs.Length; ++ii)
            reqs[ii].SetWatcher(this);
    }
    
}

/// <summary>
/// Returns the minimum of all the component requirements.
/// </summary>
public class AndReq : AggregateReq {
    private readonly Requirement[] reqs;
    public AndReq(params Requirement[] reqs) : base(reqs) {
        this.reqs = reqs;
    }

    public override State EvalState() {
        var s = State.Completed;
        for (int ii = 0; ii < reqs.Length; ++ii) {
            var si = reqs[ii].EvalState();
            if (si < s)
                s = si;
        }
        return s;
    }
}

/// <summary>
/// Returns the maximum of all the component requirements.
/// </summary>
public class OrReq : AggregateReq {
    private readonly Requirement[] reqs;
    public OrReq(params Requirement[] reqs) : base(reqs) {
        this.reqs = reqs;
    }

    public override State EvalState() {
        var s = State.Locked;
        for (int ii = 0; ii < reqs.Length; ++ii) {
            var si = reqs[ii].EvalState();
            if (si > s)
                s = si;
        }
        return s;
    }
}

/// <summary>
/// If the locker requirement is incomplete, then return LOCKED. If it is complete, then return the status
/// of the content requirement.
/// <br/>Use this to hide content achievements with sensitive information (locked achievements do not display).
/// <br/>Eg. Locked = "Beat the Ex Stage", Content = "Listen to the entirety of the Devil's Recitation".
/// </summary>
public class LockedReq : AggregateReq {
    private readonly Requirement locker;
    private readonly Requirement content;
    public LockedReq(Requirement locker, Requirement content) : base(locker, content) {
        this.locker = locker.SetWatcher(this);
        this.content = content.SetWatcher(this);
    }
    
    public override State EvalState() => locker.EvalState() != State.Completed ? State.Locked : content.EvalState();
}


}