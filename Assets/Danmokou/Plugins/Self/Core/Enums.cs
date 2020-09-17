using System;
using UnityEngine;

public enum RenderMode {
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

public enum LR {
    LEFT,
    RIGHT
}
public enum LRUD {
    LEFT,
    RIGHT,
    UP,
    DOWN
}

public enum SeijaMethod {
    X,
    Y
}

public enum Locale {
    EN,
    JP
}
public enum ShootDirection: byte {
    INHERIT,
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

public static class EnumHelpers {
    public static Vector2 Direction(this LRUD d) {
        if (d == LRUD.UP) return Vector2.up;
        else if (d == LRUD.DOWN) return Vector2.down;
        else if (d == LRUD.LEFT) return Vector2.left;
        return Vector2.right;
    }

}