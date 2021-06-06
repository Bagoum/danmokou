using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.Culture;
using Danmokou.Achievements;
using Danmokou.Core;
using NUnit.Framework;

namespace Danmokou.Testing {
public static class Achievements {
    [Test]
    public static void Utils() {
        Assert.AreEqual(State.Completed, TestAchievementRepo.deal100dmg().EvalState());
        Assert.AreEqual(State.InProgress, TestAchievementRepo.deal300dmg().EvalState());
        Assert.AreEqual(State.Completed, TestAchievementRepo.zeroHits().EvalState());
        Assert.AreEqual(State.Completed, TestAchievementRepo.atMost3Hits().EvalState());


        TestAchievementRepo.SavedState = new Dictionary<string, State>() {
            {"completed", State.Completed},
            {"inprogress", State.InProgress}
        };

        var acs = new TestAchievementsProvider().MakeRepo().Construct();
        
        //Default values
        Assert.AreEqual(acs.FindByKey("dmg100").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("completed").State, State.Completed);
        
        acs.UpdateAll();
        
        Assert.AreEqual(acs.FindByKey("dmg100").State, State.Completed);
        Assert.IsTrue(acs.FindByKey("completed").Req is CompletedFixedReq);
        Assert.AreEqual(acs.FindByKey("dmg300").State, State.InProgress);
        Assert.AreEqual(acs.FindByKey("hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("hits3").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg100hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg100hits3").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg300hits0").State, State.InProgress);
        Assert.AreEqual(acs.FindByKey("dmg300hits3").State, State.InProgress);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("completed").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("inprogress").State, State.InProgress);
        
        TestAchievementRepo.hitsTaken.SetValue(2);
        TestAchievementRepo.dmg.SetValue(400);
        Assert.AreEqual(acs.FindByKey("dmg300").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg100hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg300hits0").State, State.InProgress);
        Assert.AreEqual(acs.FindByKey("dmg300hits3").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("completed").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("inprogress").State, State.InProgress);
        
        TestAchievementRepo.hitsTaken.SetValue(0);
        Assert.AreEqual(acs.FindByKey("dmg300hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg100").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.Locked);
        
        TestAchievementRepo.dmg.SetValue(0);
        Assert.AreEqual(acs.FindByKey("dmg300hits0").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("dmg100").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.Locked);
        Assert.AreEqual(acs.FindByKey("completed").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("inprogress").State, State.InProgress);
        
        TestAchievementRepo.hitsTaken.SetValue(-1);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.InProgress);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.InProgress);
        
        TestAchievementRepo.dmg.SetValue(400);
        Assert.AreEqual(acs.FindByKey("lbyhits-1_dmg300").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("acvreq").State, State.Completed);
        Assert.AreEqual(acs.FindByKey("inprogress").State, State.Completed);
    }


    public class MessageValue<T> {
        public T Value { get; private set; }

        public MessageValue(T initVal) {
            Value = initVal;
        }

        private readonly List<Action> cbs = new List<Action>();
        public void AttachCB(Action onChange) => cbs.Add(onChange);

        public void SetValue(T nVal) {
            Value = nVal;
            foreach (var cb in cbs) cb();
        }
    }

    public class MValReq<T> : Requirement {
        private readonly MessageValue<T> value;
        private readonly Func<T, bool> pred;

        public MValReq(MessageValue<T> val, Func<T, bool> complete) {
            value = val;
            pred = complete;
            value.AttachCB(RequirementUpdated);
        }

        public override State EvalState() => pred(value.Value) ? State.Completed : State.InProgress;
    }


    public class TestAchievementRepo : AchievementRepo {
        public static Dictionary<string, State> SavedState { get; set; } = null!;
        
        public static MessageValue<int> dmg = new MessageValue<int>(200);
        public static MessageValue<int> hitsTaken = new MessageValue<int>(0);

        public static Func<Requirement> deal100dmg => () => new MValReq<int>(dmg, d => d >= 100);
        public static Func<Requirement> deal300dmg => () => new MValReq<int>(dmg, d => d >= 300);
        public static Func<Requirement> zeroHits => () => new MValReq<int>(hitsTaken, h => h <= 0);
        public static Func<Requirement> atMost3Hits => () => new MValReq<int>(hitsTaken, h => h <= 3);

        private static Func<Requirement> And(params Func<Requirement>[] reqs) =>
            () => new AndReq(reqs.Select(r => r()).ToArray());

        private Achievement M(string key, Func<Requirement> req) =>
            new Achievement(key, LString.Empty, LString.Empty, req, this);

        public override IEnumerable<Achievement> MakeAchievements() {
            var lbyhits = M("lbyhits-1_dmg300",
                () => new LockedReq(new MValReq<int>(hitsTaken, h => h <= -1), deal300dmg()));
            return new[] {
                M("dmg100", deal100dmg),
                M("dmg300", deal300dmg),
                M("hits0", zeroHits),
                M("hits3", atMost3Hits),
                M("dmg100hits0", And(deal100dmg, zeroHits)),
                M("dmg100hits3", And(deal100dmg, atMost3Hits)),
                M("dmg300hits0", And(deal300dmg, zeroHits)),
                M("dmg300hits3", And(deal300dmg, atMost3Hits)),
                lbyhits,
                M("completed", deal300dmg),
                M("inprogress", () => new LockedReq(new MValReq<int>(hitsTaken, h => h <= -1), deal300dmg())),
                M("acvreq", () => new AchievementRequirement(lbyhits))
            };
        }
        
        public override State? SavedAchievementState(string key) =>
            SavedState.TryGetValue(key, out var s) ? s : (State?) null;
    }

    public class TestAchievementsProvider : IGameAchievementsProvider {
        public AchievementRepo MakeRepo() => new TestAchievementRepo();
    }
}
}