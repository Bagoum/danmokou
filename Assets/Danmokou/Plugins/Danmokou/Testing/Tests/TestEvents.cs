using System;using System.Collections.Generic;
using System.Reactive;
using Danmokou.Core;
using NUnit.Framework;
using static Danmokou.Testing.TAssert;
using static Danmokou.Core.Events;

namespace Danmokou.Testing {

public static class TestEvents {
    [Test]
    public static void TestEventsCB() {
        var msgs = new List<string>();
        using var _ = CreateRuntimeEvent<float>("c", RuntimeEventType.Normal, out var c);
        var cdm1 = c.Ev.Subscribe(f => msgs.Add($"cont1 {f}"));
        var cdm2 = c.Ev.Subscribe(f => msgs.Add($"cont2 {f}"));
        c.Ev.OnNext(5);
        ListEq(msgs, new List<string> { "cont1 5", "cont2 5" });
        cdm1.Dispose();
        c.Ev.OnNext(4);
        ListEq(msgs, new List<string> { "cont1 5", "cont2 5", "cont2 4" });
        var cdm3 = c.Ev.Subscribe(f => msgs.Add($"cont3 {f}"));
        c.Ev.OnNext(3);
        ListEq(msgs, new List<string> { "cont1 5", "cont2 5", "cont2 4", "cont2 3", "cont3 3" });
    }

    [Test]
    public static void TestEventsRefractor() {
        var msgs = new List<string>();
        using var _ = CreateRuntimeEvent<Unit>("c", RuntimeEventType.Normal, out var c);
        var cdm = c.Ev.Subscribe(_ => msgs.Add("c"));
        using var _o = CreateRuntimeEvent<Unit>("o", RuntimeEventType.Trigger, out var o);
        var odm = o.Ev.Subscribe(_ => msgs.Add("o"));
        using var _r = CreateRuntimeEvent<Unit>("r", RuntimeEventType.Trigger, out var r);
        r.TriggerResetWith(c.Ev);
        var rdm = r.Ev.Subscribe(_ => msgs.Add("r"));
        r.Ev.OnNext(default);
        r.Ev.OnNext(default);
        o.Ev.OnNext(default);
        ListEq(msgs, new List<string> { "r", "o" });
        msgs.Clear();
        r.Ev.OnNext(default);
        c.Ev.OnNext(default);
        r.Ev.OnNext(default);
        ListEq(msgs, new List<string> { "c", "r" });
        msgs.Clear();
        o.Ev.OnNext(default);
        r.Ev.OnNext(default);
        ListEq(msgs, new List<string> { });
        msgs.Clear();
        c.Ev.OnNext(default);
        r.Ev.OnNext(default);
        ListEq(msgs, new List<string> { "c", "r" });
        msgs.Clear();
    }
    

}
}