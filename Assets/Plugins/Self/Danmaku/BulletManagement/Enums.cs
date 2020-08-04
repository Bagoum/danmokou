namespace Danmaku {
public static class Enums {
    /// <summary>
    /// Handling for setting the firing index. The generic repeaters use DEFER by default.
    /// </summary>
    public enum Parametrization {
        /// <summary>
        /// Use this function's indexing.
        /// </summary>
        THIS = 0,
        /// <summary>
        /// Use the parent function's indexing.
        /// </summary>
        DEFER = 1,
        /// <summary>
        /// Combine the indexing. Use p1 (parent) p2 (this) in FXY functions.
        /// </summary>
        ADDITIVE = 2,
        /// <summary>
        /// Combine the indexing around the repeat number of the child function.
        /// Use p1m {RPT} p2m {RPT} in FXY functions.
        /// </summary>
        MOD = 3,
        /// <summary>
        /// Combine the indexing around the repeat number of the child function.
        /// The ordering is inverted, so the first summon has local index RPT-1 and the last has local index 0.
        /// Use p1m {RPT} p2m {RPT} in FXY functions.
        /// </summary>
        INVMOD = 4
    }
    
    /// <summary>
    /// Handling for setting the angle when using an SAOffset command.
    /// </summary>
    public enum SAAngle {
        /// <summary>
        /// (o) The angle from the parent is not modified.
        /// If you add an angle offset, the entire summon will rotate.
        /// </summary>
        ORIGINAL,
        /// <summary>
        /// (bo) The angle from the parent is banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        ORIGINAL_BANK,
        /// <summary>
        /// (br) The angle is set to the position of the summon, then banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        REL_ORIGIN_BANK,
        /// <summary>
        /// (bt) The angle is set to the tangent on the summon curve, then banked.
        /// If you add an angle offset, the summon will not rotate,
        /// but the movement of the summoned pieces will.
        /// </summary>
        TANGENT_BANK
    }
    /// <summary>
    /// Enum to describe an operation that may optionally block.
    /// </summary>
    public enum Blocking {
        /// <summary>
        /// The operation is blocking and must finish before the next operation.
        /// </summary>
        BLOCKING,
        /// <summary>
        /// The operation is nonblocking; the next operation may begin immediately.
        /// </summary>
        NONBLOCKING
    }

    /// <summary>
    /// Enum to describe an event that may trigger once or trigger persistently.
    /// </summary>
    public enum Persistence {
        /// <summary>
        /// The event is persistent and must be manually destroyed.
        /// </summary>
        PERSISTENT,
        /// <summary>
        /// The event will trigger once and destroy itself.
        /// </summary>
        ONCE
    }
    
    
    /// <summary>
    /// Enum that describe components of a V2RV2 location.
    /// Used for targeting.
    /// </summary>
    public enum RV2ControlMethod {
        /// <summary>
        /// (nx) Nonrotational x
        /// </summary>
        NX,
        /// <summary>
        /// (ny) Nonrotational y
        /// </summary>
        NY,
        /// <summary>
        /// (rx) Rotational x
        /// </summary>
        RX,
        /// <summary>
        /// (ry) Rotational y
        /// </summary>
        RY,
        /// <summary>
        /// (a) Angle. When targeted, rotates the entire summon.
        /// </summary>
        ANG,
        /// <summary>
        /// (ra) Angle. When targeted, only adds to the RV2 angle.
        /// </summary>
        RANG
    }
}
}