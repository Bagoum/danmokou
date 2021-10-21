using System;
using System.Collections;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.Danmaku;
using Danmokou.DataHoist;
using Danmokou.DMath;
using Danmokou.Expressions;
using Danmokou.GameInstance;
using Danmokou.Player;
using UnityEngine;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;
using Danmokou.SM;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using BM = Danmokou.Danmaku.BulletManager;
using static NUnit.Framework.Assert;
using static Danmokou.Testing.TAssert;
using PDH = Danmokou.DataHoist.PublicDataHoisting;
using static Danmokou.Services.GameManagement;

namespace Danmokou.Testing {

public static class Scene1 {
    //Note: this is run before every test
    private const string baseScene = "TestMainMenu";
    private const string baseScenePath = "Danmokou/Scenes/Testing/TestMainMenu";
    [SetUp]
    public static void Setup() {
        PatternSM.PHASE_BUFFER = false;
        SceneManager.LoadScene(baseScenePath);
    }

    /// <summary>
    /// Tests:
    /// - Basic functionality of firing API, including RV2 increment and P-assignment
    /// - Firing timing
    /// - Identity of Velocity and Offset methods in trivial cases
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public static IEnumerator TestBasicFiring() {
        TestHarness.OnSOF(() => {
            var mokou = BehaviorEntity.GetExecForID("mokou");
            var red = BM.TPool("strip-red/w");
            var green = BM.TPool("strip-green/w");
            TestHarness.RunBehaviorScript("Regular Fire", "mokou");
            TAssert.VecEq(mokou.rBPI.loc, Vector2.zero, "Location is zero");
            TestHarness.Check(0, () => {
                //Bullet commands are dispatched during IEnum, which occurs sometime during frame 0
                Assert.AreEqual(red.Count, 2);
                TAssert.VecEq(red[0].bpi.loc, new Vector2(1, 0));
                TAssert.VecEq(red[1].bpi.loc, new Vector2(-1, 0));
                TAssert.PoolEq(red, green);
            });
            TestHarness.Check(4, () => {
                Assert.AreEqual(red.Count, 6);
                TAssert.VecEq(red[0].bpi.loc, M.PolarToXY(1 + 4f / 120, 0));
                TAssert.VecEq(red[2].bpi.loc, M.PolarToXY(1 + 2f / 120, 15));
                Assert.AreEqual(red[5].bpi.index, 2);
                TAssert.VecEq(red[5].bpi.loc, M.PolarToXY(1, 210));
                TAssert.PoolEq(red, green);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    
    /// <summary>
    /// Tests:
    /// - Banking (inner repeat)
    /// - Basic functionality of times and rpp
    /// - Basic functionality of gsr
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public static IEnumerator TestBank() {
        TestHarness.OnSOF(() => {
            var mokou = BehaviorEntity.GetExecForID("mokou");
            var red = BM.TPool("strip-red/w");
            var green = BM.TPool("strip-green/w");
            var blue = BM.TPool("strip-purple/w");
            TestHarness.RunBehaviorScript("Bank", "mokou");
            //Position command is dispatched synchronously, before frame 0
            VecEq(mokou.rBPI.loc, Vector2.zero, "Location is zero");
            //Sync bullet commands are also dispatched synchronously
            AreEqual(blue.Count, 2);
            VecEq(red[0].bpi.loc, 2 * Vector2.right);
            VecEq(red[1].bpi.loc, M.PolarToXY(2, 10));
            //Bank <1;:> off <1,:90>
            VecEq(green[0].bpi.loc, 2 * Vector2.up);
            VecEq(green[1].bpi.loc, Vector2.up + M.PolarToXY(1, 100));
            //Bank0 <1;:> off <1,:90>
            VecEq(blue[0].bpi.loc, new Vector2(1f, 1f));
            VecEq(blue[1].bpi.loc, Vector2.up + M.PolarToXY(1, 10));
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestSave() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Save", "mokou");
            var h = new ReflectEx.Hoist<float>("pqw");
            var h2 = new ReflectEx.Hoist<float>("pqw2");
            AreEqual(17, h2.Retrieve(3));
            VecEq(Vector2.up, BM.TPool("sun-red/w")[0].bpi.loc);
            TestHarness.Check(0, () => {
                for (int ii = 0; ii < 10; ++ii) {
                    AreEqual(ii + 10, h.Retrieve(ii));
                }
            });
            TestHarness.Check(4, () => {
                for (int ii = 0; ii < 10; ++ii) {
                    AreEqual(ii + 50, h.Retrieve(ii));
                }
            });
        });
        while (TestHarness.Running) yield return null;
    }

    private const float frame = ETime.FRAME_TIME;
    private const float hframe = frame / 2f;

    private static Vector2 V2(float x, float y) => new Vector2(x, y);
    [UnityTest]
    public static IEnumerator TestSlowControl() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Slow Control", "mokou");
            var red = BM.TPool("strip-red/w");
            TestHarness.Check(0, () => {
                SBPos(ref red[0], V2(0, 0));
            });
            TestHarness.Check(1, () => {
                SBPos(ref red[0], V2(1 * hframe, 0));
            });
            TestHarness.Check(2, () => {
                SBPos(ref red[0], V2(2 * hframe, 0));
                SBPos(ref red[1], V2(0, 0));
            });
            //Circle is only large enough for two frames
            TestHarness.Check(3, () => {
                SBPos(ref red[0], V2(4 * hframe, 0));
                SBPos(ref red[1], V2(1 * hframe, 0));
            });
            //Delete control after three frames
            TestHarness.Check(4, () => {
                SBPos(ref red[0], V2(6 * hframe, 0));
                SBPos(ref red[1], V2(3 * hframe, 0));
            });
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestControlEphemerality() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Control Ephemerality", "mokou");
            var red = BM.TPool("strip-red/w");
            AreEqual(6, red.NumControls);
            var eternal = red.ControlAt(0);
            var sndLastPre = red.ControlAt(2);
            var firstPost = red.ControlAt(4);
            var sndPost = red.ControlAt(5);
            TestHarness.Check(1, () => {
                AreEqual(5, red.NumControls);
                AreEqual(sndLastPre, red.ControlAt(2));
                AreEqual(firstPost, red.ControlAt(3));
            });
            TestHarness.Check(2, () => {
                AreEqual(4, red.NumControls);
                AreEqual(eternal, red.ControlAt(0));
                AreEqual(sndPost, red.ControlAt(3));
            });
            TestHarness.Check(3, () => {
                AreEqual(3, red.NumControls);
                AreEqual(sndPost, red.ControlAt(2));
            });
            TestHarness.Check(4, () => {
                AreEqual(2, red.NumControls);
                AreEqual(eternal, red.ControlAt(0));
                AreEqual(sndLastPre, red.ControlAt(1));
            });
            TestHarness.Check(6, () => {
                AreEqual(1, red.NumControls);
                AreEqual(eternal, red.ControlAt(0));
            });
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestGIR1() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("IRepeat1", "mokou");
            var red = BM.TPool("strip-red/w");
            var green = BM.TPool("strip-green/w");
            var mokou = BehaviorEntity.GetExecForID("mokou");
            TestHarness.Check(0, () => {
                AreEqual(1, red.Count);
                AreEqual(1, green.Count);
                AreEqual(5, mokou.NumRunningCoroutines); //1 for phase timeout, 2 for each irepeat
            });
            TestHarness.Check(1, () => {
                AreEqual(2, red.Count);
                AreEqual(3, green.Count);
                SBPos(ref red[1], V2(-1, 0));
                SBPos(ref green[1], V2(-1, 0));
                SBPos(ref green[2], V2(0, 1));
                AreEqual(3, mokou.NumRunningCoroutines); //phase, red GIR waiting, final green GCR (GIR hoists itself into a callback for last GCR)
            });
            TestHarness.Check(2, () => {
                AreEqual(3, red.Count);
                AreEqual(4, green.Count);
                SBPos(ref red[2], V2(0, 1));
                SBPos(ref green[3], V2(0, -1));
                AreEqual(2, mokou.NumRunningCoroutines); //phase, final red GCR
            });
            TestHarness.Check(3, () => {
                AreEqual(4, red.Count);
                AreEqual(4, green.Count);
                SBPos(ref red[3], V2(0, -1));
                AreEqual(1, mokou.NumRunningCoroutines); //The phase timeout coroutine is zombieing, but that's fine
                AreEqual(0, mokou.NumRunningSMs);
            });
            TestHarness.Check(4, () => {
                AreEqual(0, mokou.NumRunningCoroutines);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestClip() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Clip", "mokou");
            //Green has a "clip false" statement. Red has "clip true". 
            var red = BM.TPool("strip-red/w");
            var green = BM.TPool("strip-green/w");
            TestHarness.Check(0, () => {
                AreEqual(0, red.Count);
                AreEqual(1, green.Count);
            });
            TestHarness.Check(1, () => {
                AreEqual(0, red.Count);
                AreEqual(2, green.Count);
            });
            TestHarness.Check(2, () => {
                AreEqual(0, red.Count);
                AreEqual(2, green.Count);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    [UnityTest]
    public static IEnumerator TestCancel() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Cancel", "mokou");
            var mokou = BehaviorEntity.GetExecForID("mokou");
            var red = BM.TPool("gem-red/w");
            var green = BM.TPool("gem-green/w");
            var blue = BM.TPool("gem-blue/w");
            var orange = BM.TPool("gem-orange/w");
            var rc = 0;
            TestHarness.Check(0, () => {
                AreEqual(1, red.Count);
                AreEqual(1, green.Count);
                AreEqual(0, blue.Count);
                AreEqual(4, orange.Count);
                rc = mokou.NumRunningCoroutines;
            });
            TestHarness.Check(1, () => {
                AreEqual(2, red.Count);
                AreEqual(1, green.Count);
                AreEqual(0, blue.Count);
                AreEqual(4, orange.Count);
                // Only green is finished. red and blue will finish in next preloop
                AreEqual(rc - 1, mokou.NumRunningCoroutines);
            });
            TestHarness.Check(2, () => {
                AreEqual(2, red.Count);
                AreEqual(1, green.Count);
                AreEqual(0, blue.Count);
                AreEqual(1, mokou.NumRunningCoroutines); //The phase timeout coroutine is zombieing, but that's fine
                AreEqual(0, mokou.NumRunningSMs);
            });
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestGTR1() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("GTR1", "mokou");
            var red = BM.TPool("strip-red/w");
            var mokou = BehaviorEntity.GetExecForID("mokou");
            TestHarness.Check(0, () => {
                VecEq(mokou.rBPI.loc, V2(1, 0));
                AreEqual(1, red.Count);
            });
            TestHarness.Check(1, () => {
                VecEq(mokou.rBPI.loc, V2(1, 0));
                AreEqual(2, red.Count);
            });
            TestHarness.Check(2, () => { //gtr wait frame
                VecEq(mokou.rBPI.loc, V2(1, 0));
                AreEqual(2, red.Count);
            });
            TestHarness.Check(3, () => {
                VecEq(mokou.rBPI.loc, V2(2, 0));
                AreEqual(3, red.Count);
            });
            TestHarness.Check(4, () => {
                VecEq(mokou.rBPI.loc, V2(2, 0));
                AreEqual(4, red.Count);
            });
            TestHarness.Check(5, () => {
                VecEq(mokou.rBPI.loc, V2(2, 0));
                AreEqual(5, red.Count);
            });
            TestHarness.Check(6, () => {
                VecEq(mokou.rBPI.loc, V2(2, 0));
                AreEqual(5, red.Count);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    /// <summary>
    /// Tests GTR with multiple children
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public static IEnumerator TestGTR2() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("GTR2", "mokou");
            var red = BM.TPool("strip-red/w");
            var blue = BM.TPool("strip-blue/w");
            TestHarness.Check(0, () => {
                AreEqual(1, red.Count);
                SBPos(ref red[0], V2RV2.RX(1).TrueLocation);
                AreEqual(1, blue.Count);
                SBPos(ref blue[0], V2RV2.RX(1, 180).TrueLocation);
            });
            TestHarness.Check(1, () => {
                AreEqual(2, red.Count);
                SBPos(ref red[1], V2RV2.RX(1, 10).TrueLocation);
                AreEqual(2, blue.Count);
                SBPos(ref blue[1], V2RV2.RX(1, 190).TrueLocation);
            });
            TestHarness.Check(2, () => {
                AreEqual(2, red.Count);
                AreEqual(2, blue.Count);
            });
            TestHarness.Check(3, () => {
                AreEqual(3, red.Count);
                SBPos(ref red[2], V2RV2.RX(1).TrueLocation);
                AreEqual(3, blue.Count);
                SBPos(ref blue[2], V2RV2.RX(1, 180).TrueLocation);
            });
            TestHarness.Check(4, () => {
                AreEqual(4, red.Count);
                SBPos(ref red[3], V2RV2.RX(1, 10).TrueLocation);
                AreEqual(4, blue.Count);
                SBPos(ref blue[3], V2RV2.RX(1, 190).TrueLocation);
            });
            TestHarness.Check(5, () => {
                AreEqual(4, red.Count);
                AreEqual(4, blue.Count);
            });
            TestHarness.Check(6, () => {
                AreEqual(5, red.Count);
                SBPos(ref red[4], V2RV2.RX(1).TrueLocation);
                AreEqual(5, blue.Count);
                SBPos(ref blue[4], V2RV2.RX(1, 180).TrueLocation);
            });
            TestHarness.Check(7, () => {
                AreEqual(6, red.Count);
                SBPos(ref red[5], V2RV2.RX(1, 10).TrueLocation);
                AreEqual(6, blue.Count);
                SBPos(ref blue[5], V2RV2.RX(1, 190).TrueLocation);
            });
        });
        while (TestHarness.Running) yield return null;
    }

    /// <summary>
    /// Tests GTR waiting.
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public static IEnumerator TestGTR3() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("GTR3", "mokou");
            var m = BehaviorEntity.GetExecForID("mokou");
            TestHarness.Check(0, () => VecEq(V2(0, 1), m.rBPI.loc));
            TestHarness.Check(1, () => VecEq(V2(0, 1), m.rBPI.loc));
            TestHarness.Check(2, () => VecEq(V2(1, 1), m.rBPI.loc));
            TestHarness.Check(3, () => VecEq(V2(1, 1), m.rBPI.loc));
            TestHarness.Check(4, () => VecEq(V2(2, 1), m.rBPI.loc));
            TestHarness.Check(5, () => VecEq(V2(2, 1), m.rBPI.loc));
            TestHarness.Check(6, () => VecEq(V2(2, 1), m.rBPI.loc));
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestOnlyOnce() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("OnlyOnce", "mokou");
            var red = BM.TPool("strip-red/w");
            TestHarness.Check(0, () => {
                AreEqual(0, red.Count);
            });
            TestHarness.Check(1, () => {
                AreEqual(5, red.Count);
                SBPos(ref red[1],  M.PolarToXY((1 + frame), 25));
                SBPos(ref red[3],  M.PolarToXY((1 + frame), 75));
            });
            TestHarness.Check(5, () => {
                AreEqual(5, red.Count);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestGlobalSlow() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Global Slow", "mokou");
            var red = BM.TPool("strip-red/w");
            TestHarness.Check(0, () => {
                AreEqual(ETime.Slowdown.Value, 0.25f);
                AreEqual(1, red.Count);
            });
            TestHarness.Check(1, () => {
                AreEqual(ETime.Slowdown.Value, 0.25f);
                AreEqual(2, red.Count);
            });
            TestHarness.Check(2, () => {
                AreEqual(ETime.Slowdown.Value, 1f);
                AreEqual(3, red.Count);
            });
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestManualGuideEmpty() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Manual Empty Guide", "mokou");
            var red = BM.TPool("gem-red/b");
            var e = BM.TPool("empty");
            var baseLoc = V2RV2.NRot(1, 0);
            TestHarness.Check(0, () => {
                AreEqual(4, red.Count);
                for (int ii = 0; ii < 4; ++ii) {
                    var sloc = (baseLoc + V2RV2.RX(2, 45 + ii * 90)).Bank();
                    var eloc = (sloc + V2RV2.RX(0.2f, 32)).Bank() + V2RV2.RY(1f + frame);
                    SBPos(ref e[ii], eloc.TrueLocation);
                    //90 is the direction of the empty bullet (from rotate @ mydir over the movement py + 1 t)
                    var rloc = eloc.BankOffset(90) + V2RV2.RX(1, 80);
                    SBPos(ref red[ii], rloc.TrueLocation);
                }
            });
        });
        while (TestHarness.Running) yield return null;
    }
    [UnityTest]
    public static IEnumerator TestAutoGuideEmpty() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Auto Empty Guide", "mokou");
            var red = BM.TPool("gem-red/b");
            var baseLoc = V2RV2.NRot(1, 0);
            TestHarness.Check(0, () => {
                var e = BM.TPool("empty.1");
                AreEqual(4, red.Count);
                for (int ii = 0; ii < 4; ++ii) {
                    var sloc = (baseLoc + V2RV2.RX(2, 45 + ii * 90)).Bank();
                    var eloc = (sloc + V2RV2.RX(0.2f, 32)).Bank() + V2RV2.RY(1f + frame);
                    SBPos(ref e[ii], eloc.TrueLocation);
                    //90 is the direction of the empty bullet (from rotate @ mydir over the movement py + 1 t)
                    var rloc = eloc.BankOffset(90) + V2RV2.RX(1, 80);
                    SBPos(ref red[ii], rloc.TrueLocation);
                }
            });
        });
        while (TestHarness.Running) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestSummonAlong() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("SummonAlong", "mokou");
            var o = BM.TPool("gem-red/w");
            var bo = BM.TPool("gem-green/w");
            var br = BM.TPool("gem-blue/w");
            var bt = BM.TPool("gem-teal/w");
            var baseLocv2 = V2(1, 0);
            var baseLoc = V2RV2.NRot(1, 0);
            var oloc = baseLoc + V2RV2.RX(1, 80);
            var boloc = baseLoc + V2RV2.RX(1, 160);
            var brloc = baseLoc + V2RV2.RX(1, 240);
            var btloc = baseLoc + V2RV2.RX(1, 320);
            //Verify summons are correct
            SBPos(ref o[0], oloc + V2RV2.RY(0, 30));
            SBPos(ref o[1], oloc + V2RV2.RY(1, 30));
            SBPos(ref bo[0], boloc.Bank() + V2RV2.RY(0));
            SBPos(ref bo[1], boloc.Bank() + V2RV2.RY(1));
            SBPos(ref br[0], brloc.Bank() + V2RV2.RY(0));
            SBPos(ref br[1], brloc.Bank() + V2RV2.RY(1));
            SBPos(ref bt[0], btloc.Bank() + V2RV2.RY(0));
            SBPos(ref bt[1], btloc.Bank() + V2RV2.RY(1));
            TestHarness.Check(0, () => {
                //Verify angle is correct
                SBPos(ref o[0], oloc + V2RV2.Rot(frame, 0, 30));
                SBPos(ref o[1], oloc + V2RV2.Rot(frame, 1, 30));
                SBPos(ref bo[0], (boloc.Bank() + V2RV2.RY(0)).Bank() + V2RV2.RX(frame, 30));
                SBPos(ref bo[1], (boloc.Bank() + V2RV2.RY(1)).Bank() + V2RV2.RX(frame, 30));
                var rp = (brloc.Bank() + V2RV2.RY(0)).Bank(30);
                SBPos(ref br[0], rp + V2RV2.RX(frame, (rp.TrueLocation-baseLocv2).ToDeg()));
                rp = (brloc.Bank() + V2RV2.RY(1)).Bank(30);
                SBPos(ref br[1], rp + V2RV2.RX(frame, (rp.TrueLocation-baseLocv2).ToDeg()));
                //In this case the tangent is 90 degrees from the firing direction (summonalong eq = py t)
                SBPos(ref bt[0], (btloc.Bank() + V2RV2.RY(0)).BankOffset(30) + V2RV2.RX(frame, 90));
                SBPos(ref bt[1], (btloc.Bank() + V2RV2.RY(1)).BankOffset(30) + V2RV2.RX(frame, 90));
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    
    [UnityTest]
    public static IEnumerator TestControlBatching() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Control Batching", "mokou");
            var redw = BM.TPool("gem-red/w");
            var redb = BM.TPool("gem-red/b");
            var bluew = BM.TPool("gem-blue/w");
            var blueb = BM.TPool("gem-blue/b");
            TestHarness.Check(1, () => {
                //redw moves and restyles since the predicate is checked in the batch command
                AreEqual(0, redw.Count);
                AreEqual(1, bluew.Count);
                SBPos(ref bluew[0], V2(-2f, 0f));
                //redb is moved but not restyled since the predicate is not true after the movement
                AreEqual(1, redb.Count);
                AreEqual(0, blueb.Count);
                SBPos(ref redb[0], V2(-2f, 1f));
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestSampling() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Sampling", "mokou");
            var red = BM.TPool("gem-red/w");
            var green = BM.TPool("gem-green/w");
            TestHarness.Check(0, () => {
                SBPos(ref red[0], Vector2.zero);
                SBPos(ref green[0], V2(frame, 0));
            });
            TestHarness.Check(1, () => {
                SBPos(ref red[0], Vector2.zero);
                SBPos(ref green[0], V2(frame * 2, 0));
            });
            TestHarness.Check(2, () => {
                SBPos(ref red[0], Vector2.zero);
                SBPos(ref green[0], V2(frame * 2, 0));
            });
        });
        while (TestHarness.Running) yield return null;
    }

    private static void CheckGCXPSequential() {
        var m = BehaviorEntity.GetExecForID("mokou");
        var p11 = BM.TPool("sakura-pink/w");
        var p12 = BM.TPool("fireball-pink/w");
        var p21 = BM.TPool("sakura-red/w");
        var p22 = BM.TPool("fireball-red/w");
        TestHarness.Check(0, () => {
            VecEq(Vector2.zero, m.rBPI.loc);
            AreEqual(p11.Count, 1);
            AreEqual(p12.Count, 0);
        });
        TestHarness.Check(1, () => {
            AreEqual(p11.Count, 2);
            AreEqual(p12.Count, 1);
        });
        TestHarness.Check(2, () => {
            AreEqual(p11.Count, 2);
            AreEqual(p12.Count, 2);
        });
        //Note: This didn't use to work for GTR, because the first GTR iteration went like this:
        // - call child 1
        // - start waiter
        // > child 1 finishes, calls child 2
        // so the waiter ended up running after the child. This was fixed with RunTryPrepend for Async and Move commands.
        TestHarness.Check(4, () => {
            AreEqual(p11.Count, 3);
            AreEqual(p12.Count, 2);
        });
        TestHarness.Check(5, () => {
            AreEqual(p11.Count, 4);
            AreEqual(p12.Count, 3);
            AreEqual(p21.Count, 0);
        });
        TestHarness.Check(6, () => {
            AreEqual(p11.Count, 4);
            AreEqual(p12.Count, 4);
            AreEqual(p21.Count, 1);
            AreEqual(p22.Count, 0);
        });
        TestHarness.Check(7, () => {
            AreEqual(p21.Count, 2);
            AreEqual(p22.Count, 1);
        });
        TestHarness.Check(8, () => {
            AreEqual(p21.Count, 3);
            AreEqual(p22.Count, 2);
        });
        TestHarness.Check(9, () => {
            AreEqual(p21.Count, 4);
            AreEqual(p22.Count, 3);
        });
        TestHarness.Check(10, () => {
            AreEqual(p21.Count, 4);
            AreEqual(p22.Count, 4);
            VecEq(V2(2, 0), m.rBPI.loc);
        });
    }
    [UnityTest]
    public static IEnumerator TestGCXPSequential_GIR() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("GCXP Sequential GIR", "mokou");
            CheckGCXPSequential();
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestGCXPSequential_GTR() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("GCXP Sequential GTR", "mokou");
            CheckGCXPSequential();
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestSummonTime() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("Summon Time", "mokou");
            var green = BM.TPool("shell-green/w");
            var red = BM.TPool("shell-red/w");
            TestHarness.Check(6, () => {
                SBPos(ref red[0], M.PolarToXY(1, 0));
                SBPos(ref red[1], M.PolarToXY(1 + frame, 2));
                SBPos(ref red[2], M.PolarToXY(1 + 2 * frame, 4));
                SBPos(ref red[3], M.PolarToXY(1, 10));
                SBPos(ref red[4], M.PolarToXY(1 + frame, 12));
                SBPos(ref red[5], M.PolarToXY(1 + 2 * frame, 14));
                SBPos(ref green[0], M.PolarToXY(1, 0));
                SBPos(ref green[1], M.PolarToXY(1 + frame, 2));
                SBPos(ref green[2], M.PolarToXY(1 + 2 * frame, 4));
                SBPos(ref green[3], M.PolarToXY(1 + 4 * frame, 10));
                SBPos(ref green[4], M.PolarToXY(1 + 5 * frame, 12));
                SBPos(ref green[5], M.PolarToXY(1 + 6 * frame, 14));
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestRunFor() {
        TestHarness.OnSOF(() => {
            TestHarness.RunBehaviorScript("RunFor", "mokou");
            var blue = BM.TPool("strip-blue/w");
            var red = BM.TPool("strip-red/w");
            TestHarness.Check(14, () => {
                AreEqual(blue.Count, 3);
                AreEqual(red.Count, 12);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    
    [UnityTest]
    public static IEnumerator TestStaticAnalysis1_1() {
        TestHarness.OnSOF(() => {
            var m = BehaviorEntity.GetExecForID("mokou");
            TestHarness.RunBehaviorScript("Static Analysis 1", "mokou");
            //Case 1: setup goes to phase 2, which goes to 3 and then 4.
            TestHarness.Check(2, () => {
                AreEqual(0, PublicDataHoisting.GetF("v1", 1));
                AreEqual(6, PublicDataHoisting.GetF("v1", 2));
                AreEqual(7, PublicDataHoisting.GetF("v1", 3));
                AreEqual(8, PublicDataHoisting.GetF("v1", 4));
                AreEqual(0, m.NumRunningSMs);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestStaticAnalysis1_2() {
        TestHarness.OnSOF(() => {
            var m = BehaviorEntity.GetExecForID("mokou");
            bool cb = false;
            m.phaseController.Override(3, () => cb = true, false);
            TestHarness.RunBehaviorScript("Static Analysis 1", "mokou");
            //Case 2: setup is overriden, phase 3 is run.
            TestHarness.Check(0, () => IsTrue(!cb));
            TestHarness.Check(2, () => {
                AreEqual(0, PublicDataHoisting.GetF("v1", 1));
                AreEqual(0, PublicDataHoisting.GetF("v1", 2));
                AreEqual(7, PublicDataHoisting.GetF("v1", 3));
                AreEqual(0, PublicDataHoisting.GetF("v1", 4));
                IsTrue(cb);
                AreEqual(0, m.NumRunningSMs);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    [UnityTest]
    public static IEnumerator TestStaticAnalysis1_3() {
        TestHarness.OnSOF(() => {
            var m = BehaviorEntity.GetExecForID("mokou");
            m.phaseController.Override(3, null, false);
            TestHarness.RunBehaviorScript("Static Analysis 1", "mokou");
            //Case 3: setup is overriden, phase 3 is run; without a callback, goes to phase 4.
            TestHarness.Check(2, () => {
                AreEqual(0, PublicDataHoisting.GetF("v1", 1));
                AreEqual(0, PublicDataHoisting.GetF("v1", 2));
                AreEqual(7, PublicDataHoisting.GetF("v1", 3));
                AreEqual(0, PublicDataHoisting.GetF("v1", 4));
                AreEqual(0, m.NumRunningSMs);
            });
        });
        while (TestHarness.Running) yield return null;
    }
    
    [UnityTest]
    public static IEnumerator TestPracticeSelector1() {
        bool cb = false;
        //Running Static Analysis 1
        new InstanceRequest(d => cb = true, FixedDfc(FixedDifficulty.Lunatic),
            new BossPracticeRequest(AllPBosses[0], 
                new SMAnalysis.Phase(null!, PhaseType.NONSPELL, 3, new LString("_")))).Run();
        IsFalse(cb);
        yield return WaitForLoad();
        for (int ii = 0; ii < 10; ++ii) yield return null;
        var m = BehaviorEntity.GetExecForID("mokou");
        AreEqual(0, PublicDataHoisting.GetF("v1", 1));
        AreEqual(0, PublicDataHoisting.GetF("v1", 2));
        //Go to third phase and then callback
        AreEqual(7, PublicDataHoisting.GetF("v1", 3));
        AreEqual(0, PublicDataHoisting.GetF("v1", 4));
        AreEqual(0, m.NumRunningSMs);
        IsFalse(cb);
        //2 second delay
        for (int ii = 0; ii < ETime.ENGINEFPS_F * InstanceRequest.WaitBeforeReturn; ++ii) yield return null;
        IsTrue(cb);
        AreEqual("TestPractice", SceneManager.GetActiveScene().name);
    }
    private static SharedInstanceMetadata FixedDfc(FixedDifficulty fd) => 
        new SharedInstanceMetadata(new TeamConfig(0, Subshot.TYPE_D, null, 
                (ServiceLocator.Find<PlayerController>().defaultPlayers[0], 
                    ServiceLocator.Find<PlayerController>().defaultShots[0])),
            new DifficultySettings(fd));
    [UnityTest]
    public static IEnumerator TestDifficultySelect() {
        foreach (var dff in Enum.GetValues(typeof(FixedDifficulty)).Cast<FixedDifficulty>()) {
            //Running Difficulty Display Test
            DebugFloat.values.Clear();
            yield return null;
            new InstanceRequest(null, FixedDfc(dff), new BossPracticeRequest(AllPBosses[1])).Run();
            yield return WaitForLoad();
            AreEqual(Instance.mode, InstanceMode.BOSS_PRACTICE);
            AreEqual(GameManagement.Difficulty.standard!.Value, dff);
            AreEqual(GameManagement.Difficulty.Value / FixedDifficulty.Hard.Value(), DebugFloat.values[0]);
            SceneManager.LoadScene(baseScene);
            while (TestHarness.Running) yield return null;
        }
    }

    [UnityTest]
    public static IEnumerator TestCampaign() {
        SaveData.r.TutorialDone = true;
        AreEqual(SceneManager.GetActiveScene().name, baseScene);
        bool campaignComplete = false;
        InstanceRequest.RunCampaign(MainCampaign, () => campaignComplete = true, 
            FixedDfc(FixedDifficulty.Lunatic));
        AreEqual(GameManagement.Difficulty.standard!.Value, FixedDifficulty.Lunatic);
        IsFalse(campaignComplete);
        yield return WaitForLoad();
        AreEqual(SceneManager.GetActiveScene().name, "TestStage1");
        IsFalse(campaignComplete);
        ServiceLocator.Find<LevelController>().ShiftPhase();
        yield return WaitForLoad();
        AreEqual(SceneManager.GetActiveScene().name, "TestStage2");
        IsFalse(campaignComplete);
        ServiceLocator.Find<LevelController>().ShiftPhase();
        yield return WaitForLoad();
        IsTrue(campaignComplete);
        AreEqual(SceneManager.GetActiveScene().name, baseScene);
    }

    private static IEnumerator WaitForLoad() {
        yield return null;
        while (EngineStateManager.State == EngineState.LOADING_PAUSE) yield return null;
    }

    [UnityTest]
    public static IEnumerator TestCampaignQuit() {
        SaveData.r.TutorialDone = true;
        AreEqual(SceneManager.GetActiveScene().name, baseScene);
        InstanceRequest.RunCampaign(MainCampaign, null, FixedDfc(FixedDifficulty.Lunatic));
        AreEqual(GameManagement.Difficulty.standard!.Value, FixedDifficulty.Lunatic);
        yield return WaitForLoad();
        AreEqual(SceneManager.GetActiveScene().name, "TestStage1");
        GameManagement.GoToMainMenu();
        yield return WaitForLoad();
        AreEqual(SceneManager.GetActiveScene().name, "TestMainMenu");
    }

}
}