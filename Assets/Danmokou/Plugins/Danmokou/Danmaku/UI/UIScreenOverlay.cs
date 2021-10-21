using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using BagoumLib.Tweening;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using JetBrains.Annotations;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using PropConsts = Danmokou.Graphics.PropConsts;

namespace Danmokou.UI {
public interface IUIScreenOverlay {
    Task<Completion> Fade(float? start, float end, float time, Easer? smooth);
}
public class UIScreenOverlay : CoroutineRegularUpdater, IUIScreenOverlay {
    private SpriteRenderer sr = null!;
    
    private Cancellable allCancel = null!;
    private Cancellable? opCancel;

    private ICancellee MakeOpCanceller() {
        opCancel?.Cancel();
        return new JointCancellee(allCancel, opCancel = new Cancellable());
    }

    public Task<Completion> Fade(float? start, float end, float time, Easer? smooth)
        => Tween.TweenTo(start ?? sr.color.a, end, time, sr.SetAlpha, smooth, 
            MakeOpCanceller()).Run(this);

    private void Awake() {
        sr = GetComponent<SpriteRenderer>();
        allCancel = new Cancellable();
    }

    protected override void BindListeners() {
        base.BindListeners();
        RegisterService<IUIScreenOverlay>(this);
    }

    protected override void OnDisable() {
        allCancel.Cancel();
        base.OnDisable();
    }

    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;
    public override int UpdatePriority => UpdatePriorities.SLOW;
}
}
