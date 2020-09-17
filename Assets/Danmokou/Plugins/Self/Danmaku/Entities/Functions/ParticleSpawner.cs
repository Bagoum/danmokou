using DMath;
using UnityEngine;

public class ParticleSpawner : ProcReader {
    private Transform tr;
    public GameObject particlePrefab;
    private ParticleSystem.Burst baseBurst;

    public string burstScaler;
    private FXY burstScale;

    private void Awake() {
        tr = transform;
        burstScale = burstScaler.Into<FXY>();
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