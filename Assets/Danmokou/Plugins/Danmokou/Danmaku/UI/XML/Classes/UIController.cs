using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using static Danmokou.Core.DInput.InputManager;

namespace Danmokou.UI.XML {
public enum UICommand {
    Left,
    Right,
    Up,
    Down,
    Confirm,
    Back
}

public abstract record UIPointerCommand {
    public virtual bool ValidForCurrent(UINode current) => true;

    public record NormalCommand(UICommand Command, UINode? Source) : UIPointerCommand {
        public bool Silent { get; init; } = false;
        public override bool ValidForCurrent(UINode current) => 
            Source == current || Source == null || Command == UICommand.Back;
    }
    
    
    public record Goto(UINode Target) : UIPointerCommand;
}

public abstract record UIResult {
    public record DestroyMenu : UIResult;

    public enum StayOnNodeType {
        DidSomething,
        NoOp,
        Silent
    }

    public SequentialResult Then(UIResult second) => new SequentialResult(this, second);

    public record SequentialResult(params UIResult[] results) : UIResult, IUnrollable<UIResult> {
        public IEnumerable<UIResult> Values => results;
    }

    public record StayOnNode(StayOnNodeType Action) : UIResult {
        public StayOnNode(bool IsNoOp = false) : this(IsNoOp ? StayOnNodeType.NoOp : StayOnNodeType.DidSomething) { }
    }

    public record GoToNode(UINode Target) : UIResult {
        public GoToNode(UIGroup Group, int? Index = null) : 
            this(Index.Try(out var i) ? Group.Nodes.ModIndex(i) : Group.EntryNode) {}
        
        public GoToNode(UIScreen s) : this(s.Groups[0]) { }
    }

    public record ReturnToGroupCaller : UIResult;

    public record ReturnToTargetGroupCaller(UIGroup Target) : UIResult {
        public ReturnToTargetGroupCaller(UINode node) : this(node.Group) { }
    }

    public record ReturnToScreenCaller : UIResult;

    public static implicit operator UIResult(UINode node) => new GoToNode(node);
    public static implicit operator UIResult(UIGroup group) => new GoToNode(group);
}

public abstract class UIController : CoroutineRegularUpdater {
    public abstract record CacheInstruction {
        public record ToOption(int OptionIndex) : CacheInstruction;
        public record ToGroup(int? ScreenIndex, int GroupIndex) : CacheInstruction;

        public record ToGroupNode(int NodeIndex) : CacheInstruction;
    }
    /// <summary>
    /// Points to TemplateContainer
    /// </summary>
    protected VisualElement UIRoot { get; private set; } = null!;
    protected VisualElement UIContainer { get; private set; } = null!;
    protected PanelSettings UISettings { get; private set; } = null!;
    protected virtual UIScreen?[] Screens => new[] {MainScreen};
    /// <summary>
    /// Event issued when the UI receives a visual update. Screens may subscribe to this
    ///  in order to control their rendering.
    /// </summary>
    public Event<float> UIVisualUpdateEv { get; } = new();
    /// <summary>
    /// Controls the opacity of the entire menu.
    /// Generally 0, except on pause menus.
    /// <br/>The background that is displayed is generally a flat color.
    /// </summary>
    public PushLerper<float> BackgroundOpacity { get; } = 
        new(0.5f, (a, b, t) => Mathf.Lerp(a, b, Easers.EIOSine(t)));
    protected UIScreen MainScreen { get; set; } = null!;
    public DisturbedAnd UpdatesEnabled { get; } = new();
    public Stack<UINode> ScreenCall { get; } = new();
    public Stack<(UIGroup group, UINode? node)> GroupCall { get; } = new();

    
    public UINode? Current { get; private set; }

    //Fields for event-based changes 
    //Not sure if I want to generalize these to properly event-based...
    public UIPointerCommand? QueuedEvent { get; set; }

    protected virtual UINode? StartingNode => null;
    
    //The reason for this virtual structure is because implementers (eg. XMLMainMenuCampaign)
    // need to store the list *statically*, since the specific menu object will be deleted on scene change.
    protected virtual List<CacheInstruction>? ReturnTo { get; set; }

    //The point we want to return to (eg. boss card selection) is usually not the node that causes the action
    // (eg. the difficulty select button), so we have a two-step add-commit process.
    private List<CacheInstruction>? tentativeReturnTo;
    
    
    public UIBuilderRenderer uiRenderer = null!;
    public SFXConfig? upDownSound;
    public SFXConfig? leftRightSound;
    public SFXConfig? confirmSound;
    public SFXConfig? failureSound;
    public SFXConfig? backSound;
    
    public bool MenuActive => Current != null;
    //TODO what happens if you screw with the 16x9 frame? how would you measure this in non-ideal conditions?
    public int XMLWidth => UISettings.referenceResolution.x;
    public int XMLHeight => UISettings.referenceResolution.y;

    protected List<CacheInstruction> GetInstructionsToNode(UINode? c) {
        var revInds = new List<CacheInstruction>();
        var groupStack = new Stack<(UIGroup group, UINode? node)>(GroupCall.Reverse());
        var screenStack = new Stack<UINode>(ScreenCall.Reverse());
        while (c != null) {
            if (c is IOptionNodeLR opt) {
                revInds.Add(new CacheInstruction.ToOption(opt.Index));
            } else if (c is IComplexOptionNodeLR copt) {
                revInds.Add(new CacheInstruction.ToOption(copt.Index));
            }
            if ((groupStack.TryPeek(out var g) && (g.group.Screen == c.Screen))) {
                revInds.Add(new CacheInstruction.ToGroupNode(c.Group.Nodes.IndexOf(c)));
                revInds.Add(new CacheInstruction.ToGroup(null, c.Screen.Groups.IndexOf(c.Group)));
                var (g_, c_) = groupStack.Pop();
                c = c_ ?? g_.EntryNode;
            } else if (screenStack.TryPeek(out _)) {
                revInds.Add(new CacheInstruction.ToGroupNode(c.Group.Nodes.IndexOf(c)));
                revInds.Add(new CacheInstruction.ToGroup(Screens.IndexOf(c.Screen), c.Screen.Groups.IndexOf(c.Group)));
                c = screenStack.Pop();
            }  else {
                revInds.Add(new CacheInstruction.ToGroupNode(c.Group.Nodes.IndexOf(c)));
                c = null;
            }
        }
        revInds.Reverse();
        return revInds;
    }
    public void TentativeCache(UINode node) {
        tentativeReturnTo = GetInstructionsToNode(node);
    }

    public void ConfirmCache() {
        if (tentativeReturnTo != null)
            Logs.Log($"Caching menu position with indices " +
                     $"{string.Join(", ", tentativeReturnTo.Select(x => x.ToString()))}");
        ReturnTo = tentativeReturnTo;
    }

    public override EngineState UpdateDuring => EngineState.MENU_PAUSE;
    protected bool RegularUpdateGuard => ETime.FirstUpdateForScreen && UpdatesEnabled;
    /// <summary>
    /// Returns true iff this menu is active and it is also the most high-priority active menu.
    /// Of all active menus, only the most high-priority one should handle input, so use this to gate input.
    /// </summary>
    protected bool IsActiveCurrentMenu => MenuActive && uiRenderer.IsHighestPriorityActiveMenu(this);

    protected virtual bool OpenOnInit => true;
    protected virtual Color BackgroundTint => new(0.17f, 0.05f, 0.20f);

    private UICommand? CurrentInputCommand =>
        UILeft ? UICommand.Left :
        UIRight ? UICommand.Right :
        UIUp ? UICommand.Up :
        UIDown ? UICommand.Down :
        UIConfirm ? UICommand.Confirm :
        UIBack ? UICommand.Back :
        null;

    //Note: it should be possible to use Awake instead of FirstFrame w.r.t UIDocument being instantiated, 
    // but many menus depend on binding to services,
    // and services are not reliably queryable until FirstFrame.
    public override void FirstFrame() {
        if (uiRenderer == null)
            uiRenderer = ServiceLocator.Find<UIBuilderRenderer>();
        var uid = GetComponent<UIDocument>();
        //higher sort order is more visible, so give them lower priority
        tokens.Add(uiRenderer.RegisterController(this, -(int)uid.sortingOrder));
        UIRoot = uid.rootVisualElement;
        UIContainer = UIRoot.Q("UIContainer");
        UISettings = uid.panelSettings;
        Build();
        uiRenderer.ApplyScrollHeightFix(UIRoot);
        UIRoot.style.display = OpenOnInit.ToStyle();
        UIRoot.style.opacity = OpenOnInit ? 1 : 0;
        UIRoot.style.width = new Length(100, LengthUnit.Percent);
        UIRoot.style.height = new Length(100, LengthUnit.Percent);
        tokens.Add(BackgroundOpacity.Subscribe(f => UIContainer.style.unityBackgroundImageTintColor = 
            BackgroundTint.WithA(f)));
        BackgroundOpacity.Push(0);
        if (OpenOnInit) {
            Open();
            DoReturn();
        }
    }

    private void DoReturn() {
        if (ReturnTo == null) return;
        if (Current == null)
            throw new Exception("ReturnTo exists, but Current is null");
        foreach (var inst in ReturnTo) {
            switch (inst) {
                case CacheInstruction.ToGroup toGroup:
                    OperateOnResult(new UIResult.GoToNode(
                        toGroup.ScreenIndex == null ? 
                            Current.Group.Screen.Groups[toGroup.GroupIndex].EntryNode :
                            Screens[toGroup.ScreenIndex.Value]!.Groups[toGroup.GroupIndex].EntryNode), false);
                    break;
                case CacheInstruction.ToGroupNode toGroupNode:
                    OperateOnResult(new UIResult.GoToNode(Current.Group.Nodes[toGroupNode.NodeIndex]), false);
                    break;
                case CacheInstruction.ToOption toOption:
                    if (Current is IOptionNodeLR opt) {
                        opt.Index = toOption.OptionIndex;
                    } else
                        throw new Exception("Couldn't rebuild menu position: node is not an option");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(inst));
            }
        }
        ReturnTo = null;
    }

    private void Build() {
        foreach (var s in Screens)
            if (s != null)
                UIContainer.Add(s.Build(GameManagement.References.uxmlDefaults.TypeMap));
    }


    /// <summary>
    /// Redraws the current screen, or disables the UI display if there is no current node.
    /// <br/>Other screens are not affected.
    /// </summary>
    public void Redraw() {
        if (Current == null) {
            UIRoot.style.display = DisplayStyle.None;
            return;
        }
        Profiler.BeginSample("UI Redraw");
        UIRoot.style.display = DisplayStyle.Flex;
        var states = new Dictionary<UINode, UINodeVisibility>();
        foreach (var (_, node) in GroupCall)
            if (node != null)
                states[node] = UINodeVisibility.GroupCaller;
        foreach (var n in Current.Group.Nodes)
            states[n] = UINodeVisibility.GroupFocused;
        states[Current] = UINodeVisibility.Focused;
        //Other screens don't need to be redrawn
        var dependentGroups = Current.Group.Hierarchy.SelectMany(g => g.DependentGroups).ToHashSet();
        foreach (var g in Current.Screen.Groups) {
            var fallback = dependentGroups.Contains(g) ? UINodeVisibility.GroupFocused : UINodeVisibility.Default;
            g.Redraw();
            foreach (var n in g.Nodes)
                n.Redraw(states.TryGetValue(n, out var s) ? s : fallback);
        }
        RunDroppableRIEnumerator(scrollToCurrent());
        Profiler.EndSample();
    }
    //Workaround for limitation that cannot ScrollTo to objects that have just been constructed or made visible
    private IEnumerator scrollToCurrent() {
        yield return null;
        while (!ETime.FirstUpdateForScreen) yield return null;
        Current?.ScrollTo();
    }

    //public List<(UINode?, UIResult, (UIGroup, UINode?)[])> queries = new();
    //public List<UINode> currents = new();
    private Task OperateOnResult(UIResult? result, bool animate) {
        if (result == null || result is UIResult.GoToNode gTo && gTo.Target == Current) 
            return Task.CompletedTask;
        var next = Current;
        List<UIGroup> LeftGroups = new();
        List<UIGroup> EnteredGroups = new();
        void PopGroupCall(UIGroup src, out (UIGroup group, UINode? node) dst) {
            LeftGroups.Add(src);
            dst = GroupCall.Pop();
        }
        foreach (var r in new[] { result }.Unroll()) {
            var prev = next;
            //queries.Add((prev, r, GroupCall.ToArray()));
            switch (r) {
                case UIResult.DestroyMenu:
                    next = null;
                    animate = false;
                    if (prev != null)
                        LeftGroups.Add(prev.Group);
                    LeftGroups.AddRange(GroupCall.Select(g => g.group));
                    GroupCall.Clear();
                    ScreenCall.Clear();
                    break;
                case UIResult.GoToNode goToNode:
                    if (goToNode.Target.Destroyed)
                        break;
                    if (goToNode.Target.Screen != prev?.Screen && prev != null) {
                        ScreenCall.Push(prev);
                        prev = null;
                    }
                    if (goToNode.Target.Group != prev?.Group) {  
                        var th = goToNode.Target.Group.Hierarchy;
                        var ch = prev?.Group.Hierarchy;
                        var intersection = UIGroupHierarchy.GetIntersection(ch, th);
                        var lastSource = (prev?.Group,prev);
                        //Pop until we reach the intersection
                        for (var x = ch; x != intersection; x = x.Parent) {
                            LeftGroups.Add(x!.Group);
                            lastSource = GroupCall.Pop();
                        }
                        //Push until we reach the target
                        foreach (var x in th.PrefixRemainder(intersection)) {
                            EnteredGroups.Add(x);
                            if (lastSource.Group != null)
                                GroupCall.Push(lastSource!);
                            lastSource = (x, x == goToNode.Target.Group ? goToNode.Target : 
                                (x.HasInteractableNodes ? 
                                    (x.Nodes.Contains(x.EntryNode) ? x.EntryNode : x.FirstInteractableNode) : 
                                    null));
                        }
                    }
                    next = goToNode.Target;
                    break;
                case UIResult.ReturnToGroupCaller:
                    next = null;
                    while (GroupCall.Count > 0 && next == null) {
                        PopGroupCall(prev!.Group, out var nextg);
                        next = nextg.node;
                    }
                    if (next == null)
                        throw new Exception("Return-to-group resulted in a null node");
                    break;
                case UIResult.ReturnToTargetGroupCaller rtg:
                    if (prev == null) throw new Exception("Current must be present for return-to-target op");
                    var ngroup = prev.Group;
                    while (GroupCall.TryPeek(out var n)) {
                        //Logs.Log($"RPopping from group stack {n.node?.Description()}", level:LogLevel.DEBUG1);
                        PopGroupCall(ngroup, out _);
                        (ngroup, next) = n;
                        if (n.group == rtg.Target)
                            break;
                    }
                    if (next == null)
                        throw new Exception("Return-to-target resulted in a null node");
                    break;
                case UIResult.ReturnToScreenCaller:
                    if (prev == null) throw new Exception("Current must be present for return-to-screen op");
                    var ngn = (prev.Group, (UINode?)prev);
                    if (ScreenCall.TryPop(out var s)) {
                        while (GroupCall.TryPeek(out var g) && (g.group.Screen == prev.Screen))
                            PopGroupCall(ngn.Group, out ngn);
                        LeftGroups.Add(ngn.Group);
                        next = s;
                    }
                    if (next == null)
                        throw new Exception("Return-to-screen resulted in a null node");
                    break;
            }
        }
        QueuedEvent = null;
        var leftAndEntered = LeftGroups.Intersect(EnteredGroups).ToHashSet();
        if (result is not UIResult.StayOnNode { Action : UIResult.StayOnNodeType.Silent or UIResult.StayOnNodeType.NoOp })
            return TransitionToNode(next, animate, 
                LeftGroups.Except(leftAndEntered).ToList(), 
                EnteredGroups.Except(leftAndEntered).ToList()).ContinueWithSync(() => { });
        return Task.CompletedTask;
    }
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (ETime.FirstUpdateForScreen && Current != null) {
            UIVisualUpdateEv.OnNext(ETime.ASSUME_SCREEN_FRAME_TIME);
            BackgroundOpacity.Update(ETime.ASSUME_SCREEN_FRAME_TIME);
        }
        
        if (RegularUpdateGuard && Current != null && IsActiveCurrentMenu) {
            bool doCustomSFX = false;
            UICommand? command = null;
            UIResult? result = null;
            bool silence = false;
            if (QueuedEvent is UIPointerCommand.Goto mgt && mgt.Target.Destroyed)
                QueuedEvent = null;
            if (QueuedEvent is UIPointerCommand.Goto mouseGoto && mouseGoto.Target.Screen == Current.Screen && 
                mouseGoto.Target != Current) {
                result = new UIResult.GoToNode(mouseGoto.Target);
                ServiceLocator.SFXService.Request(
                    mouseGoto.Target.Group == Current.Group ? upDownSound : leftRightSound);
            } else if (Current.CustomEventHandling().Try(out var r)) {
                result = r;
                doCustomSFX = true;
            } else {
                command = (QueuedEvent is UIPointerCommand.NormalCommand uic && QueuedEvent.ValidForCurrent(Current)) ?
                    uic.Command :
                    CurrentInputCommand;
                if (command.Try(out var cmd))
                    result = Current.Navigate(cmd);
                silence = (QueuedEvent as UIPointerCommand.NormalCommand)?.Silent ?? silence;
            }
            if (result is UIResult.GoToNode gTo && gTo.Target == Current)
                result = null;
            if (result != null && !silence) {
                ServiceLocator.SFXService.Request(
                    result switch {
                        UIResult.StayOnNode{Action: UIResult.StayOnNodeType.NoOp} => failureSound,
                        UIResult.StayOnNode{Action: UIResult.StayOnNodeType.Silent} => null,
                        _ => command switch {
                            UICommand.Left => leftRightSound,
                            UICommand.Right => leftRightSound,
                            UICommand.Up => upDownSound,
                            UICommand.Down => upDownSound,
                            UICommand.Confirm => confirmSound,
                            UICommand.Back => backSound,
                            _ => null
                        }});
            } else if (doCustomSFX)
                //Probably need to get this from the custom node handling? idk
                ServiceLocator.SFXService.Request(leftRightSound);
            OperateOnResult(result, true);
        }
    }
    
    #region Transition

    public void GoToNth(int grpIndex, int nodeIndex) =>
        OperateOnResult(new UIResult.GoToNode(MainScreen.Groups[grpIndex].Nodes[nodeIndex]), false);
    protected Task Open() {
        if (StartingNode != null)
            return OperateOnResult(new UIResult.GoToNode(StartingNode), false);
        else {
            foreach (var g in Screens.First(x => x != null)!.Groups) 
                if (g.HasEntryNode) 
                    return OperateOnResult(new UIResult.GoToNode(g.EntryNode), false);
            throw new Exception($"Couldn't open menu {gameObject.name}");
        }
    }

    protected Task Close() => OperateOnResult(new UIResult.DestroyMenu(), false);

    private async Task TransitionToNode(UINode? next, bool animate, List<UIGroup> leftGroups, List<UIGroup> enteredGroups) {
        var prev = Current;
        if (prev == next) {
            //Cases like text input
            Redraw();
            return;
        }
        var screenChanged = next?.Screen != prev?.Screen;
        //currents.Add(null!);
        using var token = UpdatesEnabled.AddConst(false);
        prev?.Leave(animate);
        await Task.WhenAll(leftGroups.Select(l => l.LeaveGroup()));
        if (screenChanged) {
            prev?.Screen.ExitStart();
            next?.Screen.EnterStart(prev == null);
        }
        var enterTasks = new List<Task>();
        //Doing this explicitly causes EnterGroup>EnterShow to be run for all enter groups immediately
        foreach (var g in enteredGroups)
            if (g.EnterGroup().Try(out var t))
                enterTasks.Add(t);
        
        Current = next;
        next?.Enter(animate);
        Redraw();
        
        if (screenChanged && next != null)
            enterTasks.Add(Transition(prev?.Screen, next.Screen, animate));
        await Task.WhenAll(enterTasks);

        if (screenChanged) {
            prev?.Screen.ExitEnd();
            next?.Screen.EnterEnd();
        }
        if (next == null) {
            ScreenCall.Clear();
            GroupCall.Clear();
        }
    }

    public virtual async Task Transition(UIScreen? from, UIScreen to, bool animate) {
        if (from == null || !animate) {
            if (from != null) {
                from.HTML.transform.position = Vector3.zero;
                from.HTML.style.opacity = 0;
                if (from.SceneObjects != null)
                    from.SceneObjects.transform.position = Vector3.zero;
            }
            to.HTML.transform.position = Vector3.zero;
            to.HTML.style.opacity = 1;
            if (to.SceneObjects != null)
                to.SceneObjects.transform.position = Vector3.zero;
            return;
        }
        var t = 0.4f;
        var ep = GetRandomSlideEndpoint();
        var epxml = new Vector2(ep.x * XMLWidth, ep.y * -XMLHeight);
        var eput = new Vector2(ep.x * MainCamera.ScreenWidth, ep.y * MainCamera.ScreenHeight);
        //to.HTML.transform.position = -epxml;
        if (to.SceneObjects != null)
            to.SceneObjects.transform.position = -eput;
        to.HTML.style.opacity = 0;
        async Task FadeIn() {
            await SM.WaitingUtils.WaitForUnchecked(this, Cancellable.Null, t / 2f, false);
            await Task.WhenAll(
                to.HTML.FadeTo(1, t, M.EIOSine).Run(this),
                to.SceneObjects == null ?
                    Task.CompletedTask :
                    to.SceneObjects.transform.GoTo(Vector3.zero, t, M.EIOSine).Run(this)
            );
        }
        await Task.WhenAll(
            from.HTML.FadeTo(0, t, M.EIOSine).Run(this),
            from.SceneObjects == null ? Task.CompletedTask : 
                from.SceneObjects.transform.GoTo(eput, t, M.EIOSine).Run(this),
            FadeIn()
        );
    }
    
    
    private Vector2[] slideEndpoints => new[] {
        new Vector2(-1, 0),
        new Vector2(1, 0),
        new Vector2(0, -1),
        new Vector2(0, 1)
    };

    private Vector2 GetRandomSlideEndpoint() {
        return RNG.RandSelectOffFrame(slideEndpoints);
    }
    
    #endregion

    /// <summary>
    /// If a node dynamically becomes passthrough/invisible/deleted,
    ///  or for any other reason needs to ensure that it is not the current node,
    ///  it should call this function.
    /// </summary>
    public void MoveCursorAwayFromNode(UINode n) {
        if (n == Current)
            Current = n.Group.ExitNode;
        Redraw();
    }
    
    
    [ContextMenu("Debug group call stack")]
    public void DebugGroupCallStack() => Logs.Log(string.Join("; ", 
        GroupCall.Select(gn => $"{gn.node?.IndexInGroup}::{gn.group}")));
    [ContextMenu("Debug group hierarchy")]
    public void DebugGroupHierarcy() => Logs.Log(Current?.Group.ToString() ?? "No current node");
}
}