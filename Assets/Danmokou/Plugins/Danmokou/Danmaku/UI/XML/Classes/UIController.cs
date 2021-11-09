using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.UIElements;
using static Danmokou.Core.InputManager;

namespace Danmokou.UI.XML {
public enum UICommand {
    Left,
    Right,
    Up,
    Down,
    Confirm,
    Back
}

public abstract record UIMouseCommand {
    public virtual bool ValidForCurrent(NUINode current) => true;

    public record NormalCommand(UICommand Command, NUINode Source) : UIMouseCommand {
        public override bool ValidForCurrent(NUINode current) => Source == current;
    }

    public record Goto(NUINode Target) : UIMouseCommand { }
}

public abstract record UIResult {
    public record DestroyMenu : UIResult { }

    public record StayOnNode(bool IsNoOp = false) : UIResult { }

    public record GoToNode(UIGroup Group, int? Index = null) : UIResult {
        public NUINode Target => Index.Try(out var i) ? Group.Nodes[i] : Group.Nodes[Group.EntryIndex];
    }

    public record ReturnToGroupCaller : UIResult { }

    public record ReturnToScreenCaller : UIResult { }
}

public abstract class UIController : CoroutineRegularUpdater {
    public UIBuilderRenderer uiRenderer = null!;
    public SFXConfig? upDownSound;
    public SFXConfig? leftRightSound;
    public SFXConfig? confirmSound;
    public SFXConfig? failureSound;
    public SFXConfig? backSound;

    private float swipeTime = 0.3f;
    public VisualElement UI { get; private set; } = null!;
    protected virtual IEnumerable<NUIScreen> Screens => new[] {MainScreen};
    protected NUIScreen MainScreen { get; set; } = null!;
    
    private NUINode? Current { get; set; } //If this is null, the menu is not active.
    public DisturbedAnd UpdatesEnabled { get; } = new();
    public Stack<NUINode> ScreenCall { get; } = new();
    public Stack<NUINode> GroupCall { get; } = new();

    //Fields for event-based changes 
    //Not sure if I want to generalize these to properly event-based...
    public UIMouseCommand? QueuedEvent { get; set; }
    
    //The reason for this virtual structure is because implementers (eg. XMLMainMenuCampaign)
    // need to store the list *statically*, since the specific menu object will be deleted on scene change.
    protected virtual List<XMLMenu.CacheInstruction>? ReturnTo { get; set; }

    protected bool RegularUpdateGuard => Application.isPlaying && ETime.FirstUpdateForScreen && UpdatesEnabled;

    private UICommand? CurrentInputCommand =>
        UILeft.Active ? UICommand.Left :
        UIRight.Active ? UICommand.Right :
        UIUp.Active ? UICommand.Up :
        UIDown.Active ? UICommand.Down :
        UIConfirm.Active ? UICommand.Confirm :
        UIBack.Active ? UICommand.Back :
        null;
    
    public override void FirstFrame() {
        UI = GetComponent<UIDocument>().rootVisualElement;
        Build();
        //The UI starts off as open, and can be closed by the child afterwards.
        Open();
        DoReturn();
    }

    private void DoReturn() {
        if (ReturnTo == null) return;
        /*
        if (Current == null)
            throw new Exception("ReturnTo exists, but Current is null");
        for (int ii = 0; ii < ReturnTo.Count; ++ii) {
            var prev = Current;
            var inst = ReturnTo[ii];
            if (inst.type == XMLMenu.CacheInstruction.InstrType.CONFIRM) {
                Current = Current.Confirm().target!;
                if (Current.screen != prev.screen) {
                    //The exit/enter events are handled by Confirm internals
                    prev.screen.RunPreExit();
                    Current.screen.RunPreEnter();
                    Current.screen.RunPostEnter();
                }
            } else if (inst.type == XMLMenu.CacheInstruction.InstrType.GOTO_OPTION) {
                if (Current is IOptionNodeLR opt) {
                    opt.Index = inst.instrVal;
                } else
                    throw new Exception("Couldn't rebuild menu position: node is not an option");
            } else if (inst.type == XMLMenu.CacheInstruction.InstrType.GOTO_SIBLING) {
                Current = Current.Siblings[inst.instrVal];
                Current.OnVisit(prev, false);
            } else if (inst.type == XMLMenu.CacheInstruction.InstrType.GOTO_CHILD) {
                Current = Current.children[inst.instrVal];
                Current.OnVisit(prev, false);
            } else
                throw new Exception($"Couldn't resolve instruction {inst.type}");
        }
        */
        ReturnTo = null;
    }

    private void Build() {
        var container = UI.Q("UIContainer");
        foreach (var s in Screens.FilterNone())
            container.Add(s.Build(GameManagement.References.uxmlDefaults.TypeMap));
    }
    private void Redraw() {
        foreach (var screen in Screens)
            screen.SetVisible(Current?.Screen == screen);
        if (Current != null) {
            var states = new Dictionary<NUINode, UINodeState>();
            foreach (var n in GroupCall)
                states[n] = UINodeState.GroupCaller;
            foreach (var n in Current.Group.Nodes)
                states[n] = UINodeState.GroupFocused;
            states[Current] = UINodeState.Focused;
            //Other screens don't need to be redrawn
            foreach (var n in Current.Screen.Groups.SelectMany(g => g.Nodes))
                n.Redraw(states.TryGetValue(n, out var s) ? s : UINodeState.Default);
            RunDroppableRIEnumerator(scrollToCurrent());
        }
    }
    //Workaround for limitation that cannot ScrollTo to objects that have just been constructed or made visible
    private IEnumerator scrollToCurrent() {
        yield return null;
        while (!ETime.FirstUpdateForScreen) yield return null;
        Current?.ScrollTo();
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (RegularUpdateGuard && Current != null) {
            var next = Current;
            bool doCustomSFX = false;
            UICommand? command = null;
            UIResult? result = null;
            if (QueuedEvent is UIMouseCommand.Goto mouseGoto && mouseGoto.Target.Screen == Current.Screen) {
                next = mouseGoto.Target;
                ServiceLocator.SFXService.Request(
                    mouseGoto.Target.Group == Current.Group ? upDownSound : leftRightSound);
            } else if (Current.CustomEventHandling().Try(out var r)) {
                result = r;
                doCustomSFX = true;
            } else {
                command = (QueuedEvent is UIMouseCommand.NormalCommand uic && QueuedEvent.ValidForCurrent(Current)) ?
                    uic.Command :
                    CurrentInputCommand;
                if (command.Try(out var cmd))
                    result = Current.Navigate(cmd);
            }
            ServiceLocator.SFXService.Request(command switch {
                UICommand.Left => leftRightSound,
                UICommand.Right => leftRightSound,
                UICommand.Up => upDownSound,
                UICommand.Down => upDownSound,
                UICommand.Confirm => !(result is UIResult.StayOnNode {IsNoOp: true}) ? confirmSound : failureSound,
                UICommand.Back => backSound,
                _ => null
            });
            if (result == null && doCustomSFX)
                //Probably need to get this from the custom node handling? idk
                ServiceLocator.SFXService.Request(leftRightSound);
            switch (result) {
                case UIResult.DestroyMenu:
                    next = null;
                    break;
                case UIResult.GoToNode goToNode:
                    if (goToNode.Group.Screen != Current.Screen)
                        ScreenCall.Push(Current);
                    else if (goToNode.Group != Current.Group)
                        GroupCall.Push(Current);
                    next = goToNode.Target;
                    break;
                case UIResult.ReturnToGroupCaller:
                    next = GroupCall.TryPop(out var n) ? n : Current;
                    break;
                case UIResult.ReturnToScreenCaller:
                    next = ScreenCall.TryPop(out n) ? n : Current;
                    break;
            }
            QueuedEvent = null;
            if (result != null && !(result is UIResult.StayOnNode { IsNoOp: true }))
                TransitionToNode(next, true);
        }
    }
    
    #region Transition

    public void GoToNth(int grpIndex, int nodeIndex) => TransitionToNode(MainScreen.Groups[grpIndex].Nodes[nodeIndex], false);
    protected virtual void Open() {
        var grp = Screens.First().Groups[0];
        TransitionToNode(grp.Nodes[grp.EntryIndex], false);
    }

    protected void Close() => TransitionToNode(null, false);

    private void TransitionToNode(NUINode? next, bool animate) {
        void HandleNodeChange() {
            if (Current != next) {
                Current?.Leave(animate);
                next?.Enter(animate);
                Current = next;
                Redraw();
            }
        }
        var prev = Current;
        if (next?.Screen == prev?.Screen) {
            HandleNodeChange();
            return;
        }
        prev?.Screen.OnPreExit?.Invoke();
        next?.Screen.OnPreEnter?.Invoke();
        IDisposable token = UpdatesEnabled.AddConst(false);
        void Midpoint() {
            prev?.Screen?.OnExit?.Invoke();
            next?.Screen?.OnEnter?.Invoke();
            HandleNodeChange();
        }
        void Finish() {
            next?.Screen?.OnPostEnter?.Invoke();
            token.Dispose();
        }
        //Only do a transition if there are no nulls involved
        var task = (prev?.Screen != null && next?.Screen != null) ? Transition(Midpoint) : null;
        if (task != null) {
            _ = task.ContinueWithSync(Finish);
        } else {
            Midpoint();
            Finish();
        }
    }

    public virtual async Task? Transition(Action midpoint) {
        var c = await uiRenderer.Slide(null, GetRandomSlideEndpoint(), swipeTime, M.EInSine);
        if (c != Completion.Standard) throw new Exception("Unexpected cancellation?");
        midpoint();
        c = await uiRenderer.Slide(GetRandomSlideEndpoint(), Vector2.zero, swipeTime, M.EOutSine);
        if (c != Completion.Standard) throw new Exception("Unexpected cancellation?");
    }
    
    
    private Vector2[] slideEndpoints => new[] {
        new Vector2(-MainCamera.ScreenWidth, 0),
        new Vector2(MainCamera.ScreenWidth, 0),
        new Vector2(0, -MainCamera.ScreenHeight),
        new Vector2(0, MainCamera.ScreenHeight)
    };

    private Vector2 GetRandomSlideEndpoint() {
        return RNG.RandSelectOffFrame(slideEndpoints);
    }
    
    #endregion
}
}