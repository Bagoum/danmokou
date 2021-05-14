using System;
using UnityEngine;

namespace Danmokou.Core {
[Serializable]
public struct LocatorStrategy {
    [Serializable]
    public abstract class LocatorConfig {
        public Vector2 offset;

        public Vector2 Locate(Vector2 source, Vector2 target, float targetRadius) =>
            offset + _Locate(source, target, targetRadius);

        public abstract Vector2 _Locate(Vector2 source, Vector2 target, float targetRadius);
    }

    public enum Strategy {
        Source,
        Target,
        Perimeter
    }

    public Strategy type;
    public SourceConfig Source;
    public TargetConfig Target;
    public PerimeterConfig Perimeter;

    public Vector2 Locate(Vector2 source, Vector2 target, float targetRadius) =>
        type switch {
            Strategy.Source => Source.Locate(source, target, targetRadius),
            Strategy.Target => Target.Locate(source, target, targetRadius),
            Strategy.Perimeter => Perimeter.Locate(source, target, targetRadius),
            _ => Vector2.zero
        };

    [Serializable]
    public class SourceConfig : LocatorConfig {
        public override Vector2 _Locate(Vector2 source, Vector2 target, float targetRadius) {
            return source;
        }
    }

    [Serializable]
    public class TargetConfig : LocatorConfig {
        public override Vector2 _Locate(Vector2 source, Vector2 target, float targetRadius) {
            return target;
        }
    }

    [Serializable]
    public class PerimeterConfig : LocatorConfig {
        public float multiplier = 1f;

        public override Vector2 _Locate(Vector2 source, Vector2 target, float targetRadius) {
            return target + (source - target).normalized * (targetRadius * multiplier);
        }
    }
}
}