using System;
using System.Collections.Generic;
using Core;
using DMath;
using NUnit.Framework;
using SM;
using static NUnit.Framework.Assert;
using static DMath.Parser;
using static Tests.TAssert;
using static Core.Events;
using static Danmaku.Enums;

namespace Tests {

public static class TestSMAnalysis {
    [Test]
    public static void TestSMPhaseControl() {
        var sp = SMPhaseController.Normal(0);
        AreEqual(sp.WhatIsNextPhase(), 0);
        AreEqual(sp.WhatIsNextPhase(1), 1);
        bool ch = false;
        Action cb = () => ch = true;
        sp = SMPhaseController.Normal(0);
        sp.Override(4);
        AreEqual(sp.WhatIsNextPhase(), 0);
        AreEqual(sp.WhatIsNextPhase(0), 0);
        AreEqual(sp.WhatIsNextPhase(2), 4);
        AreEqual(sp.WhatIsNextPhase(3), 3);
        sp = SMPhaseController.Normal(0);
        sp.Override(4, cb);
        AreEqual(sp.WhatIsNextPhase(), 0);
        AreEqual(sp.WhatIsNextPhase(0), 0);
        AreEqual(sp.WhatIsNextPhase(2), 4);
        IsFalse(ch);
        AreEqual(sp.WhatIsNextPhase(3), -1);
        IsTrue(ch);
        ch = false;
        sp = SMPhaseController.Normal(0);
        sp.Override(4, cb, true);
        AreEqual(sp.WhatIsNextPhase(), 4);
        IsFalse(ch);
        AreEqual(sp.WhatIsNextPhase(3), -1);
        IsTrue(ch);
        
        sp = SMPhaseController.Normal(0);
        AreEqual(sp.WhatIsNextPhase(0), 0);
        sp.LowPriorityOverride(3);
        AreEqual(sp.WhatIsNextPhase(1), 3);
        AreEqual(sp.WhatIsNextPhase(2), 2);
        
        sp = SMPhaseController.Normal(0);
        ch = false;
        sp.Override(4, cb, false);
        AreEqual(sp.WhatIsNextPhase(0), 0);
        sp.LowPriorityOverride(3);
        AreEqual(sp.WhatIsNextPhase(1), 4);
        IsFalse(ch);
        AreEqual(sp.WhatIsNextPhase(2), -1);
        IsTrue(ch);
    }

    [Test]
    public static void TestAnalyzer() {
        var sm = StateMachine.CreateFromDump(@"
pattern { }
phase 0
<!> dialogue
phase 0
<!> type non `My First Non`
phase 0
phase 0
<!> type spell `My First Spell`
phase 0
") as PatternSM;
        AreEqual(new List<SMAnalysis.Phase>() {
                //phase 0,3 are ignored since they don't have type
                new SMAnalysis.Phase(PhaseType.DIALOGUE, 1, null),
                new SMAnalysis.Phase(PhaseType.NONSPELL, 2, "My First Non"),
                new SMAnalysis.Phase(PhaseType.SPELL, 4, "My First Spell"),
            }, SMAnalysis.Analyze(sm));
    }
    

}
}