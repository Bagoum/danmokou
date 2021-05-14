using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Danmokou.Achievements {

public interface IGameAchievementsProvider {
    public abstract AchievementRepo MakeRepo();
}

/// <summary>
/// Game-specific subclasses of this return subclasses of AchievementRepo. Otherwise, this is empty.
/// </summary>
public abstract class AchievementProviderSO : ScriptableObject, IGameAchievementsProvider {
    public abstract AchievementRepo MakeRepo();
}

}