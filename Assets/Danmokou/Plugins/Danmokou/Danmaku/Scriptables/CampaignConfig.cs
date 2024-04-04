using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Player;
using Danmokou.Scenes;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Danmokou.Scriptables {
public interface ICampaignMeta {
    string Key { get; }
    bool Replayable { get; }
    bool AllowDialogueSkip { get; }
}

public abstract class BaseCampaignConfig : ScriptableObject, ICampaignMeta {
    public string key = "";
    public string shortTitle = "";
    public bool replayable = true;
    public bool allowDialogueSkip = true;
    public int startLives;
    public int? StartLives => startLives > 0 ? startLives : null;
    public ShipConfig[] players = null!;
    public StageConfig[] stages = null!;
    /// <summary>
    /// All bosses used in this campaign. Set `BossConfig.practiceable` to whether or not they should appear
    ///  in the practice menu.
    /// </summary>
    [FormerlySerializedAs("practiceBosses")] 
    public BossConfig[] bosses = null!;
    public string Key => key;
    public bool Replayable => replayable;
    public bool AllowDialogueSkip => allowDialogueSkip;

    public abstract Task<InstanceRecord>? RunEntireCampaign(InstanceRequest req, SMAnalysis.AnalyzedCampaign c);

    protected SceneLoading? LoadStageScene(InstanceRequest req, IStageConfig stage, Checkpoint? checkpoint, 
            out TaskCompletionSource<InstanceStepCompletion> tcs, bool doSetup = false) =>
        ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnFinish(
            stage.Scene,
            SceneRequest.Reason.RUN_SEQUENCE,
            doSetup ? req.SetupInstance : null,
            //Load stage state machine immediately after scene changes in order to minimize loading lag later
            () => _ = stage.StateMachine,
            () => ServiceLocator.Find<LevelController>()
                .RunLevel(new(1, LevelController.LevelRunMethod.CONTINUE, stage, req.InstTracker), checkpoint),
            out tcs).With(req.PreferredCameraTransition));

    protected TaskCompletionSource<InstanceStepCompletion> LoadStageSceneOrThrow(InstanceRequest req, IStageConfig stage, Checkpoint? checkpoint) {
        if (LoadStageScene(req, stage, checkpoint, out var tcs) is null)
            throw new Exception($"Failed to load stage for campaign {Key}");
        return tcs;
    }

    protected TaskCompletionSource<InstanceStepCompletion> LoadStageSceneOrThrow(InstanceRequest req, int index, Checkpoint? checkpoint) {
        if (LoadStageScene(req, stages[index], checkpoint, out var tcs, doSetup: index == 0 && checkpoint is null) is null)
            throw new Exception($"Failed to load stage {index} for campaign {Key}");
        return tcs;
    }

    protected InstanceRecord FinishCampaign(InstanceRequest req, string? endingKey = null) =>
        req.CompileAndSaveRecord(req.MakeGameRecord(null, endingKey));
}

[Serializable]
public struct FixedShotPart {
    public ShipConfig ship;
    public ShotConfig shot;
    public AbilityCfg? ability;
}

/// <summary>
/// Basic handler for campaign execution with support for endings.
/// <br/>For more complex campaign logic, subclass <see cref="BaseCampaignConfig"/> for the specific campaign,
///  and override <see cref="RunEntireCampaign"/>.
/// </summary>
[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : BaseCampaignConfig {
    public EndingConfig[] endings = null!;
    public FixedShotPart[] fixedShot = null!;

    public bool HasOneShotConfig(out TeamConfig team) {
        team = default;
        if (fixedShot != null && fixedShot.Length > 0) {
            team = new(0, Subshot.TYPE_D, 
                fixedShot.Select(x => (x.ship, x.shot, x.ability as IAbilityCfg)).ToArray());
            return true;
        }
        
        if (players.Length != 1) return false;
        if (players[0].shots2.Length != 1 || players[0].supports.Length != 1) return false;
        if (players[0].shots2[0].shot.isMultiShot) return false;
        team = new(0, Subshot.TYPE_D,
            (players[0], players[0].shots2[0].shot, players[0].supports[0].ability));
        return true;
    }

    public bool TryGetEnding(out EndingConfig ed) {
        ed = default!;
        foreach (var e in endings) {
            if (e.Matches) {
                ed = e;
                return true;
            }
        }
        return false;
    }

    public override async Task<InstanceRecord> RunEntireCampaign(InstanceRequest req, SMAnalysis.AnalyzedCampaign c) {
        if (this != c.campaign)
            throw new Exception("Incorrect AnalyzedCampaign provided to CampaignConfig.RunEntireCampaign");
        //We could handle 0 separately by doing something like:
        //   if (LoadStageScene(req, 0, out var tcs) is null)
        //       return null;
        //and then putting everything below in a separate function,
        // however it's basically impossible for the 0th stage call to fail,
        // so it's ok to throw an exception in such a case
        for (int ii = 0; ii < stages.Length; ++ii) {
            for (Checkpoint? ch = null;;) {
                switch (await LoadStageSceneOrThrow(req, ii, ch).Task) {
                    case InstanceStepCompletion.Cancelled:
                        Logs.Log($"Campaign {c.campaign.Key} was cancelled.", true, LogLevel.INFO);
                        throw new OperationCanceledException();
                    case InstanceStepCompletion.RestartCheckpoint restart:
                        ch = restart.Checkpoint;
                        ii = stages.IndexOf(restart.Checkpoint.Stage as StageConfig);
                        break;
                    default:
                        goto next_stage;
                }
            }
            next_stage: ;
            InstanceRequest.StageCompleted.OnNext((Key, ii));
        }
        if (TryGetEnding(out var ed)) {
            var blockRestart = req.CanRestartStage.AddConst(false);
            await LoadStageSceneOrThrow(req, new EndcardStageConfig(ed.dialogueKey, c.Game.Endcard), null).Task;
            blockRestart.Dispose();
            return FinishCampaign(req, ed.key);
        } else 
            return FinishCampaign(req);
    }
}
}