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
    EN
}
public enum ShootDirection {
    LEFT,
    RIGHT,
    UP,
    DOWN
}


namespace Danmaku {
public enum DifficultySet {
    Easier,
    Easy,
    Normal,
    Hard,
    Lunatic,
    Ultra,
    Abex,
    Assembly,
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
public enum CampaignMode {
    MAIN,
    STAGE_PRACTICE,
    CARD_PRACTICE,
    TUTORIAL
}

public enum Layer {
    LowProjectile,
    HighProjectile
}

public static class EnumHelpers {
    public static int Int(this Layer l) {
        if (l == Layer.LowProjectile) return LayerMask.NameToLayer("LowProjectile");
        else  return LayerMask.NameToLayer("HighProjectile");
    }
    public static Vector2 Direction(this LRUD d) {
        if (d == LRUD.UP) return Vector2.up;
        else if (d == LRUD.DOWN) return Vector2.down;
        else if (d == LRUD.LEFT) return Vector2.left;
        return Vector2.right;
    }
    public static float Value(this DifficultySet d) {
        if (d == DifficultySet.Easier) return 0.7579f;      // 2^-0.4
        else if (d == DifficultySet.Easy) return 1f;        // 2^0.0
        else if (d == DifficultySet.Normal) return 1.3195f; // 2^0.4
        else if (d == DifficultySet.Hard) return 1.741f;    // 2^0.8
        else if (d == DifficultySet.Lunatic) return 2.297f; // 2^1.2
        else if (d == DifficultySet.Ultra) return 3.031f;   // 2^1.6
        else if (d == DifficultySet.Abex) return 4f;        // 2^2.0
        else if (d == DifficultySet.Assembly) return 4.925f;// 2^2.3
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }
    public static float Counter(this DifficultySet d) {
        if (d == DifficultySet.Easier) return 0;
        else if (d == DifficultySet.Easy) return 1;
        else if (d == DifficultySet.Normal) return 2;
        else if (d == DifficultySet.Hard) return 3;
        else if (d == DifficultySet.Lunatic) return 4;
        else if (d == DifficultySet.Ultra) return 5;
        else if (d == DifficultySet.Abex) return 6;
        else if (d == DifficultySet.Assembly) return 7;
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }

    public static string Describe(this DifficultySet d) {
        if (d == DifficultySet.Easier) return "Easier";
        else if (d == DifficultySet.Easy) return "Easy";
        else if (d == DifficultySet.Normal) return "Normal";
        else if (d == DifficultySet.Hard) return "Hard";
        else if (d == DifficultySet.Lunatic) return "Lunatic";
        else if (d == DifficultySet.Ultra) return "Ultra";
        else if (d == DifficultySet.Abex) return "Abex";
        else if (d == DifficultySet.Assembly) return "Assembly";
        throw new Exception($"Couldn't resolve difficulty setting {d}");
    }
}
}