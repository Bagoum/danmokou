using System;using System.Collections.Generic;
using Danmokou.Core;
using NUnit.Framework;
using static Danmokou.Testing.TAssert;

namespace Danmokou.Testing {

public static class TestEvents {
    [Test]
    public static void TestEventsCB() {
        var msgs = new List<string>();
        var c = Events.Event0.Continuous("c").ev;
        var cdm1 = c.Subscribe(() => msgs.Add("cont1"));
        var cdm2 = c.Subscribe(() => msgs.Add("cont2"));
        c.Proc();
        ListEq(msgs, new List<string> { "cont1", "cont2" });
        cdm1.MarkForDeletion();
        c.Proc();
        ListEq(msgs, new List<string> { "cont1", "cont2", "cont2" });
        var cdm3 = c.Subscribe(() => msgs.Add("cont3"));
        c.Proc();
        ListEq(msgs, new List<string> { "cont1", "cont2", "cont2", "cont2", "cont3" });
        Events.Event0.DestroyAll();
    }

    [Test]
    public static void TestEventsRefractor() {
        var msgs = new List<string>();
        var c = Events.Event0.Continuous("c").ev;
        var cdm = c.Subscribe(() => msgs.Add("c"));
        var o = Events.Event0.Once("o").ev;
        var odm = o.Subscribe(() => msgs.Add("o"));
        var r = Events.Event0.Refract("r", "c").ev;
        var rdm = r.Subscribe(() => msgs.Add("r"));
        r.Proc();
        r.Proc();
        o.Proc();
        ListEqClear(msgs, new List<string> { "r", "o" });
        r.Proc();
        c.Proc();
        r.Proc();
        ListEqClear(msgs, new List<string> { "c", "r" });
        o.Proc();
        r.Proc();
        ListEqClear(msgs, new List<string> { });
        c.Proc();
        r.Proc();
        ListEqClear(msgs, new List<string> { "c", "r" });
        Events.Event0.DestroyAll();
    }
    

}
}