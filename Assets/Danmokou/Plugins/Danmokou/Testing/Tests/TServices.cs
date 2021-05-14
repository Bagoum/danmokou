using UnityEngine;
using NUnit.Framework;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Reflection;
using static NUnit.Framework.Assert;

namespace Danmokou.Testing {

public static class TServices {
    [Test]
    public static void TestHoistClear() {
        var v = new Movement("nroffset ss0 px rand -200 200".Into<VTP>());
        var bpi11 = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi11, out var d11, 0.1f);
        var bpi2 = new ParametricInfo(Vector2.zero, 0, 2443455);
        v.UpdateDeltaAssignAcc(ref bpi2, out var d2, 0.1f);
        var bpi12 = new ParametricInfo(Vector2.zero, 0, 24, ctx: bpi11.ctx);
        v.UpdateDeltaAssignAcc(ref bpi12, out var d12, 0.1f);
        //Due to ss0 this is the same
        AreEqual(d11, d12);
        AreNotEqual(d11, d2);
        PublicDataHoisting.ClearValues();
        var bpi31 = new ParametricInfo(Vector2.zero, 0, 24);
        v.UpdateDeltaAssignAcc(ref bpi31, out var d31, 0.1f);
        var bpi4 = new ParametricInfo(Vector2.zero, 0, 2443455);
        v.UpdateDeltaAssignAcc(ref bpi4, out var d4, 0.1f);
        var bpi32 = new ParametricInfo(Vector2.zero, 0, 24, ctx: bpi31.ctx);
        v.UpdateDeltaAssignAcc(ref bpi32, out var d32, 0.1f);
        AreEqual(d31, d32);
        AreNotEqual(d31, d4);
        //Due to hoist clear, ss0 must resample
        AreNotEqual(d11, d31);
        AreNotEqual(d2, d4);
        
    }

}
}