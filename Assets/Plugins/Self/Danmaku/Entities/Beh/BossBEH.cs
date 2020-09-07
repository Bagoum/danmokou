using UnityEngine;

namespace Danmaku {
public class BossBEH : BehaviorEntity {
    public bool linkToEditorReload = false;


#if UNITY_EDITOR
    private void Update() {
        if (linkToEditorReload) {
            if (LevelController.ShouldRestart()) Restart();
        }
    }

    private void Restart() {
        Debug.Log("Reloading level. To avoid Event DelMarker bugs, running HardCancel first.");
        Debug.Log($"{GameManagement.DifficultyString.ToUpper()} is the current difficulty");
        UIManager.UpdateTags();
        HardCancel(false); //Prevents event DM caching bugs...
        global::GameManagement.LocalReset();
        RunAttachedSM();
    }


#endif

    public override bool OutOfHP() {
        ShiftPhase();
        return false;
    }
}
}