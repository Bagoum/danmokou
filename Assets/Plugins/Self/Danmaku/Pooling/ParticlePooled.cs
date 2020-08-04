using System;
using System.Collections.Generic;
using System.Linq;
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
    public ParticleSystem System { get; private set; }
    [Header("Colorize")] 
    public ParticleSystemColorConfig[] colorizable;
    
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
        ResetV();
    }

    public override void ResetV() {
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

public abstract class Pooled : CoroutineRegularUpdater {
    protected bool isPooled = false;
    /// <summary>
    /// True iff the object has a parent other than the default pool container/null.
    /// </summary>
    protected bool parented;
    protected Transform tr;
    protected abstract Transform Container { get; }
    private readonly List<Pooled> dependents = new List<Pooled>();
    [CanBeNull] private Pooled parent;

    protected virtual void Awake() {
        tr = transform;
        parented = tr.parent != null;
    }

    public void TakeParent(Pooled par) {
        //false maintains the local transform. we want to maintain the local rotation/scale
        tr.SetParent((parent = par).tr, false);
        tr.localPosition = Vector3.zero;
        parent.dependents.Add(this);
        parented = true;
    }

    protected Vector2 GetParentPosition() {
        return parented ? (Vector2)tr.parent.position : Vector2.zero;
    }

    /// <summary>
    /// Called by Pooler when a cached object is brought back alive.
    /// NOT called on first instantiation-- add a call in Awake if you need that.
    /// (BehaviorEntity calls this in Awake)
    /// </summary>
    public virtual void ResetV() {
        if (isPooled) parented = false;
        EnableRegularUpdates();
    }
    
    protected virtual void PooledDone() {
        //Note that all correctly running SM coroutines are already finished
        //by the time this function is invoked. However, there may be some hanging
        //coroutines that have 'yield return null' as their last line. For hygeine,
        //we want to clear these out.
        ForceClosingFrame();
        DisableRegularUpdates();
        if (parented) {
            tr.SetParent(Container, false);
            parent.dependents.Remove(this);
        }
        //This is the easiest way to ensure that no remaining display objects, like child SpriteRenders,
        //are visible after pooled return.
        tr.localPosition = new Vector3(50f, 50f, 0f);
        foreach (var d in dependents.ToList()) {
            d.ExternalDestroy();
        }
        dependents.Clear();
    }

    protected virtual void ExternalDestroy() => PooledDone();
}
/// <summary>
/// An object that can be constructed by a Pooler. Does not need to be pooled.
/// Objects will not receive regular update calls while in the pooled cache.
/// </summary>
/// <typeparam name="P"></typeparam>
public abstract class Pooled<P> : Pooled {
    private static Func<Transform> container_ref;
    protected override Transform Container => container_ref();
    private HashSet<P> active_ref;
    private Queue<P> free_ref;
    private P self_ref;

    public static void Prepare(Func<Transform> container) {
        container_ref = container;
    }

    public void SetPooled(HashSet<P> active, Queue<P> free, P self) {
        active_ref = active;
        free_ref = free;
        isPooled = true;
        self_ref = self;
        tr.SetParent(container_ref(), false);
    }

    protected override void PooledDone() {
        active_ref.Remove(self_ref);
        free_ref.Enqueue(self_ref);
        base.PooledDone();
    }

}
