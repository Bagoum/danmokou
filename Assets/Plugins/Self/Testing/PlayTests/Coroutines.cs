using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests {
public class Coroutines {
    private static IEnumerator NestLoC(CancellationToken cT) {
        if (cT.IsCancellationRequested) yield break;
        yield return LeaveOnCancel(cT);
    }
    private static IEnumerator LeaveOnCancel(CancellationToken cT) {
        while (true) {
            if (cT.IsCancellationRequested) yield break;
            yield return null;
        }
    }
    [UnityTest]
    public static IEnumerator TestAutoCancellation() {
        CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
        var cts = new CancellationTokenSource();
        cru.RunRIEnumerator(NestLoC(cts.Token));
        cts.Cancel();
        cru.gameObject.SetActive(false);
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        cru.gameObject.SetActive(true);
        cru.RunDroppableRIEnumerator(NestLoC(CancellationToken.None));
        cru.gameObject.SetActive(false);
        Assert.AreEqual(cru.NumRunningCoroutines, 0);
        cru.gameObject.SetActive(true);
        cru.RunRIEnumerator(NestLoC(CancellationToken.None));
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