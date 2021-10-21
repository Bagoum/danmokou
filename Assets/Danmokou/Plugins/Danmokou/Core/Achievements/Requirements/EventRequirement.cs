using System;
using BagoumLib.Events;
using Danmokou.Core;

namespace Danmokou.Achievements {
//Note: you cannot aggregate multiple event requirements, as the event requirement is only 
// in a completed state while its update function is being called.
// You can aggregate event requirements with static requirements.
//While normal requirements operate over static data and are rechecked on events, event requirements
// operate over the data returned from events.
//It is possible to write many normal requirements, such as TutorialDoneReq, as event requirements,
// but that should be avoided.

//Be careful when using this class-- the only valid use case is for an event that marks when something occurs,
// and the requirement is that "that thing has occurred".

public class EventRequirement<T> : Requirement {
    private bool eventTriggered = false;
    public EventRequirement(IObservable<T> ev, Func<T, bool> predicate) {
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