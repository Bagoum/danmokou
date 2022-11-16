using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;

namespace Danmokou.GameInstance {
public static class InstanceConsts {
    public const int defltContinues = 42;
    public static int StartLives(InstanceMode mode) {
        if (mode == InstanceMode.CAMPAIGN || mode == InstanceMode.TUTORIAL || mode == InstanceMode.STAGE_PRACTICE) 
            return 7;
        else if (mode.OneLife()) 
            return 1;
        else if (mode == InstanceMode.NULL
#if UNITY_EDITOR || ALLOW_RELOAD
            || mode == InstanceMode.DEBUG 
#endif
        ) 
            return 14;
        else 
            return 1;
    }
    public static int StartBombs(InstanceMode mode) {
        if (mode == InstanceMode.CAMPAIGN || mode == InstanceMode.TUTORIAL || mode == InstanceMode.STAGE_PRACTICE) 
            return 3;
        else if (mode.OneLife()) 
            return 0;
        else 
            return 3;
    }


}
}