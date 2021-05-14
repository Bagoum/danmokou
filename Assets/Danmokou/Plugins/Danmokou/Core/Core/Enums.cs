using System;
using UnityEngine;

namespace Danmokou.Core {
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
    public static Vector2 Direction(this LRUD d) =>
        d switch {
            LRUD.UP => Vector2.up,
            LRUD.DOWN => Vector2.down,
            LRUD.LEFT => Vector2.left,
            _ => Vector2.right
        };
}
}
