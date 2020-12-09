using System;
using DMK.Core;
using DMK.Pooling;
using DMK.Services;
using JetBrains.Annotations;
using UnityEngine;

namespace DMK.Scriptables {
[CreateAssetMenu(menuName = "Effects/EffectStrategy")]
public class EffectStrategy : ScriptableObject {
    [CanBeNull] public SFXConfig sound;
    public LocatorStrategy locator;
    public GameObject particlePrefab;

    public enum SpawnableType {
        Particle,
        RawInstantiate,
        None
    }

    public SpawnableType spawnType;
    public EffectStrategy[] subEffects;

    private void ProcMinors() {
        if (sound != null) SFXService.Request(sound);
    }

    public void Proc(Vector2 source, Vector2 target, float targetPerimeterRadius) =>
        ProcGO(source, target, targetPerimeterRadius);

    [CanBeNull]
    private GameObject ProcGO(Vector2 source, Vector2 target, float targetPerimeterRadius) {
        ProcMinors();
        foreach (var sub in subEffects) sub.Proc(source, target, targetPerimeterRadius);
        if (spawnType == SpawnableType.Particle) {
            return ParticlePooler.Request(particlePrefab, locator.Locate(source, target, targetPerimeterRadius))
                .gameObject;
        } else if (spawnType == SpawnableType.RawInstantiate) {
            GameObject w = GameObject.Instantiate(particlePrefab);
            w.transform.localPosition = locator.Locate(source, target, targetPerimeterRadius);
            return w;
        }
        return null;
    }

    public GameObject ProcNotNull(Vector2 source, Vector2 target, float targetPerimeterRadius) {
        var k = target.x;
        GameObject w = ProcGO(source, target, targetPerimeterRadius);
        if (w == null) throw new Exception("EffectStrategy.ProcNotNull called with null result");
        return w;
    }
}
}
