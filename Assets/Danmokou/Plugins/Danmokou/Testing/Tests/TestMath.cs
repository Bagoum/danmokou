using UnityEngine;
using NUnit.Framework;
using Danmokou.DMath;
using static Danmokou.DMath.ColorHelpers;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class TestMath {
        [Test]
        public static void TestAngles() {
            float or21 = 1f / 2f;
            float or22 = Mathf.Sqrt(2f) / 2f;
            float or23 = Mathf.Sqrt(3f) / 2f;
            float eps = .000001f;
            Assert.AreEqual(M.SinDeg(210), -or21, eps);
            Assert.AreEqual(M.CosDeg(30), or23, eps);
            Vector2 v2 = M.PolarToXY(120);
            Assert.AreEqual(v2.x, -or21, eps);
            Assert.AreEqual(v2.y, or23, eps);
            Vector2 rv2 = M.RotateVectorDeg(v2, 60);
            Assert.AreEqual(rv2.x, -1, eps);
            Assert.AreEqual(rv2.y, 0, eps);
            rv2 = M.RotateVector(v2, or21, or23);
            Assert.AreEqual(rv2.x, -1, eps);
            Assert.AreEqual(rv2.y, 0, eps);
            rv2 = M.RotateVector(v2, M.HPI);
            Assert.AreEqual(rv2.y, -or21, eps);
            Assert.AreEqual(rv2.x, -or23, eps);
            
            Vector2 myVec = new Vector2(3f, 4f);
            Vector2 unitv = new Vector2(or22, or22);

            Vector2 proj = M.ProjectionUnit(myVec, unitv);
            Assert.AreEqual(proj.x, 3.5f, eps);
            Assert.AreEqual(proj.y, 3.5f, eps);

        }

        [Test]
        public static void TestSigns() {
            Assert.AreEqual(M.Sign(5f), 1);
            Assert.AreEqual(M.Sign(-2f), -1);
            Assert.AreEqual(M.Sign(0f), 0);
            Assert.AreEqual(M.Clamp(0, 5, 6), 5);
            Assert.AreEqual(M.Clamp(0, 5, -2), 0);
            Assert.AreEqual(M.Clamp(0, 5, 3), 3);
        }

        [Test]
        public static void TestComplex() {
            float sx = 0;
            float sy = 0;
            float p1x = 2f;
            float p1y = 3f;
            float p2x = -4f;
            float p2y = 8f;
            Assert.IsTrue(M.IsCounterClockwise(sx, sy, p1x, p1y, p2x, p2y));
            Assert.IsFalse(M.IsCounterClockwise(p1x, p1y, sx, sy, p2x, p2y));
        }

        /// <summary>
        /// Fast-path tests for Gradient/DGradient
        /// </summary>
        [Test]
        public static void GradientHelpers() {
            IGradient g = EvenlySpaced(Color.red, Color.blue);
            ColorEq(g.Evaluate(0f), Color.red);
            ColorEq(g.Evaluate(1f), Color.blue);
            var gr = g.Reverse();
            ColorEq(gr.Evaluate(0f), Color.blue);
            ColorEq(gr.Evaluate(1f), Color.red);
            var gsr = gr.RemapTime(0.2f, 0.7f);
            ColorEq(gsr.Evaluate(0), Color.Lerp(Color.blue, Color.red, 0.2f));
            ColorEq(gsr.Evaluate(1), Color.Lerp(Color.blue, Color.red, 0.7f));
            ColorEq(gsr.Evaluate(0.5f), Color.Lerp(Color.blue, Color.red, 0.45f));
            Assert.IsTrue(gsr is DGradient);
            var red0 = new Color(1f, 0, 0, 0);
            g = EvenlySpaced(red0, Color.blue);
            //Alpha in color DOES matter as fallback
            ColorEq(g.Evaluate(0f), Color.red.WithA(0f));
            ColorEq(g.Evaluate(1f), Color.blue);
            g = FromKeys(new[] {new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f)},
                new[] {new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f)});
            ColorEq(g.Evaluate(0f), red0);
            ColorEq(g.Evaluate(1f), Color.blue);
            g = g.RemapTime(0.2f, 0.7f);
            ColorEq(g.Evaluate(0), Color.Lerp(red0, Color.blue, 0.2f));
            ColorEq(g.Evaluate(1), Color.Lerp(red0, Color.blue, 0.7f));
            ColorEq(g.Evaluate(0.5f), Color.Lerp(red0, Color.blue, 0.45f));
            g = g.RemapTime(0f, 2f);
            ColorEq(g.Evaluate(0), Color.Lerp(red0, Color.blue, 0.2f));
            ColorEq(g.Evaluate(1), Color.Lerp(red0, Color.blue, 0.7f));
            ColorEq(g.Evaluate(0.5f), Color.Lerp(red0, Color.blue, 0.7f));
            Assert.IsTrue(g is DGradient);
        }
        /// <summary>
        /// Slow-path tests for generic IGradient
        /// </summary>
        [Test]
        public static void GradientHelpers2() {
            IGradient g = new WrapGradient(EvenlySpaced(Color.red, Color.blue));
            ColorEq(g.Evaluate(0f), Color.red);
            ColorEq(g.Evaluate(1f), Color.blue);
            var gr = g.Reverse();
            ColorEq(gr.Evaluate(0f), Color.blue);
            ColorEq(gr.Evaluate(1f), Color.red);
            var gsr = gr.RemapTime(0.2f, 0.7f);
            ColorEq(gsr.Evaluate(0), Color.Lerp(Color.blue, Color.red, 0.2f));
            ColorEq(gsr.Evaluate(1), Color.Lerp(Color.blue, Color.red, 0.7f));
            ColorEq(gsr.Evaluate(0.5f), Color.Lerp(Color.blue, Color.red, 0.45f));
            Assert.IsFalse(gsr is DGradient);
            var red0 = new Color(1f, 0, 0, 0);
            g = new WrapGradient(EvenlySpaced(red0, Color.blue));
            //Alpha in color DOES matter as fallback
            ColorEq(g.Evaluate(0f), Color.red.WithA(0f));
            ColorEq(g.Evaluate(1f), Color.blue);
            g = FromKeys(new[] {new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.blue, 1f)},
                new[] {new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 1f)});
            ColorEq(g.Evaluate(0f), red0);
            ColorEq(g.Evaluate(1f), Color.blue);
            g = g.RemapTime(0.2f, 0.7f);
            ColorEq(g.Evaluate(0), Color.Lerp(red0, Color.blue, 0.2f));
            ColorEq(g.Evaluate(1), Color.Lerp(red0, Color.blue, 0.7f));
            ColorEq(g.Evaluate(0.5f), Color.Lerp(red0, Color.blue, 0.45f));
            g = g.RemapTime(0f, 2f);
            ColorEq(g.Evaluate(0), Color.Lerp(red0, Color.blue, 0.2f));
            ColorEq(g.Evaluate(1), Color.Lerp(red0, Color.blue, 0.7f));
            ColorEq(g.Evaluate(0.5f), Color.Lerp(red0, Color.blue, 0.7f));
            Assert.IsFalse(g is Gradient);
        }

        [Test]
        public static void MoreGradientHelpers() {
            var b = new NamedGradient(EvenlySpaced(Color.blue, Color.white), "b");
            var r = new NamedGradient(EvenlySpaced(Color.black, Color.red), "r");
            var br = NamedGradient.Mix(b, r, 0);
            var bg = b.Gradient;
            var rg = r.Gradient;
            var brg = br.Gradient;
            Assert.AreEqual(br.Name, "b,r");
            ColorEq(brg.Evaluate(0.1f), Color.Lerp(bg.Evaluate(0.1f), rg.Evaluate(0.1f), 0.1f));
            brg = NamedGradient.Mix(b, r, 0.1f).Gradient;
            ColorEq(brg.Evaluate(0.1f), Color.Lerp(bg.Evaluate(0.1f), rg.Evaluate(0.1f), 0f));
            ColorEq(brg.Evaluate(0.2f), Color.Lerp(bg.Evaluate(0.2f), rg.Evaluate(0.2f), 0.5f - 0.3f / 0.8f));
        }
    }
}