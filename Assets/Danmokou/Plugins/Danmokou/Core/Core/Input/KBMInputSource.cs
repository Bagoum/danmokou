using System.Collections.Generic;
using BagoumLib;
using UnityEngine;
using static FileUtils;
using static Danmokou.Core.DInput.InputHelpers;

namespace Danmokou.Core.DInput {

public class KBMInputSource : IKeyedInputSource, IPrimaryInputSource {
    public List<IInputHandler> Handlers { get; } = new();
    public MainInputSource Container { get; set; } = null!;
    public KBMInputSource() {
        arrowLeft = InputHandler.Trigger(leftArrowDown);
        arrowRight = InputHandler.Trigger(rightArrowDown);
        arrowUp = InputHandler.Trigger(upArrowDown);
        arrowDown = InputHandler.Trigger(downArrowDown);
        ((IKeyedInputSource)this).AddUpdaters();
    }

    private readonly IInputChecker leftArrowDown = Key(KeyCode.LeftArrow);
    private readonly IInputChecker rightArrowDown = Key(KeyCode.RightArrow);
    private readonly IInputChecker upArrowDown = Key(KeyCode.UpArrow);
    private readonly IInputChecker downArrowDown = Key(KeyCode.DownArrow);
    public IInputHandler arrowLeft { get; }
    public IInputHandler arrowRight { get; }
    public IInputHandler arrowUp { get; }
    public IInputHandler arrowDown { get; }
    
    public IInputHandler focusHold { get; } = InputHandler.Hold(Key(i.FocusHold));
    public IInputHandler fireHold { get; } = InputHandler.Hold(Key(i.ShootHold));
    public IInputHandler bomb { get; } = TKey(i.Special);
    public IInputHandler meter { get; } = InputHandler.Hold(Key(i.Special));
    public IInputHandler swap { get; } = TKey(i.Swap);
    public IInputHandler pause { get; } =InputHandler.Trigger(
#if WEBGL || UNITY_ANDROID
//Esc is reserved in WebGL, and is mapped to the back button in Android
        Key(KeyCode.BackQuote)
#else
        Key(KeyCode.BackQuote).Or(Key(KeyCode.Escape))
#endif
        );
    public IInputHandler vnBacklogPause { get; } = TKey(KeyCode.L);
    public IInputHandler uiConfirm { get; } =
        InputHandler.Trigger(Key(KeyCode.Z).Or(Key(KeyCode.Return)).Or(Key(KeyCode.Space)));
    
    
    //mouse button 0, 1, 2 = left, right, middle click
    //don't listen to mouse left click for confirm-- left clicks need to be reported by the targeted elemnt
    public IInputHandler uiBack { get; } =
        InputHandler.Trigger(Key(KeyCode.X)
            .Or(Mouse(1)
#if UNITY_ANDROID
//System back button is mapped to ESC
            .Or(Key(KeyCode.Escape))
#endif
        ));
    public IInputHandler dialogueSkipAll { get; } = TKey(KeyCode.LeftControl);
    
    
    public short? HorizontalSpeed =>
        rightArrowDown.Active ? IInputSource.maxSpeed : leftArrowDown.Active ? IInputSource.minSpeed : (short)0;
    public short? VerticalSpeed =>
        upArrowDown.Active ? IInputSource.maxSpeed : downArrowDown.Active ? IInputSource.minSpeed : (short)0;
    
    void IInputSource.OncePerUnityFrameToggleControls() {
        if (((IInputHandlerInputSource)this).UpdateHandlers()) {
            Container.MarkActive(this);
        }
    }
}

}