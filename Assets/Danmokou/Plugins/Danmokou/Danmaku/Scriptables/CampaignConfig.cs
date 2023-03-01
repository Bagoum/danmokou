using System;
using System.Collections;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.GameInstance;
using Danmokou.Scenes;
using Danmokou.SM;
using JetBrains.Annotations;
using UnityEngine;

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
    public BossConfig[] practiceBosses = null!;

    public string Key => key;
    public bool Replayable => replayable;
    public bool AllowDialogueSkip => allowDialogueSkip;

    public abstract Task<InstanceRecord>? RunEntireCampaign(InstanceRequest req, SMAnalysis.AnalyzedCampaign c);

    protected SceneLoading? LoadStageScene(InstanceRequest req, int index, out TaskCompletionSource<Unit> tcs) =>
        LoadStageScene(req, stages[index], out tcs, index == 0);

    protected SceneLoading? LoadStageScene(InstanceRequest req, IStageConfig stage, out TaskCompletionSource<Unit> tcs, bool doSetup = false) =>
        ServiceLocator.Find<ISceneIntermediary>().LoadScene(SceneRequest.TaskFromOnFinish(
            stage.Scene,
            SceneRequest.Reason.RUN_SEQUENCE,
            doSetup ? req.SetupInstance : null,
            //Load stage state machine immediately after scene changes in order to minimize loading lag later
            () => _ = stage.StateMachine,
            () => ServiceLocator.Find<LevelController>()
                .RunLevel(new(1, LevelController.LevelRunMethod.CONTINUE, stage, req.InstTracker)),
            out tcs).With(req.PreferredCameraTransition));

    protected TaskCompletionSource<Unit> LoadStageSceneOrThrow(InstanceRequest req, IStageConfig stage) {
        if (LoadStageScene(req, stage, out var tcs) is null)
            throw new Exception($"Failed to load stage for campaign {Key}");
        return tcs;
    }

    protected TaskCompletionSource<Unit> LoadStageSceneOrThrow(InstanceRequest req, int index) {
        if (LoadStageScene(req, index, out var tcs) is null)
            throw new Exception($"Failed to load stage {index} for campaign {Key}");
        return tcs;
    }

    protected InstanceRecord FinishCampaign(InstanceRequest req, string? endingKey = null) =>
        req.CompileAndSaveRecord(req.MakeGameRecord(null, endingKey));
}

/// <summary>
/// Basic handler for campaign execution with support for endings.
/// <br/>For more complex campaign logic, subclass <see cref="BaseCampaignConfig"/> for the specific campaign,
///  and override <see cref="RunEntireCampaign"/>.
/// </summary>
[CreateAssetMenu(menuName = "Data/Campaign Configuration")]
public class CampaignConfig : BaseCampaignConfig {
    public EndingConfig[] endings = null!;

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
            await LoadStageSceneOrThrow(req, ii).Task;
            InstanceRequest.StageCompleted.OnNext((Key, ii));
        }
        if (TryGetEnding(out var ed)) {
            await LoadStageSceneOrThrow(req, new EndcardStageConfig(ed.dialogueKey, c.Game.Endcard)).Task;
            return FinishCampaign(req, ed.key);
        } else 
            return FinishCampaign(req);
    }
}
}