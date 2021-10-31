using System;
using BagoumLib.Expressions;
using UnityEngine;
using NUnit.Framework;
using Ex = System.Linq.Expressions.Expression;
using Danmokou.DMath;
using Danmokou.Expressions;
using static Danmokou.Expressions.ExUtils;
using static Danmokou.Expressions.ExMHelpers;
using static NUnit.Framework.Assert;
using static Danmokou.DMath.Functions.ExM;
using static Danmokou.Testing.TAssert;
using static Danmokou.DMath.Functions.ExMV3;
using static Danmokou.Reflection.Compilers;
using static Danmokou.DMath.Functions.ExMLerps;
using static Danmokou.DMath.Functions.ExMConversions;
using static Danmokou.DMath.Functions.ExMMod;

namespace Danmokou.Testing {

public static class EMath {
        private const float err = 0.00001f;

        [Test]
        public static void TestLookupSin() {
            var xv = VFloat();
            var fsin = Ex.Lambda<FXY>(ExMHelpers.dLookupSinRad(xv.As<double>()), xv).Compile();
            for (float x = -16.5f; x < 16.5f; x += 0.0001f) {
                AreEqual(fsin(x), Mathf.Sin(x), 0.00001f);
            }
        }

        [Test]
        public static void TestV2Utils() {
            var exv2 = new TExV2();
            var exb = new TExV2();
            var r = VFloat();
            var rot = Ex.Lambda<Func<Vector2, float, Vector2>>(Rotate(r, exv2), exv2, r).Compile();
            var norm = Ex.Lambda<Func<Vector2, Vector2>>(Norm(exv2), exv2).Compile();
            var conv = Ex.Lambda<Func<Vector2, Vector2, Vector2>>(ConvertBasis(exv2, exb), exv2, exb).Compile();
            var deconv = Ex.Lambda<Func<Vector2, Vector2, Vector2>>(DeconvertBasis(exv2, exb), exv2, exb).Compile();
            for (float x = -3; x < 3; x += 0.5f) {
                for (float y = -3; y < 3; y += 0.5f) {
                    var v2 = new Vector2(x, y);
                    for (float ang = -300; ang < 400; ang += 24) {
                        var dir = M.CosSinDeg(ang);
                        VecEq(rot(new Vector2(x,y),ang), M.RotateVectorDeg(x, y, ang));
                        VecEq(M.ConvertBasis(v2, dir), conv(v2, dir));
                        VecEq(M.DeconvertBasis(v2, dir), deconv(v2, dir));
                        VecEq(deconv(conv(v2,dir), dir), v2);
                    }
                    VecEq(norm(new Vector2(x,y)), (new Vector2(x,y)).normalized);
                }
            }
        }
/*
        private static Vector3 CylinderWrap(float r, float a0, float amax, float axis, Vector2 loc) {
            float c = M.Cos(axis);
            float s = M.Sin(axis);
        }*/

        [Test]
        public static void TestCylinder() {
            var xz = TP3(bpi => XZrY(ExC(3f), ExC(2f), bpi.t, 5f));
            var yz = TP3(bpi => YZrX(ExC(3f), ExC(2f), bpi.t, 5f));
            var xy = TP3(bpi => XYrZ(ExC(3f), ExC(2f), bpi.t, 5f));
            var b = new ParametricInfo() { t = 1f };
            var c = 2f * Mathf.Cos(b.t * M.TAU / 3f);
            var s = 2f * Mathf.Sin(b.t * M.TAU / 3f);
            VecEq(new Vector3(c,5f,s), xz(b));
            VecEq(new Vector3(5f,c,s), yz(b));
            VecEq(new Vector3(c,s,5f), xy(b));

            var f1 = VFloat();
            var f2 = VFloat();
            var f3 = VFloat();
            var f4 = VFloat();
            var v21 = new TExV2();
            var cyl = Ex.Lambda<Func<float, float, float, float, Vector2, Vector3>>(CylinderWrap(f1, f2, f3, f4, v21), f1, f2, f3, f4, v21).Compile();
            
            VecEq(cyl(2f, M.HPI, M.PI, 0f, new Vector2(1f, 0f)), new Vector3(Mathf.Cos(1f/2f) * 2f - 2f, 0f, -2f * Mathf.Sin(1f/2f)));
            VecEq(cyl(2f, M.HPI, M.PI, 0f, new Vector2(1f + 2*M.PI, 0f)), new Vector3(-4f, 0f, 1f));

            for (float r = 1; r < 3; r += 0.8f) {
                for (float ang0 = -1; ang0 < 2; ang0 += 0.8f) {
                    for (float maxWrap = 0.5f; maxWrap < 5f; maxWrap += 1.1f) {
                        for (float axis = -2; axis < 5; axis += 0.9f) {
                            for (float x = -3; x < 10; x += 1.3f) {
                                for (float y = -2; y < 3; y += 0.5f) {
                                    var v2 = new Vector2(x,y);
                                    VecEq(cyl(r, ang0, maxWrap, axis, v2), M.CylinderWrap(r, ang0, maxWrap, axis, v2), $"Cyl({r},{ang0},{maxWrap},{axis},<{x},{y}>)");
                                }
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public static void TestLerp() {
            var x = VFloat();
            var ex = LerpMany(new (TEx<float>, TEx<float>)[] {
                (1, 10),
                (2, 20),
                (3, 30),
                (4, x.Mul(2))
            }, x);
            var f = Ex.Lambda<FXY>(ex, x).Compile();
            AreEqual(f(0.5f), 10);
            AreEqual(f(1.5f), 15);
            AreEqual(f(2.3f), 23);
            AreEqual(f(3.6f), 0.6f * 7.2f + 0.4f * 30, err);
            AreEqual(f(5.9f), 11.8f);
        }

        [Test]
        public static void TestSelect() {
            var x = VFloat();
            var ex = Select(x, new TEx<float>[] {
                10,
                20,
                30
            });
            var f = Ex.Lambda<FXY>(ex, x).Compile();
            AreEqual(f(0), 10);
            AreEqual(f(0.5f), 10);
            AreEqual(f(1), 20);
            AreEqual(f(2), 30);
            AreEqual(f(3), 30);
            
        }

        private static Vector2 V2(float x, float y) => new Vector2(x,y);
        private static Vector3 V3(float x, float y,float z) => new Vector3(x,y,z);
        
        [Test]
        public static void TestConvert() {
            var r = new TEx<float>();
            var x2 = new TExV2();
            var x3 = new TExV3();
            var f = Ex.Lambda<Func<Vector3, Vector2>>(ToSphere(x3), x3).Compile();
            var f2 = Ex.Lambda<Func<float, Vector2, Vector3>>(FromSphere(r, x2), r, x2).Compile();
            VecEq(f(V3(0,0,1)), V2(0, 0));
            VecEq(f2(3f, V2(0, 0)), V3(0,0,3));
            VecEq(f(V3(0,1,0)), V2(90, 90));
            VecEq(f2(3f, V2(90, 90)), V3(0,3,0));
            VecEq(f(V3(1,0,0)), V2(0, 90));
            VecEq(f2(3f, V2(0, 90)), V3(3,0,0));
            VecEq(f(V3(1,1,0)), V2(45, 90));
            VecEq(f(V3(0.5f, 0.5f, Mathf.Sqrt(0.5f))), V2(45, 45));
            VecEq(f2(2f, V2(45, 45)), V3(1f, 1f, 2*Mathf.Sqrt(0.5f)));
        }

        private static float Rt(float x) => Mathf.Sqrt(x);

        [Test]
        public static void TestVectorOps() {
            var x = new TExV3();
            var y = new TExV3();
            var f = Ex.Lambda<Func<Vector3, Vector3, Vector3>>(CrossProduct(x, y), x, y).Compile();
            VecEq(f(V3(1,0,0), V3(0,2,0)), V3(0,0,2));
            VecEq(f(V3(1,0,0), V3(2,2,0)), V3(0,0,2));
            VecEq(f(V3(0,1,0), V3(0,0,1)), V3(1,0,0));
            VecEq(f(V3(1,0,0), V3(0,0,1)), V3(0,-1,0));
            var z = new TEx<float>();
            var f2 = Ex.Lambda<Func<float, Vector3, Vector3, Vector3>>(RotateInPlane(z, x, y), z, x, y).Compile();
            VecEq(f2(45, V3(0,0,2), V3(2,0,0)), Mathf.Sqrt(2) * V3(1,1,0));
            VecEq(f2(30, V3(0,4,0), V3(2,0,0)), V3(Rt(3),0,-1));
        }

        [Test]
        public static void Remaps() {
            var m = VFloat();
            var i = VFloat();
            var f = Ex.Lambda<Func<float, float, float>>(RemapIndex(m, i), m, i).Compile();
            AreEqual(f(5, 0), 0);
            AreEqual(f(5, 1), 4);
            AreEqual(f(5, 2), 3);
            AreEqual(f(5, 3), 2);
            AreEqual(f(5, 4), 1);
            var f2 = Ex.Lambda<Func<float, float, float>>(RemapIndexLoop(m, i), m, i).Compile();
            AreEqual(f2(5, 100), 100);
            AreEqual(f2(5, 101), 104);
            AreEqual(f2(5, 102), 103);
            AreEqual(f2(5, 103), 102);
            AreEqual(f2(5, 104), 101);
        }

    }
}