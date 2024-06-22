using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniProjects.PJ24 {
public record Recipe(Item Result, decimal Time, params RecipeComponent[] Components) {
    public EffectRequirement[]?[]? Effects { get; init; }

    public int DaysTaken(int count) => (int)Math.Ceiling(Time * count);

    public bool HasEffects(int index) => Effects?[index]?.Length > 0;
    
    /// <summary>
    /// Determine the effects that will be applied to the final result.
    /// <br/>Can be called with incomplete inputs.
    /// </summary>
    public List<EffectInstance> DetermineEffects(List<ItemInstance>[] inputs) {
        var outp = new List<EffectInstance>();
        for (int ii = 0; ii < Effects?.Length; ++ii)
            if (DetermineEffects(inputs[ii], ii, out _) is {} res)
                outp.AddRange(res);
        return outp;
    }

    /// <summary>
    /// Determine the effects that will be applied from the `index`th recipe component.
    /// <br/>Can be called with incomplete inputs.
    /// </summary>
    public List<EffectInstance>? DetermineEffects(List<ItemInstance> inputs, int index, out int score) {
        List<EffectInstance>? outp = null;
        score = 0;
        if (Effects?[index] is { } reqs && inputs.Count > 0) {
            if (Components[index] is not RecipeComponent.ItemCategory cat)
                throw new Exception("Scoring for fixed item types not yet supported");
            score = (int) Math.Round(inputs.Average(x => x.Type.CategoryScore(cat.Category)) ?? 0);
            foreach (var req in reqs) {
                if (req.Evaluate(score) is { } eff)
                    (outp ??= new()).Add(eff);
            }
        }
        return outp;
    }

    public ItemInstance Synthesize(List<ItemInstance>[] inputs) {
        return new ItemInstance(Result, DetermineEffects(inputs), Alchemy.CombineTraits(inputs));
    }

    public override string ToString() => $"{Result.Name} Recipe";
}

public abstract record RecipeComponent {
    public int Count { get; init; } = 1;
    public virtual bool? MatchesType(Item item) => null;
    public virtual bool Matches(ItemInstance item) => 
        MatchesType(item.Type) ?? throw new Exception($"Recipe component {this} does not define " +
                                                      $"instance matching logic for {item}");
    public abstract string Describe();

    public record ItemType(Item Item) : RecipeComponent {
        public override bool? MatchesType(Item item) => 
            item == Item;

        public override string Describe() => Item.Name;
    }

    public record ItemCategory(Category Category) : RecipeComponent {
        public override bool? MatchesType(Item item) => 
            item.CategoryScore(Category) is not null;

        public override string Describe() => Category.Print();
    }

    public static implicit operator RecipeComponent(Item item) => new ItemType(item);
    public static implicit operator RecipeComponent((Item, int) item) => 
        new ItemType(item.Item1) { Count = item.Item2 };
    public static implicit operator RecipeComponent(Category cat) => new ItemCategory(cat);
    public static implicit operator RecipeComponent((Category, int) cat) => 
        new ItemCategory(cat.Item1) { Count = cat.Item2 };
}



}