using System;
using DMK.Core;

namespace DMK.Achievements {
//Note: you cannot aggregate multiple event requirements, as the event requirement is only 
// in a completed state while its update function is being called.
// You can aggregate event requirements with static requirements.
//While normal requirements operate over static data and are rechecked on events, event requirements
// operate over the data returned from events.
//It is possible to write many normal requirements, such as TutorialDoneReq, as event requirements,
// but that should be avoided.

//Please don't use this class-- it probably means you've design something incorrectly
public class EventRequirement : Requirement {
    private bool eventTriggered = false;

    public EventRequirement(Events.Event0 ev, Func<bool> predicate) {
        ev.Subscribe(() => {
            eventTriggered = predicate();
            if (eventTriggered)
                RequirementUpdated();
            eventTriggered = false;
        });
    }
    public override State EvalState() => eventTriggered.ToACVState();
}

public class EventRequirement<T> : Requirement {
    private bool eventTriggered = false;
    public EventRequirement(Events.IEvent<T> ev, Func<T, bool> predicate) {
        ev.Subscribe(val => {
            eventTriggered = predicate(val);
            if (eventTriggered)
                RequirementUpdated();
            eventTriggered = false;
        });
    }
    public override State EvalState() => eventTriggered.ToACVState();
}
}