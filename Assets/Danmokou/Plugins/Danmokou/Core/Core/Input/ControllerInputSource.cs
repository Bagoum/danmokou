using System;
using System.Collections.Generic;
using System.Reactive;
using BagoumLib.Culture;
using BagoumLib.Functional;
using Danmokou.DMath;
using UnityEngine;
using KC = UnityEngine.KeyCode;
using static Danmokou.Core.DInput.InputSettings;
using static Danmokou.Core.LocalizedStrings.Controls;

namespace Danmokou.Core.DInput {

/// <summary>
/// Maps an underlying <see cref="RebindableInputBinding"/> through a specific controller,
/// so <see cref="AnyControllerInputBinding"/> will be narrowed down to <see cref="ControllerInputBinding"/>.
/// This allows more accurately handling individual controllers' input.
/// </summary>
public class ControllerRebindingProxy : IPurposefulInputBinding {
    private readonly RebindableInputBinding source;
    private readonly InputObject.Controller controller;
    private readonly IInputBinding?[] mapped;
    private readonly IDisposable token;
    
    public LString Purpose => source.Purpose;
    public string Description => source.Description;
    public bool Active {
        get {
            foreach (var b in mapped)
                if (b is { Active: true })
                    return true;
            return false;
        }
    }
    
    public ControllerRebindingProxy(RebindableInputBinding source, InputObject.Controller controller) {
        this.source = source;
        this.controller = controller;
        mapped = new IInputBinding[source.Sources.Length];
        token = source.BindingsChanged.Subscribe(Remap);
        Remap(default);
    }

    private void Remap(Unit _) {
        for (int ii = 0; ii < mapped.Length; ++ii) {
            var b = source.Sources[ii];
            mapped[ii] = (b is AnyControllerInputBinding cb) ? cb.Realize(controller) : b;
        }
    }

    public void ComputeAxes(bool isReversed, List<(Either<ControllerAxis, KeyCode>, float)> loadInto) {
        foreach (var b in mapped) {
            if (b is ControllerInputBinding.Axis ba)
                loadInto.Add((ba.CAxis, ba.AxisValue));
            else if (b is ControllerInputBinding.Key bk) {
                loadInto.Add((bk.BaseKey, bk.Active ? (isReversed ? -1 : 1) : 0));
            }
        }
    }

}
public class ControllerInputSource : IKeyedInputSource, IPrimaryInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    public bool AnyKeyPressedThisFrame { get; private set; }
    bool IInputHandlerInputSource.AnyKeyPressedThisFrame {
        get => AnyKeyPressedThisFrame;
        set => AnyKeyPressedThisFrame = value;
    }
    public MainInputSource Container { get; set; } = null!;
    public InputObject.Controller Source { get; }

    private ControllerRebindingProxy pleft;
    private ControllerRebindingProxy pright;
    private ControllerRebindingProxy pup;
    private ControllerRebindingProxy pdown;
    
    public ControllerInputSource(InputObject.Controller source) {
        Source = source;

        ControllerRebindingProxy Proxy(RebindableInputBinding b) => new(b, source);
        
        arrowLeft = InputHandler.Trigger(pleft = Proxy(i.CLeft), left);
        arrowRight = InputHandler.Trigger(pright = Proxy(i.CRight), right);
        arrowUp = InputHandler.Trigger(pup = Proxy(i.CUp), up);
        arrowDown = InputHandler.Trigger(pdown = Proxy(i.CDown), down);
        focusHold = InputHandler.Hold(Proxy(i.CFocusHold), focus);
        fireHold = InputHandler.Hold(Proxy(i.CShootHold), fire);
        bomb = InputHandler.Trigger(Proxy(i.CSpecial), special);
        meter = InputHandler.Hold(Proxy(i.CSpecial), special);
        swap = InputHandler.Trigger(Proxy(i.CSwap), LocalizedStrings.Controls.swap);
        pause = InputHandler.Trigger(Proxy(i.CPause), LocalizedStrings.Controls.pause);
        vnBacklogPause = InputHandler.Trigger(Proxy(i.CBacklog), backlog);
        uiConfirm = InputHandler.Trigger(Proxy(i.CConfirm), confirm);
        uiBack = InputHandler.Trigger(Proxy(i.CBack), back);

        ((IKeyedInputSource)this).AddUpdaters();
    }

    public IInputHandler arrowLeft { get; }
    public IInputHandler arrowRight { get; }
    public IInputHandler arrowUp { get; }
    public IInputHandler arrowDown { get; }
    public IInputHandler focusHold { get; }
    public IInputHandler fireHold { get; }
    public IInputHandler bomb { get; }
    public IInputHandler meter { get; }
    public IInputHandler swap { get; }
    public IInputHandler pause { get; }
    public IInputHandler vnBacklogPause { get; }
    public IInputHandler uiConfirm { get; }
    public IInputHandler uiBack { get; }
    public IInputHandler? dialogueSkipAll { get; } = null;

    private readonly List<(Either<ControllerAxis, KeyCode>, float)> accValues = new();
    private readonly HashSet<ControllerAxis> countedTypes = new();

    private float AccumulateSources(ControllerRebindingProxy negated, ControllerRebindingProxy inphase) {
        accValues.Clear();
        countedTypes.Clear();
        negated.ComputeAxes(true, accValues);
        inphase.ComputeAxes(false, accValues);
        var acc = 0f;
        //Don't double-count numbers from the same axis source
        foreach (var (typ, val) in accValues) {
            if (!typ.IsLeft || !countedTypes.Contains(typ.Left)) {
                acc += val;
                if (typ.IsLeft) countedTypes.Add(typ.Left);
            }
        }
        return Math.Clamp(acc, -1, 1);
    }
    public short? HorizontalSpeed =>
        M.ClampS(IInputSource.minSpeed, IInputSource.maxSpeed,
            (short)(IInputSource.maxSpeed * AccumulateSources(pleft, pright)));
    public short? VerticalSpeed =>
        M.ClampS(IInputSource.minSpeed, IInputSource.maxSpeed,
            (short)(IInputSource.maxSpeed * AccumulateSources(pdown, pup)));


    bool IInputSource.OncePerUnityFrameToggleControls() {
        if (((IInputHandlerInputSource)this).OncePerUnityFrameUpdateHandlers()) {
            Container.MarkActive(this);
            return true;
        } else
            return false;
    }
}
}