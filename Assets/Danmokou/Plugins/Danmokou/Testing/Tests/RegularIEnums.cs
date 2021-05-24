using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Danmokou.Testing {

    public static class RegularIEnums {
        private static IEnumerator Add(List<string> msgs, string msg) {
            msgs.Add(msg);
            yield return null;
        }

        private static IEnumerator AddYieldAdd(List<string> msgs, string msg, IEnumerator? toYield) {
            msgs.Add(msg);
            yield return toYield;
            msgs.Add(msg);
        }
        private static IEnumerator AddYieldAddNull(List<string> msgs, string msg, IEnumerator? toYield) {
            msgs.Add(msg);
            yield return toYield;
            msgs.Add(msg);
            yield return null;
        }

        [Test]
        public static void TestCorrectness() {
            CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
            List<string> msgs = new List<string>();
            cru.RunRIEnumerator(Add(msgs, "0"));
            cru.RunRIEnumerator(Add(msgs, "1"));
            Assert.AreEqual(msgs, new List<string>());
            cru.RegularUpdate();
            Assert.AreEqual(msgs, new List<string>() {"0", "1"});
            Assert.AreEqual(cru.NumRunningCoroutines, 2);
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 0);
            
            
            msgs = new List<string>();
            cru.RunRIEnumerator(
                AddYieldAdd(msgs, "0", 
                AddYieldAdd(msgs, "1", 
                    AddYieldAdd(msgs, "2", null))));
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>());
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2"});
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 0);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2", "2", "1", "0"});
            
            
            msgs = new List<string>();
            cru.RunRIEnumerator(
                AddYieldAddNull(msgs, "0", 
                    AddYieldAddNull(msgs, "1", 
                        AddYieldAddNull(msgs, "2", null))));
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>());
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2"});
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2", "2"});
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2", "2", "1"});
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            Assert.AreEqual(msgs, new List<string>() {"0", "1", "2", "2", "1", "0"});
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 0);
        }

        private static IEnumerator LeaveOnCancel(ICancellee cT) {
            while (true) {
                if (cT.Cancelled) yield break;
                yield return null;
            }
        }
        private static IEnumerator OnePlusTwoFrames(ICancellee cT) {
            IEnumerator TwoFrames() {
                if (cT.Cancelled) yield break;
                yield return null;
                if (cT.Cancelled) yield break;
                yield return null;
            }
            if (cT.Cancelled) yield break;
            yield return TwoFrames();
        }
        [Test]
        public static void TestTimingParenting() {
            CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
            cru.RunRIEnumerator(OnePlusTwoFrames(Cancellable.Null));
            cru.RegularUpdate();
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 0);
        }
        [Test]
        public static void TestCancellation() {
            CoroutineRegularUpdater cru = new GameObject().AddComponent<CoroutineRegularUpdater>();
            var cts = new Cancellable();
            cru.RunRIEnumerator(LeaveOnCancel(cts));
            cru.RegularUpdate();
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 1);
            cts.Cancel();
            cru.RegularUpdate();
            Assert.AreEqual(cru.NumRunningCoroutines, 0);
        }

        [Test]
        public static void TestConversion() {
            Coroutines cors = new Coroutines();
            cors.Run(OnePlusTwoFrames(Cancellable.Null));
            cors.Step();
            Assert.AreEqual(cors.Count, 1);
            IEnumerator ienum = cors.AsIEnum();
            Assert.IsTrue(ienum.MoveNext());
            Assert.IsFalse(ienum.MoveNext());
            ienum = cors.AsIEnum();
            Assert.IsFalse(ienum.MoveNext());
        }
    }
}