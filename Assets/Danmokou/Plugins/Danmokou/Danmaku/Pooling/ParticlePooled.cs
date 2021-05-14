using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.DMath;
using JetBrains.Annotations;
using UnityEngine;
// ReSharper disable StaticMemberInGenericType

//To write a pooled class XYZ:
// XYZ : Pooled<XYZ>
// Awake should contain first-initialization logic.
// You may want a separate initialize function, but this is controlled by the specific pool.
// By default, an instanced of a pooled class does not need to actually be pooled. It can be created
// normally as well. However, if it is created by the pooler via Request, then it must deactivate and
// call PooledDone when it is finished.
// You can easily check this via `isPooled`, which is inherited from the base Pooled class.
// Here is how BEH handles pooling:
/*
        if (isPooled) {
            PooledDone();
        } else {
            Destroy(gameObject, 1f);
        }
 */
// You probably want a ResetV function that re-initializes the instance when it is depooled. You may call
// ResetV in Awake, but you should not call it in Start, since it may cause issues with TakeParent.
// You must not run tr.SetParent on a pooled object. Instead, use TakeParent. This is because the object
// needs to send itself back to the container on cull if its parentage changes.

namespace Danmokou.Pooling {
//Always pooled
/// <summary>
/// A component attached to pooled particle systems.
/// To configure the particle systems for eg. color, you can attach this component to the prefab
/// beforehand, and set the configuration variables.
/// </summary>
public class ParticlePooled : Pooled<ParticlePooled> {
    [Serializable]
    public struct ParticleSystemColorConfig {
        [Serializable]
        public struct SystemColor {
            public ParticleSystem system;
            public ParticleSystem.MinMaxGradient color;
        }

        public SystemColor[] assignments;

        public void AssignColors() {
            for (int ii = 0; ii < assignments.Length; ++ii) {
                var m = assignments[ii].system.main;
                m.startColor = assignments[ii].color;
            }
        }
    }

    public ParticleSystem System { get; private set; } = null!;
    [Header("Colorize")] 
    public ParticleSystemColorConfig[]? colorizable;
    
    public void OnParticleSystemStopped() {
        PooledDone();
    }

    protected override void Awake() {
        base.Awake();
        System = GetComponent<ParticleSystem>();
        var main = System.main;
        main.stopAction = ParticleSystemStopAction.Callback;
        foreach (var psi in GetComponentsInChildren<ParticleSystem>()) {
            var m = psi.main;
            m.loop = false;
        }
    }

    protected override void ResetValues() {
        ReassignColors();
    }

    [ContextMenu("Reassign")]
    public void ReassignColors() {
        if (colorizable?.Length > 0) {
            colorizable[RNG.GetIntOffFrame(0, colorizable.Length)].AssignColors();
        }
    }

    public void Initialize(Vector2 loc) {
        tr.localPosition = loc;
        System.Play();
    }
}

}
