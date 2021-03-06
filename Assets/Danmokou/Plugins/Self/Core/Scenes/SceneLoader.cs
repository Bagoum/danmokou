﻿using DMK.Behavior;
using DMK.Core;

namespace DMK.Scenes {
/// <summary>
/// This is a singleton on the GameManagement object that runs cross-scene coroutines related to scene management.
/// </summary>
public class SceneLoader : CoroutineRegularUpdater {
    public static CoroutineRegularUpdater Main { get; private set; } = null!;

    private void Awake() {
        Main = this;
    }

    public override bool UpdateDuringPause => true;

    public override int UpdatePriority => UpdatePriorities.SOF;
}
}
