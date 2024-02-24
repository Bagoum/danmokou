namespace Danmokou.GameInstance {
/// <summary>
/// An interface for any game-specific custom logic.
/// </summary>
public interface ICustomDataFeature : IInstanceFeature { }

public class NoCustomDataFeature : BaseInstanceFeature, ICustomDataFeature { }

public class NoCustomDataFeatureCreator : IFeatureCreator<ICustomDataFeature> {
    public ICustomDataFeature Create(InstanceData instance) => new NoCustomDataFeature();
}

}