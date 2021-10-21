using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Core;

namespace Danmokou.Achievements {

public class AchievementManager {
    //Each achievement is only created once. Requirements are constructed as treelike dependencies from each achievement;
    // requirements shared between achievements are created once for each such achievement.
    // Requirement duplication is more convenient from the perspective of defining requirements in RequirementsRepo.
    public Achievement[] Achievements { get; }
    
    //Prefer completed achievements, and then order by the original ordering
    public IEnumerable<Achievement> SortedAchievements =>
        Achievements
            .Select((x, i) => (x, i))
            .OrderByDescending(((Achievement a, int ind) x) => (x.a.State, -x.ind))
            .Select(x => x.Item1);
    private Dictionary<string, Achievement> AchievementsByKey { get; } = new Dictionary<string, Achievement>();
    public Achievement FindByKey(string key) => AchievementsByKey[key];

    public AchievementManager(AchievementRepo repo) {
        Achievements = repo.MakeAchievements().Select(a => AchievementsByKey[a.Key] = a).ToArray();
    }

    public void UpdateAll() {
        foreach (var a in Achievements)
            a.RequirementUpdated();
    }
}

/// <summary>
/// Game-specific implementations of this class construct all achievments.
/// </summary>
public abstract class AchievementRepo {

    protected virtual string LocalizationPrefix => "";
    
    protected Achievement L(string key, Func<Requirement> req) => new Achievement(key, 
        LocalizedStrings.FindReference($"{LocalizationPrefix}.{key}"),
        LocalizedStrings.FindReference($"{LocalizationPrefix}.d_{key}"),
        req, this);

    public abstract IEnumerable<Achievement> MakeAchievements();
    public abstract State? SavedAchievementState(string key);

    public AchievementManager Construct() => new AchievementManager(this);
}

}