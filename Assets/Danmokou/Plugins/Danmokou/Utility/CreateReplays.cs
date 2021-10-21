using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;

public class CreateReplays : RegularUpdater {
    public string saveReplayTo = null!;
    public TextAsset runReplay = null!;
    public Replayer.ReplayerConfig.FinishMethod replayFinishMethod = Replayer.ReplayerConfig.FinishMethod.REPEAT;

    private ReplayActor? actor = null;

    public override void RegularUpdate() {
        if (ETime.FirstUpdateForScreen) {
            if (Input.GetKeyDown(KeyCode.G)) {
                if (actor is ReplayRecorder rr) {
                    SaveData.Replays.SaveReplayFrames(saveReplayTo, rr.Recording.ToArray());
                    rr.Cancel();
                    actor = null;
                } else {
                    actor = Replayer.BeginRecording();
                }
            }
        }
    }

    [ContextMenu("Run Replay")]
    public void RunReplay() {
        Replayer.BeginReplaying(new Replayer.ReplayerConfig(replayFinishMethod, 
            SaveData.Replays.LoadReplayFrames(runReplay)));
    }
}