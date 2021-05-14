using System.Collections.Generic;
using System.Threading.Tasks;
using Danmokou.DMath;

namespace Danmokou.SM {

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