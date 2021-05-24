using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using BagoumLib.Cancellation;
using Danmokou.Behavior;
using Danmokou.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Danmokou.Testing {
public class TestCoroutines {
    private static IEnumerator NestLoC(ICancellee cT) {
        if (cT.Cancelled) yield break;
        yield return LeaveOnCancel(cT);
    }
    private static IEnumerator LeaveOnCancel(ICancellee cT) {
        while (true) {
            if (cT.Cancelled) yield break;
            yield return null;
        }
    }
    [UnityTest]
    public static IEnumerator TestAutoCancellation() {
        CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
        var cts = new Cancellable();
        cru.RunRIEnumerator(NestLoC(cts));
        cts.Cancel();
        cru.gameObject.SetActive(false);
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        cru.gameObject.SetActive(true);
        cru.RunDroppableRIEnumerator(NestLoC(Cancellable.Null));
        cru.gameObject.SetActive(false);
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        cru.gameObject.SetActive(true);
        cru.RunRIEnumerator(NestLoC(Cancellable.Null));
        LogAssert.Expect(LogType.Error, new Regex(".*1 leftover coroutine.*"));
        cru.gameObject.SetActive(false);
        Assert.AreEqual(cru.NumRunningCoroutines, 1);
        yield return null;
    }

    private static IEnumerator SelfDestroy(CoroutineRegularUpdater cru) {
        yield return null;
        cru.gameObject.SetActive(false);
        yield return null;
    }
    private static IEnumerator SelfDestroy2(CoroutineRegularUpdater cru) {
        yield return null;
        cru.gameObject.SetActive(false);
        // ReSharper disable once RedundantJumpStatement
        yield break;
    }

    [UnityTest]
    public static IEnumerator TestSelfDestroy() {
        CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
        cru.RunRIEnumerator(SelfDestroy(cru));
        cru.RegularUpdate();
        Assert.AreEqual(cru.NumRunningCoroutines, 1);
        cru.RegularUpdate();
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        yield return null;
    }
    [UnityTest]
    public static IEnumerator TestSelfDestroy2() {
        CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
        cru.RunRIEnumerator(SelfDestroy2(cru));
        cru.RegularUpdate();
        Assert.AreEqual(cru.NumRunningCoroutines, 1);
        cru.RegularUpdate();
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        yield return null;
    }
}
}