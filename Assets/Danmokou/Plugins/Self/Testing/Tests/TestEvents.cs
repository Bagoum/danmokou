using System;
using System.Collections.Generic;
using Core;
using DMath;
using NUnit.Framework;
using SM;
using static NUnit.Framework.Assert;
using static DMath.Parser;
using static Tests.TAssert;
using static Core.Events;

namespace Tests {

public static class TestEvents {
    [Test]
    public static void TestEventsCB() {
        var msgs = new List<string>();
        var c = Event0.Continuous("c").ev;
        var cdm1 = c.Listen(() => msgs.Add("cont1"));
        var cdm2 = c.Listen(() => msgs.Add("cont2"));
        c.InvokeIfNotRefractory();
        ListEq(msgs, new List<string> { "cont1", "cont2" });
        cdm1.MarkForDeletion();
        c.InvokeIfNotRefractory();
        ListEq(msgs, new List<string> { "cont1", "cont2", "cont2" });
        var cdm3 = c.Listen(() => msgs.Add("cont3"));
        c.InvokeIfNotRefractory();
        ListEq(msgs, new List<string> { "cont1", "cont2", "cont2", "cont2", "cont3" });
        Event0.DestroyAll();
    }

    [Test]
    public static void TestEventsRefractor() {
        var msgs = new List<string>();
        var c = Event0.Continuous("c").ev;
        var cdm = c.Listen(() => msgs.Add("c"));
        var o = Event0.Once("o").ev;
        var odm = o.Listen(() => msgs.Add("o"));
        var r = Event0.Refract("r", "c").ev;
        var rdm = r.Listen(() => msgs.Add("r"));
        r.InvokeIfNotRefractory();
        r.InvokeIfNotRefractory();
        o.InvokeIfNotRefractory();
        ListEqClear(msgs, new List<string> { "r", "o" });
        r.InvokeIfNotRefractory();
        c.InvokeIfNotRefractory();
        r.InvokeIfNotRefractory();
        ListEqClear(msgs, new List<string> { "c", "r" });
        o.InvokeIfNotRefractory();
        r.InvokeIfNotRefractory();
        ListEqClear(msgs, new List<string> { });
        c.InvokeIfNotRefractory();
        r.InvokeIfNotRefractory();
        ListEqClear(msgs, new List<string> { "c", "r" });
        Event0.DestroyAll();
    }
    

}
}