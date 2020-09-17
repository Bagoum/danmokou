using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Danmaku;
using DMath;
using JetBrains.Annotations;

namespace SM {

public class DebugFloat : LineActionSM {
    private readonly GCXF<float> f;
    public static readonly List<float> values = new List<float>();
    public DebugFloat(GCXF<float> f) {
        this.f = f;
    }

    public override Task Start(SMHandoff smh) {
        values.Add(f(smh.GCX));
        return Task.CompletedTask;
    }
}

}