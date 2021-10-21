using System;
using JetBrains.Annotations;
using ProtoBuf;
using UnityEngine;

namespace Danmokou.Core {
//Inspector-exposed structs cannot be readonly

[Serializable]
public struct FieldBounds {
    // Default LR is +-5, scene game is +-4, and Touhou equivalent is +-3.62
    public float left;
    public float right;
    public float top;
    public float bot;
    //Eg. set this to (-1, 0) for a traditional Touhou "left-leaning" play area.
    public Vector2 center;
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
    private Action? done;
    
    private Sprite? SetNewAnimation(Frame[] frames, bool loop, Action? onLoopOrFinish) {
        if (frames.Length == 0) {
            done?.Invoke();
            return null;
        }
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
            
    public Sprite? SetAnimationTypeIfPriority(AnimationType typ, Frame[] frames, bool loop, Action? onLoopOrFinish) {
        return HasPriority(currType, typ) ? null : SetAnimationType(typ, frames, loop, onLoopOrFinish);
    }
    public Sprite? SetAnimationType(AnimationType typ, Frame[] frames, bool loop, Action? onLoopOrFinish) {
        currType = typ;
        return SetNewAnimation(frames, loop, onLoopOrFinish);
    }
    
    public (bool resetMe, Sprite? updateSprite) Update(float dT) {
        currTime += dT;
        bool didUpdate = false;
        if (currFrameIndex >= currFrames.Length) {
            var srname = currFrames.Length > 0 ? currFrames[0].sprite.name : "";
            Logs.UnityError($"Ran past the end of a {currType} update loop with {currFrames.Length} frames. " +
                           $"0th frame name: {srname} ");
        }
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

public struct AABB {
    public float x;
    public float y;
    public float rx;
    public float ry;

    public AABB(float minX, float maxX, float minY, float maxY, Vector2? offset = null) {
        var off = offset ?? Vector2.zero;
        x = off.x + (minX + maxX) / 2f;
        y = off.y + (minY + maxY) / 2f;
        rx = (maxX - minX) / 2f;
        ry = (maxY - minY) / 2f;
    }

    public AABB(Vector2 center, Vector2 radius) {
        x = center.x;
        y = center.y;
        rx = radius.x;
        ry = radius.y;
    }
}
[Serializable]
public struct Color2 {
    public Color color1;
    public Color color2;
}

[Serializable]
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
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
