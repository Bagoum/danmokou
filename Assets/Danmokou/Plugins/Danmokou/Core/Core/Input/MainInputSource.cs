using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib.DataStructures;
using UnityEngine;

namespace Danmokou.Core.DInput {

/// <summary>
/// An abstraction representing a primary input source (eg. KBM, controller, or touch input).
/// </summary>
public interface IPrimaryInputSource : IDescriptiveInputSource {
    public MainInputSource Container { set; }
    public bool AnyKeyPressedThisFrame { get; }

    /// <summary>
    /// The priority of this input source when multiple input sources are simultaneously active
    ///  (lower priority = more prioritized).
    /// </summary>
    public int Priority { get; }
}

/// <summary>
/// An abstraction enclosing multiple <see cref="IPrimaryInputSource"/>s,
///  only one of which may be "active" at a given time.
/// <br/>This allows customizing button tooltips and the like to the "currently active input mechanism".
/// </summary>
public class MainInputSource {
    private readonly DMCompactingArray<IPrimaryInputSource> sources = new(8);
    public IPrimaryInputSource Current { get; private set; }
    private string[] controllersRaw = Array.Empty<string>();
    private readonly List<(string name, DeletionMarker<IPrimaryInputSource> entry)> controllers = new();
    /// <summary>
    /// Maps controller types to a provisioned controller of that type.
    /// Only one controller is permitted per type (single-player assumption).
    /// </summary>
    private readonly Dictionary<ControllerType, (int, InputObject.Controller)> controllerTypeMap = new();
    private int frameCt = 0;

    private DeletionMarker<IPrimaryInputSource> AddSource(IPrimaryInputSource s) {
        s.Container = this;
        return sources.Add(s);
    }

    public ControllerType? GetFirstControllerType() {
        for (int ii = 0; ii < sources.Count; ++ii) {
            if (sources.ExistsAt(ii) && sources[ii] is ControllerInputSource c)
                return c.Source.Type;
        }
        return null;
    }

    private bool RecheckControllers() {
        var newControllers = Input.GetJoystickNames();
        if (newControllers.Length != controllersRaw.Length)
            goto requiresUpdate;
        for (int ii = 0; ii < newControllers.Length; ++ii)
            if (newControllers[ii] != controllersRaw[ii])
                goto requiresUpdate;
        return false;
        
        requiresUpdate:
        Logs.Log($"Controllers have changed to {string.Join(", ", newControllers)}");
        foreach (var (_, dm) in controllers)
            dm.MarkForDeletion();
        controllersRaw = newControllers;
        controllers.Clear();
        controllerTypeMap.Clear();
        for (int ii = 0; ii < newControllers.Length; ++ii) {
            var c = newControllers[ii];
            if (InputObject.FromJoystickName(c, ii) is { } typ && !controllerTypeMap.ContainsKey(typ.Type)) {
                controllerTypeMap[typ.Type] = (ii, typ);
                controllers.Add((c, AddSource(new ControllerInputSource(typ))));
            }
            
        }
        return true;
    }
    
    public MainInputSource() {
        AddSource(new KBMInputSource());
        Current = sources[0];
        RecheckControllers();
    }

    private IPrimaryInputSource? nextCurrent = null;
    public void MarkActive(IPrimaryInputSource src) {
        if (nextCurrent is null)
            nextCurrent = src;
        else if (src.Priority < nextCurrent.Priority)
            nextCurrent = src;
    }

    public bool OncePerUnityFrameToggleControls() {
        //Use inner frame count since ETime.FrameNumber does not account for pause menus
        if (++frameCt % ETime.ENGINEFPS == 0 && RecheckControllers())
            Current = sources[0];
        nextCurrent = null;
        for (int ii = 0; ii < sources.Count; ++ii)
            if (sources.ExistsAt(ii))
                sources[ii].OncePerUnityFrameToggleControls();
        sources.Compact();
        Current = nextCurrent ?? Current;
        return Current.AnyKeyPressedThisFrame;
        //Logs.Log($"Current method: {Current}, updated {nextCurrent}");
    }
}
}