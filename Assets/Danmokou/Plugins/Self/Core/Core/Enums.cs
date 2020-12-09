using System;
using UnityEngine;

namespace DMK.Core {
/// <summary>
/// Standard rendering modes for bullets.
/// </summary>
public enum DRenderMode {
    NORMAL,
    ADDITIVE,
    NEGATIVE
}

public enum Emote {
    NORMAL,
    HAPPY,
    ANGRY,
    WORRY,
    CRY,
    SURPRISE,
    SPECIAL
}

/// <summary>
/// A direction that is either LEFT or RIGHT.
/// </summary>
public enum LR {
    LEFT,
    RIGHT
}

/// <summary>
/// A direction that is LEFT, RIGHT, UP, or DOWN.
/// </summary>
public enum LRUD {
    LEFT,
    RIGHT,
    UP,
    DOWN
}

public enum AnimationType {
    None,
    Left,
    Right,
    Up,
    Down,
    Attack,
    Death
}

public static partial class EnumHelpers {
    public static Vector2 Direction(this LRUD d) {
        switch (d) {
            case LRUD.UP:
                return Vector2.up;
            case LRUD.DOWN:
                return Vector2.down;
            case LRUD.LEFT:
                return Vector2.left;
            default:
                return Vector2.right;
        }
    }
}
}
