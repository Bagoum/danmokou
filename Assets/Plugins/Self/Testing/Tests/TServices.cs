using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using DMath;
using Danmaku;
using static NUnit.Framework.Assert;

namespace Tests {

public static class TServices {
    [Test]
    public static void TestHoistClear() {
        var v = new Velocity("nroffset ss0 px rand -200 200".Into<VTP>());
        var bpi = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi, out var d11, 0.1f);
        bpi = new ParametricInfo(Vector2.zero, 0, 2443455);
        v.UpdateDeltaAssignAcc(ref bpi, out var d2, 0.1f);
        bpi = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi, out var d12, 0.1f);
        //Due to ss0 this is the same
        AreEqual(d11, d12);
        AreNotEqual(d11, d2);
        DataHoisting.ClearValues();
        bpi = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi, out var d31, 0.1f);
        bpi = new ParametricInfo(Vector2.zero, 0, 2443455);
        v.UpdateDeltaAssignAcc(ref bpi, out var d4, 0.1f);
        bpi = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi, out var d32, 0.1f);
        AreEqual(d31, d32);
        AreNotEqual(d31, d4);
        //Due to hoist clear, ss0 must resample
        AreNotEqual(d11, d31);
        AreNotEqual(d2, d4);
        
    }

}
}