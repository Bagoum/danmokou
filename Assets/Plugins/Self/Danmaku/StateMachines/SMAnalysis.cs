using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using static Danmaku.Enums;

namespace SM {

public struct SMPhaseController {
    private enum ControllerType {
        /// <summary>
        /// Go to whatever phase is specified.
        /// </summary>
        DEFAULT,
        /// <summary>
        /// Go to the override phase, and then invoke a callback when the SCRIPT is done.
        /// However, if the normal phase specified is 0 (the setup phase) then go there.
        /// </summary>
        EXTERNAL_OVERRIDE_CONTINUE,
        /// <summary>
        /// Go to the override phase, and then invoke a callback when the PHASE is done.
        /// However, if the normal phase specified is 0 (the setup phase) then go there.
        /// </summary>
        EXTERNAL_OVERRIDE,
        /// <summary>
        /// Same as EXTERNAL_OVERRIDE without allowance for 0.
        /// </summary>
        EXTERNAL_OVERRIDE_SKIP,
        /// <summary>
        /// End execution and run a callback.
        /// </summary>
        WAITING_OVERRIDE_RETURN
    }

    private ControllerType typ;
    private int externalOverride;
    private int normalNextPhase;
    [CanBeNull] private Action callback;

    private SMPhaseController(int normalNext) {
        typ = ControllerType.DEFAULT;
        externalOverride = 0;
        normalNextPhase = normalNext;
        callback = null;
    }
    
    public static SMPhaseController Normal(int firstPhase) => new SMPhaseController(firstPhase);

    /// <summary>
    /// Run a single phase and then hit the callback.
    /// By default, the zero phase (setup phase by convention) is run first, and then it goes to the target phase.
    /// </summary>
    /// <param name="gotoPhase">Target phase</param>
    /// <param name="cb">Callback</param>
    /// <param name="forceZeroOverride">True iff the zero phase should also be skipped</param>
    public void Override(int gotoPhase, [CanBeNull] Action cb = null, bool forceZeroOverride = false) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = forceZeroOverride ? ControllerType.EXTERNAL_OVERRIDE_SKIP : ControllerType.EXTERNAL_OVERRIDE;
    }

    /// <summary>
    /// Run a single phase, then continue the script, and hit a callback when the script is done normally.
    /// </summary>
    public void SetGoTo(int gotoPhase, [CanBeNull] Action cb) {
        externalOverride = gotoPhase;
        callback = cb;
        typ = ControllerType.EXTERNAL_OVERRIDE_CONTINUE;
    }
    /// <summary>
    /// Set a callback that can be run on script end, or on phase end if using override.
    /// </summary>
    public void SetCallback([CanBeNull] Action cb) => callback = cb;

    /// <summary>
    /// </summary>
    /// <returns>-1</returns>
    public int RunEndingCallback() {
        callback?.Invoke();
        callback = null;
        return -1;
    }

    /// <summary>
    /// Set an override, but only if one is not already set.
    /// </summary>
    public void LowPriorityOverride(int gotoPhase, bool forceZeroOverride = false) {
        if (typ == ControllerType.DEFAULT) Override(gotoPhase, callback, forceZeroOverride);
    }

    public void SetDesiredNext(int nxt) => normalNextPhase = nxt;
    
    /// <summary>
    /// </summary>
    /// <param name="requestedNormal">The phase desired by the SM</param>
    /// <returns>The phase the SM should go to. This number may be negative or greater than the phase length,
    /// in which case the SM should stop executing.</returns>
    public int WhatIsNextPhase(int? requestedNormal = null) {
        normalNextPhase = requestedNormal ?? normalNextPhase;
        if (typ == ControllerType.EXTERNAL_OVERRIDE_SKIP ||
            (typ == ControllerType.EXTERNAL_OVERRIDE && normalNextPhase > 0)) {
            typ = ControllerType.WAITING_OVERRIDE_RETURN;
            return externalOverride;
        } else if (typ == ControllerType.EXTERNAL_OVERRIDE_CONTINUE && normalNextPhase > 0) {
            typ = ControllerType.DEFAULT;
            return externalOverride;
        } else if (typ == ControllerType.WAITING_OVERRIDE_RETURN) {
            typ = ControllerType.DEFAULT;
            if (callback != null) return RunEndingCallback();
        }
        return normalNextPhase;
    }

}
public static class SMAnalysis {
    public readonly struct Phase {
        public readonly PhaseType type;
        [CanBeNull] private readonly string title;
        public readonly int index;
        public string Title {
            get {
                if (type == PhaseType.STAGE) return $"Stage Section {index}";
                if (type == PhaseType.STAGEMIDBOSS) return "Midboss";
                if (type == PhaseType.STAGEENDBOSS) return "Endboss";
                if (type == PhaseType.DIALOGUE) return title ?? "Dialogue";
                return title ?? "!!!UNTITLED PHASE (REPORT ME)!!!";
            }
        }

        public Phase(PhaseType c, int phaseNum, [CanBeNull] string name) {
            type = c;
            title = name;
            index = phaseNum;
        }
    }

    public static List<Phase> Analyze(PatternSM pat, PhaseType? deflt = null, bool ignoreZero = true) {
        var ret = new List<Phase>();
        foreach (var (i, phase) in pat.phases.Enumerate()) {
            if (ignoreZero && i == 0) continue;
            var assumedCardType = phase.props.phaseType ?? deflt;
            if (assumedCardType.HasValue) {
                ret.Add(new Phase(assumedCardType.Value, i, phase.props.cardTitle));
            }
        }
        return ret;
    }
    
    
}
}