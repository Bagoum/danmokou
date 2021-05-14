using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Danmokou.Scriptables;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Danmokou.Testing {

public static class Scriptables {

    [UnityTest]
    public static IEnumerator TestRefs() {
        RFloat rf = new RFloat();
        rf.refVal = ScriptableObject.CreateInstance<SOFloat>();
        rf.constVal = 4;
        rf.useConstant = true;
        Assert.IsFalse(rf.Set(7));
        float f = rf;
        Assert.AreEqual(f, 4);
        rf.useConstant = false;
        Assert.IsTrue(rf.Set(7));
        f = rf.GetRef();
        Assert.AreEqual(f, 7);
        Assert.AreEqual(rf.GetRef().Get(), 7);
        yield return null;
    }
}
}