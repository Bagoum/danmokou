using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Reflection;
using Danmokou.Services;
using NUnit.Framework;
using Danmokou.SM;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class TestParsing {
    private static void TestSMExceptionRegex(string sm, string pattern) =>
        ThrowsRegex(pattern, () => StateMachine.CreateFromDump(sm));

    private const string baseScenePath = "Danmokou/Scenes/Testing/TestMainMenu";
    [UnityTest]
    public static IEnumerator TestSMFailures() {
        SceneManager.LoadScene(baseScenePath);
        yield return null;
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxfff 2", "to type Func<TExArgCtx, TEx<Vector2>>.*≪cxfff≫");
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b <2;:> gcr2 20 _ <;:5> { } gsr2 5 <;:72> { } s :: {
			R	w
		} tp-rot pxy 2 &R", "Failed to construct an object of type Func<TExArgCtx, TEx<float>>.*of type float.*to type float");
        /* No longer an error, as R will be looked up in private data hoisting instead.
        TestSMExceptionRegex(@"
bullet shell-teal/b <2;:> cre 20 _ <;:5> repeat 5 <;:72> s :: {
			R	5
		} tp-rot pxy 2 &R2", "The reference R2 is used, but does not have a value.");*/
        TestSMExceptionRegex(@"<#> bdsl1
sync danger <2;:> summons tprot cx 1 file YEET", "file by name YEET");
        TestSMExceptionRegex(@"<#> bdsl1
sync danger <2;:> summons tprot cx 1 blarg", "blarg is not a StateMachine");
        TestSMExceptionRegex(@"<#> bdsl1
sync danger <2;:> summons tprot cx 1 sad", 
            "sad is not a StateMachine");
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cx sad", 
            "to type float.*≪sad≫");
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2", 
            "TP.*ran out of text");
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b", "ran out of text");
        TestSMExceptionRegex(@"<#> bdsl1
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2
async shell-teal/b <2;:> gcr2 60 5 <-0.2;:10> { } gsr2 5 <;:72> { } s tp-rot cxy 2 3",
            "to type float.*≪async≫");
    }

    [UnityTest]
    public static IEnumerator TestPhaseProperties() {
        SceneManager.LoadScene(baseScenePath);
        yield return null;
        var props = (StateMachine.CreateFromDump(@"<#> bdsl1
<!> type spell en4
<!> hp 21000
<!> hpbar 1
<!> bgt-in wipetex1
<!> bg black
<!> bgt-out shatter4
phase 0
paction 0
noop") as EFStateMachine)!.inner.TField<PhaseProperties>("props");
        AreEqual(PhaseType.Spell, props.phaseType);
        AreEqual("en4", props.cardTitle?.ToString());
        AreEqual(21000, props.hp);
        AreEqual(1, props.hpbar);
        AreEqual(ResourceManager.GetBackgroundTransition("wipetex1"), props.BgTransitionIn);
        AreEqual(ResourceManager.GetBackgroundTransition("shatter4"), props.BgTransitionOut);
        AreEqual(ResourceManager.GetBackground("black"), props.Background);
        AreEqual(false, props.HideTimeout);
    }

    [UnityTest]
    public static IEnumerator TestCountEnforcer() {
        SceneManager.LoadScene(baseScenePath);
        yield return null;
        var sm = StateMachine.CreateFromDump(@"<#> bdsl1
paction 0
    noop
    @ n1
        paction 0 :1
            noop
    noop
    noop") as EFStateMachine;
        AreEqual(4, sm!.inner.TField<StateMachine[]>("states").Length);
        sm = StateMachine.CreateFromDump(@"<#> bdsl1
paction 0
    noop
    @ n1
        paction 0 :2
            noop
            noop
    noop") as EFStateMachine;
        AreEqual(3, sm!.inner.TField<StateMachine[]>("states").Length);
        sm = StateMachine.CreateFromDump(@"<#> bdsl1
paction 0
    noop
    @ n1
        paction 0
            noop
            noop
            noop") as EFStateMachine;
        AreEqual(2, sm!.inner.TField<StateMachine[]>("states").Length);

    }

}
}