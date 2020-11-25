using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {
//Inspector-exposed structs cannot be readonly
[Serializable]
public struct Frame {
    public Sprite sprite;
    public float time;
    public bool skipLoop;
}

public struct FrameRunner {
    private AnimationType currType;
    private int currFrameIndex;
    private float currTime;
    private Frame[] currFrames;
    private bool doLoop;
    [CanBeNull] private Action done;
    
    private Sprite SetNewAnimation(Frame[] frames, bool loop, [CanBeNull] Action onLoopOrFinish) {
        currFrames = frames;
        currFrameIndex = 0;
        currTime = 0f;
        doLoop = loop;
        done = onLoopOrFinish;
        if (frames.Length == 0) return null; //TODO
        return frames[0].sprite;
    }
    
    private static int Priority(AnimationType typ) {
        if (typ == AnimationType.Attack) return 10;
        if (typ == AnimationType.Death) return 999;
        return 0;
    }
    private static bool HasPriority(AnimationType curr, AnimationType challenge) {
        if (curr == challenge) return true; //Same animation should not restart
        if (Priority(curr) > Priority(challenge)) return true;
        return false;
    }
            
    [CanBeNull]
    public Sprite SetAnimationTypeIfPriority(AnimationType typ, Frame[] frames, bool loop, [CanBeNull] Action onLoopOrFinish) {
        return HasPriority(currType, typ) ? null : SetAnimationType(typ, frames, loop, onLoopOrFinish);
    }
    public Sprite SetAnimationType(AnimationType typ, Frame[] frames, bool loop, [CanBeNull] Action onLoopOrFinish) {
        currType = typ;
        return SetNewAnimation(frames, loop, onLoopOrFinish);
    }
    
    //[CanBeNull]: the sprite may be null
    public (bool resetMe, Sprite updateSprite) Update(float dT) {
        currTime += dT;
        bool didUpdate = false;
        while (currTime >= currFrames[currFrameIndex].time) {
            currTime -= currFrames[currFrameIndex].time;
            didUpdate = true;
            if (++currFrameIndex == currFrames.Length) {
                done?.Invoke();
                if (doLoop) {
                    currFrameIndex = 0;
                    while (currFrames[currFrameIndex].skipLoop) ++currFrameIndex;
                } else return (true, null);
            }
        }
        return (false, didUpdate ? currFrames[currFrameIndex].sprite : null);
    }
}
}

public readonly struct Maybe<T> where T : class {
    [CanBeNull] public readonly T ValueOrNull;
    public bool IsValue => ValueOrNull != null;
    public T ValueOrThrow => ValueOrNull ?? throw new Exception("Maybe is null");
    public Maybe([CanBeNull] T obj) => ValueOrNull = obj;
    public static implicit operator Maybe<T>([CanBeNull] T obj) => new Maybe<T>(obj);
}

[Serializable]
public struct float2 {
    public float var1;
    public float var2;
}
[Serializable]
public struct Color2 {
    public Color color1;
    public Color color2;
}

[Serializable]
public struct Version {
    public int major;
    public int minor;
    public int patch;

    public Version(int maj, int min, int ptch) {
        major = maj;
        minor = min;
        patch = ptch;
    }
    
    public override string ToString() => $"v{major}.{minor}.{patch}";

    private (int, int, int) Tuple => (major, minor, patch);

    public static bool operator ==(Version b1, Version b2) => b1.Tuple == b2.Tuple;

    public static bool operator !=(Version b1, Version b2) => b1.Tuple != b2.Tuple;

    public override int GetHashCode() => Tuple.GetHashCode();
    public override bool Equals(object o) => o is Version v && this == v;
}


//I like the idea of level-based cancellation but there's currently not really a use case for it. 
public class CancelException : Exception {
    public CancelException() : base() { }
    public CancelException(string message) : base(message) { }
    public CancelException(string message, Exception inner) : base(message, inner) { }
}

public enum CancelLevel: int {
    None = 0,
    Operation = 100,
    Scene = 1000
}

public static class CancelHelpers {
    public static void ThrowIfCancelled(this ICancellee c) {
        if (c.Cancelled) throw new OperationCanceledException();
    }
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

    public PassthroughCancellee([CanBeNull] ICancellee root, [CanBeNull] ICancellee local) {
        this.root = root?.Joinable ?? Cancellable.Null;
        this.local = local ?? Cancellable.Null;
    }
    public bool Cancelled => root.Cancelled || local.Cancelled;
}
public class JointCancellee : ICancellee {
    private readonly ICancellee c1;
    private readonly ICancellee c2;
    public ICancellee Joinable => this;
    public JointCancellee([CanBeNull] ICancellee c1, [CanBeNull] ICancellee c2) {
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
    private T obj;
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