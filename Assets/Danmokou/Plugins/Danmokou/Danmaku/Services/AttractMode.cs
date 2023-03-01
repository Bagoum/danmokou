using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Scenes;
using Danmokou.Scriptables;
using Danmokou.SM;
using Danmokou.UI.XML;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Danmokou.Services {

[Serializable]
public struct AttractModeReplay {
    public StaticReplay replay;
    public float startTime;
    public float endTime;
}

public class AttractMode : CoroutineRegularUpdater {
    public SpriteRenderer sr = null!;
    public SceneConfig mainMenuScene = null!;
    private bool timerEnabled = true;
    public float timeUntilAttractModeStarts = 180f;
    public AttractModeReplay[] replays = null!;
    public TextAsset[] preserveSMScripts = null!;

    public override void FirstFrame() {
        if (replays.Length > 0) {
            RunDroppableRIEnumerator(PrepareInitialRun());
            Listen(SceneIntermediary.SceneLoaded, s =>
                timerEnabled = (mainMenuScene.sceneName == s.name));
            Listen(UIController.UIEventQueued, _ => elapsedTime = 0);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("test only: start seq")]
    public void _tmpSeqRep() {
        RunDroppableRIEnumerator(RunReplays());
    }
#endif

    private IEnumerator RunReplays() {
        //make sure that inputs like pause don't get triggered
        using var noInput = InputManager.PlayerInput.AddSource(new NullInputSource(), 
            AggregateInputSource.REPLAY_PRIORITY + 1);
        while (true)
            foreach (var r in replays) {
                bool finished = false;
                new InstanceRequest((_, __) => finished = true, r.replay.CompiledReplay) {
                    PreferredCameraTransition = GameManagement.References.defaultTransition.AsQuickFade(true)
                }.Run();
                yield return null;
                sr.enabled = true;
                if (r.startTime > 0)
                    ETime.SkipTime(r.startTime);
                while (ETime.FrameNumber * ETime.FRAME_TIME < r.endTime && !finished) {
                    if (InputManager.MainSource.AnyKeyPressedThisFrame) {
                        GameManagement.QuickFadeToMainMenu();
                        RunDroppableRIEnumerator(WaitToStartAttractMode());
                        yield break;
                    }
                    sr.color = sr.color.WithA(0.8f + 0.2f * Mathf.Sin(ETime.FrameNumber * 0.02f));
                    yield return null;
                }
            }
    }

    private float elapsedTime = 0f;
    private IEnumerator WaitToStartAttractMode() {
        yield return null;
        sr.enabled = false;
        for (elapsedTime = 0; elapsedTime < timeUntilAttractModeStarts; elapsedTime += ETime.FRAME_TIME) {
            if (!timerEnabled || InputManager.MainSource.AnyKeyPressedThisFrame) {
                elapsedTime = 0;
            }
            yield return null;
        }
        RunDroppableRIEnumerator(RunReplays());
    }

    private IEnumerator PrepareInitialRun() {
        if (preserveSMScripts.Length > 0) {
            for (var t = 0f; t < 0.3f; t += ETime.FRAME_TIME)
                yield return null;
            Logs.Log("Starting background loading for attract mode scripts");
            var data = preserveSMScripts.Select(p => {
                var id = p.GetInstanceID();
                StateMachineManager.Preserve(id);
                return (id, p.text, p.name);
            }).ToArray();
            var task = Task.Run(() => {
                foreach (var d in data) {
                    Logs.Log($"Loading attract mode script {d.name}");
                    StateMachineManager.FromText(d.id, d.text, d.name);
                }
            });
            while (!task.IsCompleted)
                yield return null;
            Logs.Log("Background loading for attract mode complete");
        }
        RunDroppableRIEnumerator(WaitToStartAttractMode());
    }
}
}
