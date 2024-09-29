using System;
using UnityEngine;

namespace Danmokou.Core {
/// <summary>
/// Standard rendering modes for bullets.
/// </summary>
public enum DRenderMode {
    NORMAL,
    ADDITIVE,
    NEGATIVE,
    SOFT_ADDITIVE
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
    public static Vector2 Direction(this LRUD d) => d switch {
            LRUD.UP => Vector2.up,
            LRUD.DOWN => Vector2.down,
            LRUD.LEFT => Vector2.left,
            _ => Vector2.right
        };

    public static LRUD RotateCCW(this LRUD d) => d switch {
        LRUD.UP => LRUD.LEFT,
        LRUD.LEFT => LRUD.DOWN,
        LRUD.DOWN => LRUD.RIGHT,
        _ => LRUD.UP
    };
    
    public static LRUD RotateCW(this LRUD d) => d switch {
        LRUD.DOWN => LRUD.LEFT,
        LRUD.LEFT => LRUD.UP,
        LRUD.UP => LRUD.RIGHT,
        _ => LRUD.DOWN
    };
    
    public static LRUD Flip(this LRUD d) => d switch {
        LRUD.UP => LRUD.DOWN,
        LRUD.LEFT => LRUD.RIGHT,
        LRUD.DOWN => LRUD.UP,
        _ => LRUD.DOWN
    };

    public static float CCWRotation(this LRUD d) => d switch {
        LRUD.UP => 90,
        LRUD.LEFT => 180,
        LRUD.DOWN => 270,
        _ => 0
    };
    public static float CWRotation(this LRUD d) => d switch {
        LRUD.UP => 270,
        LRUD.LEFT => 180,
        LRUD.DOWN => 90,
        _ => 0
    };
    
    public static bool IsLR(this LRUD d) => d is LRUD.LEFT or LRUD.RIGHT;
    public static bool IsUD(this LRUD d) => d is LRUD.UP or LRUD.DOWN;
}
}
