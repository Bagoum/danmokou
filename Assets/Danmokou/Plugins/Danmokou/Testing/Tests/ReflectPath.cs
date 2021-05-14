using NUnit.Framework;
using Danmokou.DMath;
using UnityEngine;
using static Danmokou.Reflection.Reflector;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

    public static class ReflectPath {

        private static Vector2 V2(float x, float y) => new Vector2(x, y);
        
        private const float err1 = 0.00001f;
        [Test]
        public static void TPolar() {
            var pivoter = "polar * 2 t * 60 t".Into<VTP>();
            var pivoter2 = "polar2 pxy * 2 t * 60 t".Into<VTP>();
            var bpi = new ParametricInfo(Vector2.zero, 1, 0, 0f);
            var bpi2 = new ParametricInfo(Vector2.zero, 1, 0, 0f);
            Movement v = new Movement(pivoter, Vector2.right, V2RV2.Angle(25));
            Movement v2 = new Movement(pivoter2, Vector2.right, V2RV2.Angle(25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 1f);
            v2.UpdateDeltaAssignAcc(ref bpi2, out var _, 1f);
            VecEq(bpi.loc, V2(1, 0) + M.RotateVectorDeg(M.PolarToXY(2, 60), 25), "", err1);
            VecEq(bpi.loc,  bpi2.loc);
            v = new Movement(pivoter, Vector2.left, V2RV2.NRotAngled(1, 1, 25));
            v2 = new Movement(pivoter2, Vector2.left, V2RV2.NRotAngled(1, 1, 25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 1f);
            v2.UpdateDeltaAssignAcc(ref bpi2, out var _, 1f);
            VecEq(bpi.loc, V2(0, 1) + M.RotateVectorDeg(M.PolarToXY(4, 120), 25), "", err1);
            VecEq(bpi.loc,  bpi2.loc);
        }
        [Test]
        public static void TCartesian() {
            var pivoter = "offset px * 2 p py * 2 t".Into<VTP>();
            var bpi = new ParametricInfo(Vector2.down, 3, 0, 0f);
            Movement v = new Movement(pivoter, Vector2.zero, V2RV2.Angle(25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 1f);
            VecEq(bpi.loc, new V2RV2(0, 2, 6, 0, 25).TrueLocation, "", err1);
            
            pivoter = "tp px * 2 p py * 2 t".Into<VTP>();
            bpi = new ParametricInfo(Vector2.down, 3, 0, 0f);
            v = new Movement(pivoter, Vector2.zero, V2RV2.Angle(25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 2f);
            VecEq(bpi.loc, Vector2.down + new V2RV2(0, 4, 6, 0, 25).TrueLocation * 2f, "", err1);
            
            pivoter = "tp px * 2 p px * 2 t".Into<VTP>();
            bpi = new ParametricInfo(Vector2.down, 3, 0, 0f);
            v = new Movement(pivoter, Vector2.zero, V2RV2.Angle(25));
            v.FlipX();
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 2f);
            VecEq(bpi.loc, Vector2.down + new V2RV2(-4, 0, 6, 0, 180-25).TrueLocation * 2f, "", err1);
        }

        [Test]
        public static void TCartesianRotNRot() {
            var p = "roffset px * 2 t".Into<VTP>();
            var bpi = new ParametricInfo(Vector2.down, 3, 0, 0f);
            Movement v = new Movement(p, Vector2.zero, V2RV2.Angle(25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 1f);
            VecEq(bpi.loc, V2RV2.Rot(2, 0, 25).TrueLocation, "", err1);
            
            
            p = "nroffset px * 2 t".Into<VTP>();
            bpi = new ParametricInfo(Vector2.down, 3, 0, 0f);
            v = new Movement(p, Vector2.zero, V2RV2.Angle(25));
            v.UpdateDeltaAssignAcc(ref bpi, out var _, 1f);
            VecEq(bpi.loc, V2RV2.NRot(2, 0).TrueLocation, "", err1);
        }
    }
}