using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using NUnit.Framework;
using Danmokou.SM;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static NUnit.Framework.Assert;
using static Danmokou.DMath.Parser;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class TestParsing {
    [Test]
    public static void TestFloat() {
        AreEqual(4.4f, Float("4.4"));
        AreEqual(4.4f, Float("--4.4"));
        AreEqual(-4.4f, Float("-4.4"));
        AreEqual(-.4f * ETime.ENGINEFPS_F, Float("+-0.4s"));
        AreEqual(-.4f * ETime.FRAME_TIME, Float("-+.4f"));
        AreEqual(-.4f * M.PHI, Float("-+.4p"));
        AreEqual(-.4f * M.IPHI, Float("-+.4h"));
        AreEqual(-2f * M.PI, Float("-2.π"));
        AreEqual(2f * M.PI, Float("2π"));
        AreEqual(false, TryFloat("kemrlge", out _));
        AreEqual(false, TryFloat("<4", out _));
        AreEqual(false, TryFloat("4>", out _));
    }
    [Test]
    public static void TestV2RV2() {
        AreEqual(V2RV2.Zero, ParseV2RV2("<>"));
        AreEqual(V2RV2.Angle(2 * M.IPHI), ParseV2RV2("<2h>"));
        AreEqual(V2RV2.Rot(4.0f, -2.5f, 2 * M.IPHI), ParseV2RV2("<4.0;-2.5:2h>"));
        ThrowsAny(() => ParseV2RV2("<gf;-2.5:2h>"));
        AreEqual(new V2RV2(2f, 3f, 4.0f, -2.5f, 2 * M.IPHI), ParseV2RV2("<2;3:4.0;-2.5:2h>"));
        AreEqual(new V2RV2(2f, 3f, 0, 0, 2 * M.IPHI), ParseV2RV2("<2;3:;:2h>"));
    }

    private static void TestSMExceptionRegex(string sm, string pattern) =>
        ThrowsMessage(pattern, () => StateMachine.CreateFromDump(sm));

    private const string baseScenePath = "Danmokou/Scenes/Testing/TestMainMenu";
    [UnityTest]
    public static IEnumerator TestSMFailures() {
        SceneManager.LoadScene(baseScenePath);
        yield return null;
        TestSMExceptionRegex(@"
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxfff 2", "to type TP.*≪cxfff≫");
        TestSMExceptionRegex(@"
async shell-teal/b <2;:> gcr2 20 _ <;:5> { } gsr2 5 <;:72> { } s :: {
			R	w
		} tp-rot pxy 2 &R", "Instead of constructing type BPY.*of type Float.*to type Float");
        /* No longer an error, as R will be looked up in private data hoisting instead.
        TestSMExceptionRegex(@"
bullet shell-teal/b <2;:> cre 20 _ <;:5> repeat 5 <;:72> s :: {
			R	5
		} tp-rot pxy 2 &R2", "The reference R2 is used, but does not have a value.");*/
        TestSMExceptionRegex(@"sync danger <2;:> summons tprot cx 1 file YEET", "file by name YEET");
        TestSMExceptionRegex(@"sync danger <2;:> summons tprot cx 1 blarg", "blarg is not a StateMachine");
        TestSMExceptionRegex(@"sync danger <2;:> summons tprot cx 1 sad", 
            "Nested StateMachine construction.*sad is not a StateMachine");
        TestSMExceptionRegex(@"async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cx sad", 
            "to type Float.*≪sad≫");
        TestSMExceptionRegex(@"async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2", 
            "TP.*ran out of text");
        TestSMExceptionRegex(@"async shell-teal/b", "ran out of text");
        TestSMExceptionRegex(@"
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2 3",
            "to type Float.*≪async≫");
    }

    [UnityTest]
    public static IEnumerator TestPhaseProperties() {
        SceneManager.LoadScene(baseScenePath);
        yield return null;
        var props = (StateMachine.CreateFromDump(@"
<!> type spell en4
<!> hp 21000
<!> hpbar 1
<!> bgt-in wipetex1
<!> bg black
<!> bgt-out shatter4
phase 0
paction 0
noop") as PhaseSM).TField<PhaseProperties>("props");
        AreEqual(PhaseType.SPELL, props.phaseType);
        AreEqual("en4", props.cardTitle?.ToString());
        AreEqual(21000, props.hp);
        AreEqual(1, props.hpbar);
        AreEqual(ResourceManager.GetBackgroundTransition("wipetex1"), props.BgTransitionIn);
        AreEqual(ResourceManager.GetBackgroundTransition("shatter4"), props.BgTransitionOut);
        AreEqual(ResourceManager.GetBackground("black"), props.Background);
        AreEqual(false, props.HideTimeout);
    }

    [Test]
    public static void TestCountEnforcer() {
        var sm = StateMachine.CreateFromDump(@"
paction 0
    noop
    @ n1
        paction 0 :1
            noop
    noop
    noop") as PhaseParallelActionSM;
        AreEqual(4, sm.TField<List<StateMachine>>("states").Count);
        sm = StateMachine.CreateFromDump(@"
paction 0
    noop
    @ n1
        paction 0 :2
            noop
            noop
    noop") as PhaseParallelActionSM;
        AreEqual(3, sm.TField<List<StateMachine>>("states").Count);
        sm = StateMachine.CreateFromDump(@"
paction 0
    noop
    @ n1
        paction 0
            noop
            noop
            noop") as PhaseParallelActionSM;
        AreEqual(2, sm.TField<List<StateMachine>>("states").Count);

    }

}
}