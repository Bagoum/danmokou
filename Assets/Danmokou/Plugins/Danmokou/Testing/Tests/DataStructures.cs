using System;
using System.Collections;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.Core;
using Danmokou.DMath;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
// ReSharper disable UnusedVariable

namespace Danmokou.Testing {
public class DataStructures {
    private readonly struct I {
        public readonly int x;

        public I(int _x) {
            x = _x;
            
        }
    }

    [Test]
    public void MultiOppers() {
        var p = MultiOp.Priority.CLEAR_PHASE;
        var ml = new List<float>();
        var m = new MultiMultiplier(100, x => ml.Add(x));
        var t1 = m.CreateModifier(2, p);
        var t2 = m.CreateModifier(3, p);
        Assert.AreEqual(m.Value, 600);
        TAssert.ListEq(ml, new[] { 200f, 600f });
        t2.TryRevoke();
        Assert.AreEqual(m.Value, 200);
        TAssert.ListEq(ml, new[] { 200f, 600f, 200f });
        var t3 = m.CreateModifier(5, p);
        Assert.AreEqual(m.Value, 1000);
        TAssert.ListEq(ml, new[] { 200f, 600f, 200f, 1000f });
        m.RevokeAll(MultiOp.Priority.ALL);
        Assert.AreEqual(m.Value, 100);
        
        var al = new List<float>();
        var a = new MultiAdder(100, x => al.Add(x));
        var t4 = a.CreateModifier(2, p);
        var t5 = a.CreateModifier(3, p);
        Assert.AreEqual(a.Value, 105);
        TAssert.ListEq(al, new[] { 102f, 105f });
        t4.TryRevoke();
        Assert.AreEqual(a.Value, 103);
        TAssert.ListEq(al, new[] { 102f, 105f, 103f });
        var t6 = a.CreateModifier(5, p);
        Assert.AreEqual(a.Value, 108);
        TAssert.ListEq(al, new[] { 102f, 105f, 103f, 108f });
        a.RevokeAll(MultiOp.Priority.ALL);
        Assert.AreEqual(a.Value, 100);
    }

    [Test]
    public void BitField() {
        var b = BitCompression.FromBools(true, false, false, false, true, false, false, true);
        Assert.IsTrue(b.NthBool(0) && b.NthBool(4) && b.NthBool(7));
        Assert.IsFalse(b.NthBool(1) || b.NthBool(2) || b.NthBool(3) || b.NthBool(5) || b.NthBool(6));
    }

    [Test]
    public void N2Triangle() {
        var arr = new[] {
            new[] {0},
            new[] {10, 11, 12},
            new[] {20, 21, 22, 23, 24}
        };
        var n2 = new N2Triangle<int>(arr);
        Assert.AreEqual(n2[0][0], 0);
        Assert.Throws<IndexOutOfRangeException>(() => {
            var x = n2[0][1];
        });
        Assert.AreEqual(n2[1][0], 11);
        Assert.AreEqual(n2[1][-1], 10);
        Assert.AreEqual(n2[1][1], 12);
        Assert.Throws<IndexOutOfRangeException>(() => {
            var x = n2[1][-2];
        });
        Assert.AreEqual(n2[2][0], 22);
        Assert.AreEqual(n2[2][-2], 20);
        Assert.AreEqual(n2[2][1], 23);
        Assert.Throws<IndexOutOfRangeException>(() => {
            var x = n2[2][3];
        });
        arr = new[] {
            new[] {0},
            new[] {10, 11, 12, 13},
            new[] {20, 21, 22, 23, 24}
        };
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<ArgumentException>(() => new N2Triangle<int>(arr));
    }

    
    
}
}