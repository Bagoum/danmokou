using System;
using System.Collections.Generic;
using BagoumLib;
using BagoumLib.DataStructures;
using UnityEngine;

namespace Danmokou.Core.DInput {
public class AggregateInputSource : IInputHandlerInputSource, IInputSource {
    public const int REPLAY_PRIORITY = -100;
    private DMCompactingArray<IInputSource> Sources { get; } = new(8);
    public MainInputSource MainSource { get; }
    private static readonly Dictionary<KeyCode, IInputHandler> KeyTriggers = new();

    public readonly IInputHandler ReplayDebugSave = new AndInputHandler(
        InputHandler.Hold(InputHelpers.Key(KeyCode.LeftControl)),
        InputHandler.Hold(InputHelpers.Key(KeyCode.LeftShift)),
        InputHandler.Trigger(InputHelpers.Key(KeyCode.R))
    );
    public List<IInputHandler> Handlers { get; } = new();

    public AggregateInputSource(MainInputSource main) {
        Handlers.Add(ReplayDebugSave);
        MainSource = main;
    }
    
    public DeletionMarker<IInputSource> AddSource(IInputSource source, int priority) {
        return Sources.AddPriority(source, priority);
    }

    private T? Aggregate<T>(Func<IInputSource, T?> map) where T : struct {
        for (int ii = 0; ii < Sources.Count; ++ii)
            if (Sources.ExistsAt(ii) && map(Sources[ii]).Try(out var inp))
                return inp;
        return map(MainSource.Current);
    }

    public void OncePerUnityFrameToggleControls() {
        for (int ii = 0; ii < Sources.Count; ++ii)
            if (Sources.ExistsAt(ii))
                Sources[ii].OncePerUnityFrameToggleControls();
        Sources.Compact();
        MainSource.OncePerUnityFrameToggleControls();
        ((IInputHandlerInputSource)this).UpdateHandlers();
    }
    
    public IInputHandler GetKeyTrigger(KeyCode key) {
        if (!KeyTriggers.TryGetValue(key, out var v))
            AddHandler(v = KeyTriggers[key] = InputHandler.Trigger(InputHelpers.Key(key)));
        return v;
    }

    public void AddHandler(IInputHandler h) {
        Handlers.Add(h);
        h.Update();
    }

    //praying these don't make garbage, lmao.
    public short? HorizontalSpeed => Aggregate(x => x.HorizontalSpeed);
    public short? VerticalSpeed => Aggregate(x => x.VerticalSpeed);
    public bool? Firing => Aggregate(x => x.Firing);
    public bool? Focus => Aggregate(x => x.Focus);
    public bool? Bomb => Aggregate(x => x.Bomb);
    public bool? Meter => Aggregate(x => x.Meter);
    public bool? Swap => Aggregate(x => x.Swap);
    public bool? Pause => Aggregate(x => x.Pause);
    public bool? VNBacklogPause => Aggregate(x => x.VNBacklogPause);
    public bool? UIConfirm => Aggregate(x => x.UIConfirm);
    public bool? UIBack => Aggregate(x => x.UIBack);
    public bool? UILeft => Aggregate(x => x.UILeft);
    public bool? UIRight => Aggregate(x => x.UIRight);
    public bool? UIUp => Aggregate(x => x.UIUp);
    public bool? UIDown => Aggregate(x => x.UIDown);

    public bool? DialogueConfirm => Aggregate(x => x.DialogueConfirm);
    public bool? DialogueSkipAll => Aggregate(x => x.DialogueSkipAll);
}

}