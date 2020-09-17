using System;
using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Effects/EffectStrategy")]
public class EffectStrategy : ScriptableObject {
    [CanBeNull] public SOProccable proccer;
    [CanBeNull] public SFXConfig sound;
    public LocatorStrategy.Strategy locator;
    public GameObject particlePrefab;
    public enum SpawnableType {
        Particle,
        RawInstantiate,
        None
    }

    public SpawnableType spawnType;

    private void ProcMinors() {
        if (proccer != null) proccer.Proc();
        if (sound != null) SFXService.Request(sound);
    }
    public void Proc(Vector2 source, Vector2 target, float targetPerimeterRadius) {
        ProcMinors();
        if (spawnType == SpawnableType.Particle) {
            ParticlePooler.Request(particlePrefab, LocatorStrategy.Locate(locator, source, target, targetPerimeterRadius));
        } else if (spawnType == SpawnableType.RawInstantiate) {
            GameObject w = GameObject.Instantiate(particlePrefab);
            w.transform.localPosition = LocatorStrategy.Locate(locator, source, target, targetPerimeterRadius);
        }
    }
    
    [CanBeNull]
    private GameObject ProcGO(Vector2 source, Vector2 target, float targetPerimeterRadius) {
        ProcMinors();
        if (spawnType == SpawnableType.Particle) {
            return ParticlePooler.Request(particlePrefab, LocatorStrategy.Locate(locator, source, target, targetPerimeterRadius)).gameObject;
        } else if (spawnType == SpawnableType.RawInstantiate) {
            GameObject w = GameObject.Instantiate(particlePrefab);
            w.transform.localPosition = LocatorStrategy.Locate(locator, source, target, targetPerimeterRadius);
            return w;
        }
        return null;
    }

    public GameObject ProcNotNull(Vector2 source, Vector2 target, float targetPerimeterRadius) {
        GameObject w = ProcGO(source, target, targetPerimeterRadius);
        if (w == null) throw new Exception("EffectStrategy.ProcNotNull called with null result");
        return w;
    }
}
