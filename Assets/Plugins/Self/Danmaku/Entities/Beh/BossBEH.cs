using UnityEngine;

namespace Danmaku {
public class BossBEH : BehaviorEntity {
    public bool linkToEditorReload = false;


#if UNITY_EDITOR
    private void Update() {
        if (linkToEditorReload) {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            else if (Input.GetKeyDown(KeyCode.Keypad5)) {
                GameManagement.Difficulty = DifficultySet.Easier;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.T)) {
                GameManagement.Difficulty = DifficultySet.Easy;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.Y)) {
                GameManagement.Difficulty = DifficultySet.Normal;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.U)) {
                GameManagement.Difficulty = DifficultySet.Hard;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.I)) {
                GameManagement.Difficulty = DifficultySet.Lunatic;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.O)) {
                GameManagement.Difficulty = DifficultySet.Ultra;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.P)) {
                GameManagement.Difficulty = DifficultySet.Abex;
                Restart();
            } else if (Input.GetKeyDown(KeyCode.LeftBracket)) {
                GameManagement.Difficulty = DifficultySet.Assembly;
                Restart();
            }
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