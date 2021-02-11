

using System;
using JetBrains.Annotations;

namespace DMK.Core {
public enum CancelLevel : int {
    None = 0,
    Operation = 100,
    Scene = 1000
}

public static class CancelHelpers {
    public static void ThrowIfCancelled(this ICancellee c) {
        if (c.Cancelled) throw new OperationCanceledException();
    }

    public static Action Guard(this ICancellee c, Action ifNotCancelled) => () => {
        if (!c.Cancelled) ifNotCancelled();
    };

    public static Action<T> Guard<T>(this ICancellee c, Action<T> ifNotCancelled) => x => {
        if (!c.Cancelled) ifNotCancelled(x);
    };
}

public interface ICancellee {
    bool Cancelled { get; }
    ICancellee Joinable { get; }
}

public class Cancellable : ICancellee {
    public static readonly ICancellee Null = new Cancellable();
    private CancelLevel level = CancelLevel.None;
    public void Cancel() => Cancel(CancelLevel.Operation);
    public bool Cancelled => level > CancelLevel.None;
    public ICancellee Joinable => this;

    public void Cancel(CancelLevel toLevel) {
        if (toLevel > level) level = toLevel;
    }
}

/// <summary>
/// Is locally cancellable, but when joined in nesting, the local information will be discarded.
/// </summary>
public class PassthroughCancellee : ICancellee {
    private readonly ICancellee root;
    private readonly ICancellee local;
    public ICancellee Joinable => root;

    public PassthroughCancellee(ICancellee? root, ICancellee? local) {
        this.root = root?.Joinable ?? Cancellable.Null;
        this.local = local ?? Cancellable.Null;
    }

    public bool Cancelled => root.Cancelled || local.Cancelled;
}

public class JointCancellee : ICancellee {
    private readonly ICancellee c1;
    private readonly ICancellee c2;
    public ICancellee Joinable => this;

    public JointCancellee(ICancellee? c1, ICancellee? c2) {
        this.c1 = c1?.Joinable ?? Cancellable.Null;
        this.c2 = c2?.Joinable ?? Cancellable.Null;
    }

    public bool Cancelled => c1.Cancelled || c2.Cancelled;
}

public interface ICancellee<T> {
    bool Cancelled(out T value);
}

public class GCancellable<T> : ICancellee<T> {
    public static readonly ICancellee<T> Null = new GCancellable<T>();
    private CancelLevel level = CancelLevel.None;
    private T obj = default!;
    public void Cancel(T value) => Cancel(CancelLevel.Operation, value);

    public bool Cancelled(out T value) {
        value = obj;
        return level > CancelLevel.None;
    }

    public void Cancel(CancelLevel toLevel, T value) {
        if (toLevel > level) {
            level = toLevel;
            obj = value;
        }
    }
}
}