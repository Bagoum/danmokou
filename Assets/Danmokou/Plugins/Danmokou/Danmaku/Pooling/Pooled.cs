using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Scenes;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Danmokou.Pooling {

public abstract class Pooled : CoroutineRegularUpdater {
    protected bool isPooled = false;
    /// <summary>
    /// True iff the object has a parent other than the default pool container/null.
    /// </summary>
    public bool Parented { get; private set; }
    public Transform tr { get; private set; } = null!;
    protected abstract Transform Container { get; }
    private readonly List<Pooled> trchildren = new();
    private Pooled? parent;

    protected virtual void Awake() {
        tr = transform;
        Parented = tr.parent != null;
    }

    private bool isFirstEnable = true;
    protected override void OnEnable() {
        base.OnEnable();
        //If an object receives awake/enable->disable->enable, then
        // we usually want special enable handling only on the first awake/enable
        ResetValues(isFirstEnable);
        isFirstEnable = false;
    }

    public void TakeParent(Pooled par) {
        //false maintains the local transform. we want to maintain the local rotation/scale
        tr.SetParent((parent = par).tr, false);
        tr.localPosition = Vector3.zero;
        parent.trchildren.Add(this);
        Parented = true;
    }

    protected Vector2 GetParentPosition() {
        return Parented ? (Vector2)tr.parent.position : Vector2.zero;
    }

    public void ResetValuesEnableUpdates() {
        if (isPooled) Parented = false;
        EnableUpdates();
        ResetValues(false);
    }

    /// <summary>
    /// Called by Pooler when a cached object is brought back alive (after object initialization),
    ///  and also for unpooled objects in FirstFrame.
    /// </summary>
    protected virtual void ResetValues(bool isFirst) { }
    
    protected virtual void PooledDone() {
        //Note that all correctly running SM coroutines are already finished
        //by the time this function is invoked. However, there may be some hanging
        //coroutines that have 'yield return null' as their last line. For hygeine,
        //we want to clear these out.
        ForceClosingFrame();
        DisableUpdates();
        if (Parented) {
            if (parent!.gameObject.activeInHierarchy) tr.SetParent(Container, false);
            else {
                //This case occurs when disabling due to scene end, which can occur naturally via eg. scene reload
                //In which case we just don't do the parenting step
            }
            parent.trchildren.Remove(this);
        }
        //This is the easiest way to ensure that no remaining display objects, like child SpriteRenders,
        //are visible after pooled return.
        tr.localPosition = new Vector3(50f, 50f, 0f);
        if (trchildren.Count > 0) {
            foreach (var d in trchildren.ToList())
                d.RepoolOrDestroy();
        }
        trchildren.Clear();
    }

    protected virtual void RepoolOrDestroy() {
        if (isPooled)
            PooledDone();
        else
            Object.Destroy(gameObject);
    }
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
        GameObject go = new() {
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
        active.Add(self);
    }

    protected override void PooledDone() {
        active_ref.Remove(self_ref);
        free_ref.Enqueue(self_ref);
        base.PooledDone();
    }
}

}