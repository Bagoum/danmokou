using System.Text;
using BagoumLib.Reflection;
using Newtonsoft.Json;

namespace MiniProjects.PJ24 {
[JsonConverter(typeof(SingletonSerializer<Trait>))]
public abstract class Trait {
    public string Name { get; }

    private Trait(string? name = null) {
        Name = name ?? Alchemy.DefaultName(GetType());
    }
    public virtual Trait? TryMergeWith(Trait other) => null;

    public override string ToString() => $"Trait {Name}";

    public class Glowing1 : Trait {
        private Glowing1() { }
        public static Glowing1 S { get; } = new();
        public override Trait? TryMergeWith(Trait other) =>
            other is Glowing2 ? Glowing3.S : null;
    }
    public class Glowing2: Trait { 
        private Glowing2() { }
        public static Glowing2 S { get; } = new();
    }

    public class Glowing3 : Trait {
        private Glowing3() { }
        public static Glowing3 S { get; } = new();
    }
    
    public class Sweet : Trait { 
        private Sweet() { }
        public static Sweet S { get; } = new();
        
        public override Trait? TryMergeWith(Trait other) =>
            other is Sour ? SweetAndSour.S : null;
    }
    public class Sour : Trait {
        private Sour() { }
        public static Sour S { get; } = new();
        
    }

    public class SweetAndSour : Trait {
        private SweetAndSour() : base("Sweet and Sour") { }
        public static SweetAndSour S { get; } = new();
    }
    public class Sturdy : Trait { 
        private Sturdy() { }
        public static Sturdy S { get; } = new();
        
    }
    public class Flexible : Trait {
        private Flexible() { }
        public static Flexible S { get; } = new();
        
    }
    public class Illuminating : Trait { 
        private Illuminating() { }
        public static Illuminating S { get; } = new();
        
    }
    public class TastesLikeCoke : Trait {
        private TastesLikeCoke() : base("Tastes like Coke") { }
        public static TastesLikeCoke S { get; } = new();
    }
    public class EfficientFlow : Trait {
        private EfficientFlow() { }
        public static EfficientFlow S { get; } = new();
    }
    public class ResistsFire : Trait {
        private ResistsFire() { }
        public static ResistsFire S { get; } = new();
    }
    public class ResistsLightning : Trait {
        private ResistsLightning() { }
        public static ResistsLightning S { get; } = new();
    }
}

public record TraitInstance(Trait Type, (TraitInstance, TraitInstance)? SynthedFrom = null) { }
}