using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Scenes;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmokou.Pooling {

public abstract class Pooled : CoroutineRegularUpdater {
    protected bool isPooled = false;
    /// <summary>
    /// True iff the object has a parent other than the default pool container/null.
    /// </summary>
    protected bool parented;
    protected Transform tr = null!;
    protected abstract Transform Container { get; }
    private readonly List<Pooled> dependents = new List<Pooled>();
    private Pooled? parent;

    protected virtual void Awake() {
        tr = transform;
        parented = tr.parent != null;
    }

    protected override void OnEnable() {
        ResetValues();
        base.OnEnable();
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

    public void ResetValuesEnableUpdates() {
        if (isPooled) parented = false;
        EnableUpdates();
        ResetValues();
    }

    /// <summary>
    /// Called by Pooler when a cached object is brought back alive,
    /// as well as during first instantiation in OnEnable (after Awake, but before object is fully created).
    /// </summary>
    protected virtual void ResetValues() { }
    
    protected virtual void PooledDone() {
        //Note that all correctly running SM coroutines are already finished
        //by the time this function is invoked. However, there may be some hanging
        //coroutines that have 'yield return null' as their last line. For hygeine,
        //we want to clear these out.
        ForceClosingFrame();
        DisableUpdates();
        if (parented) {
            if (parent!.gameObject.activeInHierarchy) tr.SetParent(Container, false);
            else {
                //This case occurs when disabling due to scene end, which can occur naturally via eg. scene reload
                //In which case we just don't do the parenting step
            }
            parent.dependents.Remove(this);
        }
        //This is the easiest way to ensure that no remaining display objects, like child SpriteRenders,
        //are visible after pooled return.
        tr.localPosition = new Vector3(50f, 50f, 0f);
        if (dependents.Count > 0) {
            foreach (var d in dependents.ToList()) {
                d.ExternalDestroy();
            }
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
public abstract class Pooled<P> : Pooled where P : class {
    // ReSharper disable once StaticMemberInGenericType
    private static Transform container = null!;
    protected override Transform Container {
        get {
            if (container == null) {
                SceneIntermediary.SceneLoaded.Subscribe(_ => CreateParticleContainer());
                //The event or may not run immediately on load, depending on when the static constructor runs
                if (container == null)
                    CreateParticleContainer();
            }
            return container!;
        }
    }
    private HashSet<P> active_ref = null!;
    private Queue<P> free_ref = null!;
    private P self_ref = null!;
    public virtual bool ShowUnderContainer => true;

    private static void CreateParticleContainer() {
        GameObject go = new GameObject {
            name = $"{typeof(P).Name} Pool Container"
        };
        container = go.transform;
        container.position = Vector3.zero;
    }

    public void SetPooled(HashSet<P> active, Queue<P> free, P self) {
        active_ref = active;
        free_ref = free;
        isPooled = true;
        self_ref = self;
        tr.SetParent(ShowUnderContainer ? Container : null);
    }

    protected override void PooledDone() {
        active_ref.Remove(self_ref);
        free_ref.Enqueue(self_ref);
        base.PooledDone();
    }
}

}