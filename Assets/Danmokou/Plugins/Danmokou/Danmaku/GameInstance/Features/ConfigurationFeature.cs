using System;
using System.Reactive;
using BagoumLib.Events;
using Danmokou.Core;
using Danmokou.Danmaku;

namespace Danmokou.GameInstance {
/// <summary>
/// A feature for handling basic configurations required for a danmaku game, such as the respawn method
///  and the location of the Point-of-Collection.
/// </summary>
public interface IConfigurationFeature : IInstanceFeature {
    /// <summary>
    /// True iff the player controller should reappear from the bottom of the screen when a life is lost.
    /// </summary>
    bool UseTraditionalRespawn { get; }
    
    /// <summary>
    /// The distance of the Point-of-Collection from the origin. By default, this is 2.
    /// </summary>
    float PoCLocation { get; }
}

public class ConfigurationFeature : BaseInstanceFeature, IConfigurationFeature {
    private InstanceData Inst { get; }

    public bool UseTraditionalRespawn { get; }
    public float PoCLocation { get; }

    public ConfigurationFeature(InstanceData inst, ConfigurationFeatureCreator c) {
        Inst = inst;
        PoCLocation = c.PoCLocation + (float)inst.Difficulty.pocOffset;
        UseTraditionalRespawn = inst.Difficulty.respawnOnDeath ?? c.TraditionalRespawn;
    }
}

public record ConfigurationFeatureCreator : IFeatureCreator<IConfigurationFeature> {
    public bool TraditionalRespawn { get; init; } = false;
    public float PoCLocation { get; init; } = 2;
    public IConfigurationFeature Create(InstanceData instance) => new ConfigurationFeature(instance, this);
}
}