using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Pooling;
using Danmokou.Reflection;
using UnityEngine;

namespace Danmokou.Behavior.Functions {
public class ParticleSpawner : ProcReader {
    private Transform tr = null!;
    public GameObject particlePrefab = null!;
    private ParticleSystem.Burst baseBurst;

    [ReflectInto(typeof(FXY))]
    public string burstScaler = "";
    private FXY burstScale  = null!;

    private void Awake() {
        tr = transform;
        burstScale = ReflWrap<FXY>.Wrap(burstScaler);
        var em = particlePrefab.GetComponent<ParticleSystem>().emission;
        baseBurst = em.GetBurst(0);
    }

    protected override void Check(int procs) {
        if (procs > 0) {
            var p = ParticlePooler.Request(particlePrefab, tr.position);
            var newBurst = baseBurst;
            var ct = newBurst.count;
            ct.constant *= burstScale(procs);
            newBurst.count = ct;
            p.System.emission.SetBurst(0, newBurst);
        }
    }
}
}