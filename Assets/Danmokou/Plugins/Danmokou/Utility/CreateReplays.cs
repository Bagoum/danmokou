using System.Collections;
using System.Collections.Generic;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;

public class CreateReplays : RegularUpdater {
    private enum ReplayHelperState {
        RECORDING,
        REPLAYING,
        NONE
    }
    
    public string saveReplayTo = null!;
    public TextAsset runReplay = null!;
    public Replayer.ReplayerConfig.FinishMethod replayFinishMethod = Replayer.ReplayerConfig.FinishMethod.REPEAT;
    
    private ReplayHelperState state = ReplayHelperState.NONE;

    public override void RegularUpdate() {
        if (ETime.FirstUpdateForScreen) {
            if (Input.GetKeyDown(KeyCode.G)) {
                if (state == ReplayHelperState.RECORDING) {
                    var r = Replayer.End(null);
                    SaveData.Replays.SaveReplayFrames(saveReplayTo, r!.Value.frames());
                    state = ReplayHelperState.NONE;
                } else {
                    Replayer.BeginRecording();
                    state = ReplayHelperState.RECORDING;
                }
            }
        }
    }

    [ContextMenu("Run Replay")]
    public void RunReplay() {
        state = ReplayHelperState.REPLAYING;
        Replayer.BeginReplaying(new Replayer.ReplayerConfig(replayFinishMethod, 
            SaveData.Replays.LoadReplayFrames(runReplay)));
    }
}