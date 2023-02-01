using Danmokou.Core;

namespace Danmokou.GameInstance {
/// <summary>
/// A container for all <see cref="IFeatureCreator{T}"/>s required to construct a game instance.
/// </summary>
public record InstanceFeatures {
    public IFeatureCreator<IBasicFeature> Basic { get; init; } = new BasicFeatureCreator();
    public IFeatureCreator<IConfigurationFeature> Configuration { get; init; } = new ConfigurationFeatureCreator();
    public IFeatureCreator<IScoreFeature> Score { get; init; } = new ScoreFeatureCreator(null);
    //Power is disabled by default
    public IFeatureCreator<IPowerFeature> Power { get; init; } = new DisabledPowerFeatureCreator();
    public IFeatureCreator<ILifeItemFeature> ItemExt { get; init; } = new LifeItemExtendFeatureCreator();
    public IFeatureCreator<IScoreExtendFeature> ScoreExt { get; init; } = new ScoreExtendFeatureCreator();
    //Rank is disabled by default
    public IFeatureCreator<IRankFeature> Rank { get; init; } = new DisabledRankFeatureCreator();
    public IFeatureCreator<IFaithFeature> Faith { get; init; } = new FaithFeatureCreator();

    public IFeatureCreator<IMeterFeature> Meter { get; init; } = new MeterFeatureCreator();

    public static readonly InstanceFeatures ShotDemoFeatures = new() {
        Power = new DisabledPowerFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        Faith = new DisabledFaithFeatureCreator(),
        Meter = new DisabledMeterFeatureCreator()
    };
    public static readonly InstanceFeatures InactiveFeatures = new() {
        Power = new DisabledPowerFeatureCreator(),
        Rank = new DisabledRankFeatureCreator(),
        Faith = new DisabledFaithFeatureCreator(),
        Meter = new DisabledMeterFeatureCreator()
    };
}
}