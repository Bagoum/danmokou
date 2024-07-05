using Newtonsoft.Json;

namespace MiniProjects.PJ24 {
/// <summary>
/// An type of effect, created when certain conditions are satisfied during crafting.
/// <br/>Effect *types* are general. Individual items have <see cref="EffectInstance"/>.
/// </summary>
[JsonConverter(typeof(SingletonConverter<Effect>))]
public abstract class Effect {
    public string Name { get; }

    private Effect(string? name = null) {
        Name = name ?? Alchemy.DefaultName(GetType());
    }

    public override string ToString() => $"Effect {Name}";

    public class Fragile : Effect {
        private Fragile() { }
        public static Fragile S { get; } = new();
    }
    public class Antifragile : Effect {
        private Antifragile() { }
        public static Antifragile S { get; } = new();
    }

    public class FullOfMicroplastics : Effect {
        private FullOfMicroplastics() : base("Full of Microplastics") { }
        public static FullOfMicroplastics S { get; } = new();
    }
    public class TraditionallyWoven : Effect {
        private TraditionallyWoven() : base("Traditionally-Woven") { }
        public static TraditionallyWoven S { get; } = new();
    }
    public class ThreeColor : Effect {
        private ThreeColor() : base("Three-Colored") { }
        public static ThreeColor S { get; } = new();
    }
    public class LoveColor : Effect {
        private LoveColor() : base("Love-Colored") { }
        public static LoveColor S { get; } = new();
    }
}

/// <summary>
/// A score requirement on a recipe ingredient that results in an effect.
/// </summary>
/// <param name="MinScore">Minimum score to trigger the effect (inclusive)</param>
/// <param name="MaxScore">Maximum score to trigger the effect (exclusive)</param>
/// <param name="Result">Resultant effect</param>
public record EffectRequirement(int? MinScore, int? MaxScore, Effect Result) {
    public EffectInstance? Evaluate(int score) {
        if (score < MinScore) return null;
        if (score >= MaxScore) return null;
        return new(Result);
    }
}

/// <summary>
/// An instance of an <see cref="Effect"/>, applied to an item.
/// </summary>
public class EffectInstance {
    public Effect Type { get; }

    public EffectInstance(Effect type) {
        Type = type;
    }
}


}