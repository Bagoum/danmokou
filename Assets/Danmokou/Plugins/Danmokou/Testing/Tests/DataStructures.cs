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