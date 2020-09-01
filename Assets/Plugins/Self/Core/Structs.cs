using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Danmaku {
//Inspector-exposed structs cannot be readonly
[Serializable]
public struct MovementModifiers {
    public bool flipX;
    public bool flipY;
    public sbyte FlipX => flipX ? (sbyte)-1 : (sbyte)1;
    public sbyte FlipY => flipY ? (sbyte)-1 : (sbyte)1;

    public static MovementModifiers Default => new MovementModifiers(false, false);
    public static MovementModifiers WithXFlip => new MovementModifiers(true, false);
    public MovementModifiers(bool x, bool y) {
        flipX = x;
        flipY = y;
    }
    public MovementModifiers ApplyOver(MovementModifiers basem) {
        return new MovementModifiers(flipX ^ basem.flipX, flipY ^ basem.flipY);
    }

    public Vector2 ApplyOver(Vector2 basev) {
        basev.x *= FlipX;
        basev.y *= FlipY;
        return basev;
    }
}

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
public class CancelSource {
    private CancelLevel level = CancelLevel.None;

    public void Cancel() => Cancel(CancelLevel.Operation);

    public void Cancel(CancelLevel toLevel) {
        if (level == CancelLevel.None) level = toLevel;
        else throw new CancelException("Source has already been cancelled");
    }

    public class SourcedCancelToken : CancelToken {
        private readonly CancelSource src;
        public SourcedCancelToken(CancelSource src) {
            this.src = src;
        }
        public override CancelLevel CancelLevel => src.level;
    }
}

public class JointCancelToken : CancelToken {
    private readonly CancelToken c1;
    private readonly CancelToken c2;
    public JointCancelToken(CancelToken c1, CancelToken c2) {
        this.c1 = c1;
        this.c2 = c2;
    }
    public override CancelLevel CancelLevel { get {
            var c1t = c1.CancelLevel;
            var c2t = c2.CancelLevel;
            return (c1t > c2t) ? c1t : c2t;
        }
    }
}
//This should be a trait... rip c#8
public abstract class CancelToken {
    public abstract CancelLevel CancelLevel { get; }
    public bool Cancelled => CancelLevel > CancelLevel.None;
    
    public void ThrowIfCancelled() {
        if (Cancelled) throw new OperationCanceledException();
    }
    
    public CancelToken JoinWith(CancelToken other) => new JointCancelToken(this, other);
}