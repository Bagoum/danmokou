using System;
using JetBrains.Annotations;
using UnityEngine;

public abstract class RegularUpdater : MonoBehaviour, IRegularUpdater {
    [CanBeNull] private DeletionMarker<IRegularUpdater> token;

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void EnableRegularUpdates() {
        if (token == null) token = ETime.RegisterRegularUpdater(this);
    }
    

    protected virtual void OnEnable() => EnableRegularUpdates();

    public abstract void RegularUpdate();
    public virtual void RegularUpdateParallel() { }
    public virtual int UpdatePriority => UpdatePriorities.DEFAULT;
    
    public virtual bool ReceivePartialUpdates => false;
    public virtual bool UpdateDuringPause => false;

    public virtual void PartialUpdate(float dT) => throw new NotImplementedException();

    public virtual void PreSceneClose() { }

    /// <summary>
    /// Safe to call twice.
    /// </summary>
    protected void DisableRegularUpdates() {
        token?.MarkForDeletion();
        token = null;
    }

    protected virtual void OnDisable() => DisableRegularUpdates();
}

