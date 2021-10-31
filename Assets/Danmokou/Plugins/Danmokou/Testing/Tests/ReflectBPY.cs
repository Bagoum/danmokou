using System;
using System.Linq;
using System.Linq.Expressions;
using BagoumLib.Expressions;
using NUnit.Framework;
using Danmokou.DMath;
using Danmokou.DMath.Functions;
using Danmokou.Expressions;
using Danmokou.Reflection;
using UnityEngine;
using static Danmokou.Reflection.Compilers;
using static Danmokou.DMath.Functions.BPYRepo;
using static NUnit.Framework.Assert;
using static Danmokou.DMath.Functions.ExMLerps;

namespace Danmokou.Testing {

    public static class ReflectBPY {
        private const float err = 0.00001f;

        [Test]
        public static void Smooth1() {
            BPY lerp = "lerp 4 5 t 0 2".Into<BPY>();
            var pi = new ParametricInfo(Vector2.right, 234, 3243, 0f);
            AreEqual(lerp(pi.CopyWithT(3f)), 0f, err);
            AreEqual(lerp(pi.CopyWithT(4.4f)), 0.8f, err);
            AreEqual(lerp(pi.CopyWithT(6f)), 2f, err);
            lerp = "lerp 4 5 t t 2".Into<BPY>();
            AreEqual(lerp(pi.CopyWithT(3)), 3f, err);
            AreEqual(lerp(pi.CopyWithT(4)), 4f, err);
            AreEqual(lerp(pi.CopyWithT(4.5f)), 0.5f * (4.5f + 2f), err);
            AreEqual(lerp(pi.CopyWithT(5f)), 2f, err);
        }

        private static void TestTPoints(BPY b, (float t, float val)[] pts) {
            var pi = new ParametricInfo(Vector2.right, 234, 3243, 1f);
            foreach (var (t, val) in pts) {
                AreEqual(b(pi.CopyWithT(t)), val, err);
            }
        }
        
        [Test]
        public static void E01() {
            void TestE01(BPY e01, (float t, float val)[] otherPts) {
                TestTPoints(e01, otherPts);
                TestTPoints(e01, new[] { (0f, 0f), (0.5f, 0.5f), (1f, 1f)});
            }
            BPY s = BPY(x => ExMEasers.EIOSine(x.t));
            TestE01(s, new []{ (0.1f, 0.02447f) });
            s = BPY(x => SmoothLoop(ExMEasers.EIOSine, x.t));
            TestE01(s, new []{ (1.1f, 1.02447f) });
            TestE01(s, new []{ (3.1f, 3.02447f) });
        }

        [Test]
        public static void TSwitchH() {
            BPY sh = BPY(SwitchH<float>(T(), _ => 2f, bpi => bpi.t, bpi => bpi.t.Mul(2f)));
            var pi = new ParametricInfo();
            AreEqual(sh(pi.CopyWithT(1.9f)), 1.9f, err);
            AreEqual(sh(pi.CopyWithT(2.01f)), 0.02f, err);
            AreEqual(sh(pi.CopyWithT(2.2f)), 0.4f, err);
        }
        
        private static float logsum(float sharpness, float[] vals) {
            return (float) Math.Log(vals.Select(x => Math.Exp(x * sharpness)).Sum()) / sharpness;
        }
        [Test]
        public static void TLogsum() {
            BPY fsoftmax1 = "logsum 1 { (+ t 2) 8 }".Into<BPY>();
            var pi = new ParametricInfo();
            Assert.AreEqual(logsum(1f, new[] {5f, 8f}), fsoftmax1(pi.CopyWithT(3f)), err);
            Assert.AreEqual(logsum(1f, new[] {9f, 8f}), fsoftmax1(pi.CopyWithT(7f)), err);
        }
        private static Expression ExC(object x) => Expression.Constant(x);
        private static float shiftlogsum(float sharpness, float pivot, BPY eq1, BPY eq2, ParametricInfo x) {
            return logsum(sharpness, new[] {eq1(x), eq1(x.CopyWithT(pivot)) + eq2(x) - eq2(x.CopyWithT(pivot)) });
        }
            
        [Test]
        public static void TShiftLogsum() {
            BPY eq1 = x => 5 * x.t;
            BPY eq2 = x => 2 * x.t;
            BPY shifter = BPY(LogsumShiftT(_=>-1f, _=>10f, bpi => bpi.t.Mul(5f), bpi => bpi.t.Mul(2f)));
            var pi = new ParametricInfo();
            Assert.AreEqual(shiftlogsum(-1f, 10f, eq1, eq2, pi.CopyWithT(10.2f)), shifter(pi.CopyWithT(10.2f)), err);
            Assert.AreEqual(shiftlogsum(-1f, 10f, eq1, eq2, pi.CopyWithT(20.2f)), shifter(pi.CopyWithT(20.2f)), err);
            eq1 = x => 200 * x.t;
            eq2 = x => 4 * x.t;
            var exs = LogsumShiftT(_=>-0.1f, _=>1f, bpi => bpi.t.Mul(200f), bpi => bpi.t.Mul(4f));
            shifter = BPY(exs);
            for (float ii = 0; ii < 2; ii += 0.05f) {
                AreEqual(shiftlogsum(-0.1f, 1f, eq1, eq2, pi.CopyWithT(ii)), shifter(pi.CopyWithT(ii)), 0.0001f, $"shifter at {ii}");
            }
            
        }

        [Test]
        public static void PredLinks() {
            BPY p10 = "* t pred10(> t 5)".Into<BPY>();
            TestTPoints(p10, new []{ (4f, 0f), (6, 6) });
        }
        
    }
}