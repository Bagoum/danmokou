using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Events;
using Danmokou.UI.XML;
using Newtonsoft.Json;

namespace MiniProjects.PJ24 {
[JsonConverter(typeof(SingletonConverter<Item>))]
public abstract class Item {
    public string Name { get; }
    public Recipe? Recipe { get; protected init; }

    public (Category category, int score)[] Categories => Alchemy.Categories
        .SelectNotNull(cat => this.CategoryScore(cat) is {} score ? (cat, score) : default((Category, int)?))
        .ToArray();
    
    /// <summary>
    /// Returns the score (generally 0-100) this item
    ///  provides for the given category.
    /// <br/>If the ingredient is not in the given category, return null.
    /// </summary>
    public virtual int? CategoryScore(Category c) => null;

    private static Item[]? _items = null;
    /// <summary>
    /// All items, listed in the order they should be displayed in the inventory or crafting menu.
    /// </summary>
    public static Item[] Items => _items ??= new Item[] {
        //raw
        Water.S,
        Alcohol.S,
        Salt.S,
        Blackberry.S,
        BagOfNuts.S,
        FlaxFiber.S,
        MagnoliaBloom.S,
        RainbowRose.S,
        RawMeat.S,
        DragonScale.S,
        DragonHorn.S,
        Red40.S,
        PetrochemicalWaste.S,
        Ingot.S,
        CrystallizedFantasy.S,
        ScatteringOpal.S,
        SoulShell.S,
        //craftable
        TritricolorBanner.S,
        MoldablePlastic.S,
        LinenCloth.S,
        DragonscaleCloth.S,
        Rope.S,
        SteelYarn.S,
        DragonHarness.S,
        NutOil.S,
        RainbowDye.S,
        RainbowPaintSet.S,
        EnchantedOil.S,
        OilLamp.S,
        RefractiveMagicFuel.S,
        ProcessedMeat.S,
        BoneBuildingJuice.S,
        MedicalSolution.S,
        Astringent.S,
        EyeBleach.S,
        LiteralPoison.S,
        BoneHurtingJuice.S,
        SoulChain.S,
        SoulGuard.S,
        GrayDragonStatue.S
    };

    private static Dictionary<string, Item>? _nameDict;

    public static Dictionary<string, Item> NameDict => _nameDict ??= Items.ToDictionary(x => x.Name);

    public static Item FindByName(string name) {
        if (NameDict.TryGetValue(name, out var x))
            return x;
        throw new Exception($"No item by name {name}");
    }
    
    private Item(string? name = null) {
        Name = name ?? Alchemy.DefaultName(GetType());
    }

    public override string ToString() => 
        Recipe is null ? $"Ingredient {Name}" : $"Craftable {Name}";
    
    //--- raw ingredients
    public class MagnoliaBloom : Item {
        private MagnoliaBloom() { }
        public static MagnoliaBloom S { get; } = new();
    }

    public class PetrochemicalWaste : Item {
        private PetrochemicalWaste() { }
        public static PetrochemicalWaste S { get; } = new();
    }
    public class Water : Item {
        private Water() { }
        public static Water S { get; } = new();
    }
    public class Alcohol : Item {
        private Alcohol() { }
        public static Alcohol S { get; } = new();
    }
    public class FlaxFiber : Item {
        private FlaxFiber() { }
        public static FlaxFiber S { get; } = new();
    }
    public class Red40 : Item {
        private Red40() { }
        public static Red40 S { get; } = new();
    }
    public class Blackberry : Item {
        private Blackberry() { }
        public static Blackberry S { get; } = new();
    }
    public class BagOfNuts : Item {
        private BagOfNuts() : base("Bag of Nuts") { }
        public static BagOfNuts S { get; } = new();
    }
    public class Salt : Item {
        private Salt() { }
        public static Salt S { get; } = new();
    }
    public class Ingot : Item {
        private Ingot() { }
        public static Ingot S { get; } = new();
    }
    public class RainbowRose : Item {
        private RainbowRose() { }
        public static RainbowRose S { get; } = new();
    }
    public class RawMeat : Item {
        private RawMeat() { }
        public static RawMeat S { get; } = new();
    }
    public class CrystallizedFantasy : Item {
        private CrystallizedFantasy() { }
        public static CrystallizedFantasy S { get; } = new();
    }
    public class ScatteringOpal : Item {
        private ScatteringOpal() { }
        public static ScatteringOpal S { get; } = new();
    }
    public class DragonScale : Item {
        private DragonScale() { }
        public static DragonScale S { get; } = new();
    }
    public class DragonHorn : Item {
        private DragonHorn() { }
        public static DragonHorn S { get; } = new();
    }
    public class SoulShell : Item {
        private SoulShell() { }
        public static SoulShell S { get; } = new();
    }
    
    
    //--- synthesized

    public class MoldablePlastic : Item {
        private MoldablePlastic() {
            Recipe = new(this, 1, 0.25m, PetrochemicalWaste.S, Water.S);
        }
        public static MoldablePlastic S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.CLOTH => 30,
            _ => null
        };
    }
    public class LinenCloth : Item {
        private LinenCloth() {
            Recipe = new(this, 3, 0.5m, FlaxFiber.S, Water.S);
        }
        public static LinenCloth S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.CLOTH => 60,
            _ => null
        };
    }
    public class DragonscaleCloth : Item {
        private DragonscaleCloth() {
            Recipe = new(this, 10, 0.5m, DragonScale.S, Water.S);
        }
        public static DragonscaleCloth S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.CLOTH => 80,
            _ => null
        };
    }
    public class Rope : Item {
        private Rope() {
            Recipe = new(this, 6, 0.5m, (FlaxFiber.S, 2));
        }
        public static Rope S { get; } = new();
    }
    public class SteelYarn : Item {
        private SteelYarn() {
            Recipe = new(this, 8, 0.5m, FlaxFiber.S, Ingot.S, Water.S);
        }
        public static SteelYarn S { get; } = new();
    }
    public class NutOil : Item {
        private NutOil() {
            Recipe = new(this, 8, 0.25m, BagOfNuts.S);
        }
        public static NutOil S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.OIL => 50,
            _ => null
        };
    }
    public class RainbowDye : Item {
        private RainbowDye() {
            Recipe = new(this, 12, 0.5m, Water.S, RainbowRose.S);
        }
        public static RainbowDye S { get; } = new();
    }
    public class RainbowPaintSet : Item {
        private RainbowPaintSet() {
            Recipe = new(this, 14, 0.5m, RainbowDye.S, MoldablePlastic.S, Water.S);
        }
        public static RainbowPaintSet S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.OIL => 80,
            _ => null
        };
    }
    public class OilLamp : Item {
        private OilLamp() {
            Recipe = new(this, 19, 1m, Category.OIL, Ingot.S);
        }
        public static OilLamp S { get; } = new();
    }
    public class DragonHarness : Item {
        private DragonHarness() {
            Recipe = new(this, 22, 0.5m, (Category.CLOTH, 2), Rope.S) { Effects = new[] {
                new EffectRequirement[] {
                    new(null, 65, Effect.Fragile.S),
                    new(65, null, Effect.Antifragile.S)
                },
                null
            } };
        }
        public static DragonHarness S { get; } = new();
    }
    public class MedicalSolution : Item {
        private MedicalSolution() {
            Recipe = new(this, 13, 0.5m, Water.S, Alcohol.S);
        }
        public static MedicalSolution S { get; } = new();
    }
    public class Astringent : Item {
        private Astringent() {
            Recipe = new(this, 11, 0.5m, MedicalSolution.S, Blackberry.S);
        }
        public static Astringent S { get; } = new();
    }
    public class EyeBleach : Item {
        private EyeBleach() {
            Recipe = new(this, 26, 1m, (Astringent.S, 2), Salt.S, Category.OIL);
        }
        public static EyeBleach S { get; } = new();
    }
    public class ProcessedMeat : Item {
        private ProcessedMeat() {
            Recipe = new(this, 21, 0.3m, RawMeat.S, Salt.S);
        }
        public static ProcessedMeat S { get; } = new();
    }
    public class LiteralPoison : Item {
        private LiteralPoison() {
            Recipe = new(this, 29, 0.5m, MoldablePlastic.S, Red40.S);
        }
        public static LiteralPoison S { get; } = new();
    }
    public class BoneBuildingJuice : Item {
        private BoneBuildingJuice() {
            Recipe = new(this, 30, 0.5m, ProcessedMeat.S, Category.OIL);
        }
        public static BoneBuildingJuice S { get; } = new();
    }
    public class BoneHurtingJuice : Item {
        private BoneHurtingJuice() {
            Recipe = new(this, 32, 1m, BoneBuildingJuice.S, LiteralPoison.S);
        }
        public static BoneHurtingJuice S { get; } = new();
    }
    public class EnchantedOil : Item {
        private EnchantedOil() {
            Recipe = new(this, 35, 0.5m, Category.OIL, CrystallizedFantasy.S);
        }
        public static EnchantedOil S { get; } = new();
        public override int? CategoryScore(Category c) => c switch {
            Category.OIL => 55,
            _ => null
        };
    }
    public class SoulChain : Item {
        private SoulChain() {
            Recipe = new(this, 39, 2m, SteelYarn.S, Salt.S, LiteralPoison.S, SoulShell.S);
        }
        public static SoulChain S { get; } = new();
    }
    public class SoulGuard : Item {
        private SoulGuard() {
            Recipe = new(this, 46, 1m, SteelYarn.S, Salt.S, BoneBuildingJuice.S, SoulShell.S);
        }
        public static SoulGuard S { get; } = new();
    }
    public class RefractiveMagicFuel : Item {
        private RefractiveMagicFuel() {
            Recipe = new(this, 59, 0.5m, ScatteringOpal.S, RainbowDye.S, Category.OIL) { Effects = new[] {
                null,
                null,
                new EffectRequirement[] {
                    new(null, 60, Effect.ThreeColor.S),
                    new(60, null, Effect.LoveColor.S)
                },
            } };
        }
        public static RefractiveMagicFuel S { get; } = new();
    }
    public class TritricolorBanner : Item {
        private TritricolorBanner() {
            Recipe = new(this, 42, 1m, Category.CLOTH, RainbowDye.S, MagnoliaBloom.S) { Effects = new[] {
                new EffectRequirement[] {
                    new(null, 50, Effect.FullOfMicroplastics.S),
                    new(50, null, Effect.TraditionallyWoven.S)
                },
                null,
                null,
            } };
        }
        public static TritricolorBanner S { get; } = new();
    }
    public class GrayDragonStatue : Item {
        private GrayDragonStatue() {
            Recipe = new(this, 69, 1m, DragonHorn.S, EnchantedOil.S, RefractiveMagicFuel.S, (MoldablePlastic.S, 2));
        }
        public static GrayDragonStatue S { get; } = new();
    }
}

public enum Category {
    CLOTH,
    OIL
}

public class ItemInstance : IModelObject {
    Evented<bool> IModelObject._destroyed { get; } = new(false);
    public Item Type { get; private init; }
    public List<EffectInstance> Effects { get; private init; }
    public List<TraitInstance> Traits { get; private init; }

    public ItemInstance(Item type, List<EffectInstance>? effects = null, List<TraitInstance>? traits = null) {
        Type = type;
        Effects = effects ?? new();
        Traits = traits ?? new();
    }

    public ItemInstance Copy() => new(Type, Effects.ToList(), Traits.ToList());

    public string Describe() {
        var eff = "";
        if (Effects.Count > 0) {
            var es = Effects.Count > 1 ? "s" : "";
            eff = $" w/ effect{es} " + string.Join(", ", Effects.Select(x => x.Type.Name));
        }
        var trait = "";
        if (Traits.Count > 0) {
            var ts = Traits.Count > 1 ? "s" : "";
            trait = (eff.Length > 0 ? $"; trait{ts} " : $" w/ trait{ts} ") + 
                    string.Join(", ", Traits.Select(x => x.Type.Name));
        }
        return $"{Type.Name}{eff}{trait}";
    }
}

}