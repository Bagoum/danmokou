using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Danmokou.Danmaku;
using Danmokou.DMath;
using NUnit.Framework;
using UnityEngine;

namespace Danmokou.Testing {

public static class THelpers {
    public static T TField<T>(this object? sut, string name) {
        var field = sut?.GetType()
            .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)field?.GetValue(sut)!;
    }
}
public static class TAssert {
    public static void ListEq<T>(IReadOnlyList<T> left, IReadOnlyList<T> right) where T : IEquatable<T> {
        string extraFail = (left.Count == right.Count) ? "" : $"Lengths are mismatched: {left.Count}, {right.Count}. ";
        for (int ii = 0; ii < left.Count && ii < right.Count; ++ii) {
            if (!left[ii].Equals(right[ii])) {
                Assert.Fail($"{extraFail}At index {ii}, left is {left[ii]} and right is {right[ii]}.");
            }
        }
        if (extraFail.Length > 0) Assert.Fail(extraFail);
    }
    public static void ThrowsAny(Action code) {
        try {
            code();
            Assert.Fail("Expected code to fail");
        } catch (Exception) {
            // ignored
        }
    }

    public static void ThrowsMessage(string pattern, Action code) {
        try {
            code();
            Assert.Fail("Expected code to fail");
        } catch (Exception e) {
            RegexMatches(pattern, e.Message);
        }
    }
    public static void RegexMatches(string pattern, string message) {
        if (!new Regex(pattern, RegexOptions.Singleline).Match(message).Success) {
            Assert.Fail($"Could not find pattern `{pattern}` in `{message}`");
        }
    }
    private const float err = 0.0001f;

    public static void VecEq(Vector2 left, Vector2 right, string msg="", float error=err) {
        if (msg == "") msg = $"Comparing vectors {left}, {right}";
        Assert.AreEqual(left.x, right.x, error, msg);
        Assert.AreEqual(left.y, right.y, error, msg);
    }
    public static void VecEq(Vector3 left, Vector3 right, string msg="", float error=err) {
        msg += $": Comparing vectors {left}, {right}";
        Assert.AreEqual(left.x, right.x, error, msg);
        Assert.AreEqual(left.y, right.y, error, msg);
        Assert.AreEqual(left.z, right.z, error, msg);
    }

    public static void ColorEq(Color expect, Color actual, string msg = "", float error = err) {
        Assert.AreEqual(expect.r, actual.r, error, $"Color red of: {msg}");
        Assert.AreEqual(expect.g, actual.g, error, $"Color grn of: {msg}");
        Assert.AreEqual(expect.b, actual.b, error, $"Color blu of: {msg}");
        Assert.AreEqual(expect.a, actual.a, error, $"Color alf of: {msg}");
    }

    public static void BPIEq(ParametricInfo p1, ParametricInfo p2, string msg="") {
        Assert.AreEqual(p1.index, p2.index, $"{msg} BPI Firing Index");
        Assert.AreEqual(p1.t, p2.t, err, $"{msg} BPI Time");
        VecEq(p1.loc, p2.loc, $"{msg} BPI Location");
    }

    public static void SBEq(ref BulletManager.SimpleBullet sb1, ref BulletManager.SimpleBullet sb2, string msg) {
        VecEq(sb1.direction, sb2.direction, $"{msg} Direction");
        Assert.AreEqual(sb1.scale, sb2.scale, err, $"{msg} Scale");
        Assert.AreEqual(sb1.grazeFrameCounter, sb2.grazeFrameCounter, $"{msg} GrazeCtr");
        Assert.AreEqual(sb1.cullFrameCounter, sb2.cullFrameCounter, err, $"{msg} CullCtr");
        BPIEq(sb1.bpi, sb2.bpi, msg);
    }

    public static void SBPos(ref BulletManager.SimpleBullet sb, Vector2 loc, string msg = "") =>
        VecEq(loc, sb.bpi.loc, msg);
    public static void SBPos(ref BulletManager.SimpleBullet sb, V2RV2 loc, string msg = "") =>
        VecEq(loc.TrueLocation, sb.bpi.loc, msg);

    public static void PoolEq(BulletManager.AbsSimpleBulletCollection sbc1,
        BulletManager.AbsSimpleBulletCollection sbc2) {
        if (sbc1.Count != sbc2.Count) Assert.Fail($"Different counts: {sbc1.Count} {sbc2.Count}");
        for (int ii = 0; ii < sbc1.Count; ++ii) {
            ref BulletManager.SimpleBullet sb1 = ref sbc1[ii];
            ref BulletManager.SimpleBullet sb2 = ref sbc2[ii];
            SBEq(ref sb1, ref sb2, $"SB#{ii}");
        }
    }

    public static void TestSMWithExceptionRegex(string sname, string regex) {
        try {
            TestHarness.LoadBehaviorScript(sname);
            Assert.IsFalse(true);
        } catch (Exception e) {
            while (e is TargetInvocationException) {
                e = e.InnerException!;
            }
            TAssert.RegexMatches(regex, e.Message);
        }
    }
}
}