using System.Reactive;
using BagoumLib;
using BagoumLib.Events;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling life items and extends from life items.
/// </summary>
public interface ILifeItemFeature : IInstanceFeature {
    public Evented<int> LifeItems { get; }
    public void AddLifeItems(int delta);
    public int NextLifeItems { get; }
}

public class LifeItemFeature : BaseInstanceFeature, ILifeItemFeature {
    public static readonly int[] pointLives = {
        69,
        141,
        224,
        314,
        420,
        618,
        840,
        1084,
        1337,
        1618,
        2048,
        2718,
        3142,
        9001,
        int.MaxValue
    };
    private InstanceData Inst { get; }
    public int NextLifeItems => pointLives.Try(nextItemLifeIndex, 9001);
    public Evented<int> LifeItems { get; } = new(0);
    private int nextItemLifeIndex;

    public LifeItemFeature(InstanceData inst) {
        Inst = inst;
    }
    
    public void AddLifeItems(int delta) {
        LifeItems.Value += delta;
        if (nextItemLifeIndex < pointLives.Length && LifeItems >= pointLives[nextItemLifeIndex]) {
            ++nextItemLifeIndex;
            Inst.BasicF.LifeExtend(ExtendType.LIFE_ITEM);
        }
        foreach (var f in Inst.Features)
            f.OnItemLife(delta);
    }

    public void OnContinueOrCheckpoint() {
        nextItemLifeIndex = 0;
        LifeItems.Value = 0;
    }
}

public class LifeItemExtendFeatureCreator : IFeatureCreator<ILifeItemFeature> {
    public ILifeItemFeature Create(InstanceData instance) => new LifeItemFeature(instance);
}

}