using System.Collections.Generic;
using NUnit.Framework;
using DMK.Core;

namespace DMK.Testing {
public static class DMath {

    [Test]
    public static void Utils() {
        Assert.AreEqual(new[] {1,2,3,4}, new[]{1,2}.Extend(new[]{3,4}));
        var lis = new List<int>() { 2 };
        var ol = new List<int>() { 1 };
        ol.AssignOrExtend(ref lis);
        Assert.AreEqual(new List<int>() { 2, 1 }, lis);
        lis = null;
        ol.AssignOrExtend(ref lis);
        Assert.AreEqual(ol, lis);
    }

}
}