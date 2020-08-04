using UnityEngine;

public static class LocatorStrategy {
    public enum Strategy {
        Source,
        Target,
        Perimeter,
        HalfPerimeter
    }

    public static bool IsPerimeter(this Strategy s) => s == Strategy.Perimeter || s == Strategy.HalfPerimeter;

    public static float RadiusMult(this Strategy s) {
        if (s == Strategy.HalfPerimeter) return 0.5f;
        return 1f;
    }
    private static Vector2 Source(Vector2 source, Vector2 target, float targetRadius) {
        return source;
    }
    private static Vector2 Target(Vector2 source, Vector2 target, float targetRadius) {
        return target;
    }
    private static Vector2 OnPerimeter(Vector2 source, Vector2 target, float targetRadius) {
        return target + (source - target).normalized * targetRadius;
    }

    public static Vector2 Locate(Strategy s, Vector2 source, Vector2 target, float targetRadius) {
        if (s == Strategy.Source) {
            return Source(source, target, targetRadius);
        } else if (s == Strategy.Target) {
            return Target(source, target, targetRadius);
        } else if (s.IsPerimeter()) {
            return OnPerimeter(source, target, targetRadius * s.RadiusMult());
        }
        return source;
    }

}
