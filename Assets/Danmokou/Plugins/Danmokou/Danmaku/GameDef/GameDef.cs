using System;
using System.Collections.Generic;
using Danmokou.Achievements;
using Danmokou.Core.DInput;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;

namespace Danmokou {

/// <summary>
/// A wrapper ScriptableObject subclassed to define functionality for a game.
/// <br/>Subclasses <see cref="ADV.ADVGameDef"/> and <see cref="Danmaku.DanmakuGameDef"/> define functionality
///  more specific to those game types.
/// </summary>
public abstract class GameDef : ScriptableObject, IGameDef {
    public string m_key = "";
    public Version m_version;
    public SceneConfig? m_tutorial;
    public SceneConfig? m_miniTutorial;
    public string Key => m_key;
    public Version Version => m_version;
    public SceneConfig? Tutorial => m_tutorial;
    public SceneConfig? MiniTutorial => m_miniTutorial;
    public virtual AchievementRepo? MakeAchievements() => null;
    
    public virtual void ApplyConfigurations() { }
    
    public virtual (IEnumerable<RebindableInputBinding> kbm, IEnumerable<RebindableInputBinding> controller) GetRebindableControls() =>
        (InputSettings.i.KBMBindings, InputSettings.i.ControllerBindings);

    public virtual FrameInput RecordReplayFrame() =>
        throw new NotImplementedException($"The game {Key} does not support recording replays, because no replay input recording is defined");
    public virtual IInputSource CreateReplayInputSource(ReplayPlayer input) =>
        throw new NotImplementedException($"The game {Key} does not support replaying replays, because no replay input handling is defined");
}

public interface IGameDef {
    /// <summary>
    /// Short identifier unique to this game.
    /// </summary>
    string Key { get; }
    Version Version { get; }
    AchievementRepo? MakeAchievements();
    SceneConfig? Tutorial { get; }
    SceneConfig? MiniTutorial { get; }

    /// <summary>
    /// Apply any settings specific to this game definition to the engine at large.
    /// </summary>
    void ApplyConfigurations() { }

    /// <summary>
    /// Get the list of controls that can be rebound from the controls menu.
    /// </summary>
    /// <returns></returns>
    (IEnumerable<RebindableInputBinding> kbm, IEnumerable<RebindableInputBinding> controller) GetRebindableControls();

    /// <summary>
    /// Record all relevant game input into a replay frame.
    /// </summary>
    FrameInput RecordReplayFrame();
    
    /// <summary>
    /// Create an input source (such as <see cref="StandardDanmakuInputExtractor"/>) from a replay provider.
    /// </summary>
    IInputSource CreateReplayInputSource(ReplayPlayer input);
}
}