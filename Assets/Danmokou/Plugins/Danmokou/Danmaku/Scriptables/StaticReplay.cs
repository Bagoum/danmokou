using System;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.GameInstance;
using Danmokou.Graphics;
using Danmokou.Scenes;
using Danmokou.Services;
using Suzunoya.Data;
using UnityEngine;

namespace Danmokou.Scriptables {
//A replay that is saved and shipped with the game, eg. shot demo replays.
[CreateAssetMenu(menuName = "Data/Static Replay")]
public class StaticReplay : ScriptableObject {
    [Tooltip("The .txt replay metadata.")]
    public TextAsset replayMetadata = null!;
    [Tooltip("The .dat replay data.")]
    public TextAsset replayFile = null!;

    public Replay CompiledReplay =>
        new Replay(Frames, _metadata ??= Serialization.DeserializeJson<ReplayMetadata>(replayMetadata.text)
            ?? throw new Exception($"Couldn't read static replay metadata from {replayMetadata.name}"));
    
    [NonSerialized] private ReplayMetadata? _metadata = null;
    [NonSerialized] private FrameInput[]? _frames = null;

    public Func<FrameInput[]> Frames => () => {
        if (_frames == null || _frames.Length == 0)
            try {
                _frames = SaveData.Replays.LoadReplayFrames(replayFile)();
            } catch (Exception ex) {
                _frames = Array.Empty<FrameInput>();
                Logs.LogException(new Exception("Failed to load static replay", ex));
            }
        return _frames;
    };

    [ContextMenu("View Replay")]
    public void View() {
        new InstanceRequest((_, __) => ServiceLocator.Find<ISceneIntermediary>().LoadScene(
            InstanceRequest.ReturnScene(GameManagement.References.mainMenu)), CompiledReplay) {
            PreferredCameraTransition = GameManagement.References.defaultTransition.AsQuickFade()
        }.Run();
    }

}
}