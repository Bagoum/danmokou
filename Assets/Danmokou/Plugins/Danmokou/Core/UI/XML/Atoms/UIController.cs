using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
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
    Back,
    ContextMenu,
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
    /// <summary>
    /// If this UIResult results in a transition between nodes or between screens, then this callback is invoked
    ///  after the transition is complete.
    /// </summary>
    public Action? OnPostTransition { get; init; } = null;
    
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

    public record GoToNode(UINode Target, bool NoOpIfSameNode = true) : UIResult {
        public GoToNode(UIGroup Group, int? Index = null) : 
            this(Index.Try(out var i) ? Group.Nodes[Math.Clamp(i, 0, Group.Nodes.Count-1)] : Group.EntryNode) {}
        
        public GoToNode(UIScreen s) : this(s.Groups[0]) { }
    }

    //Note: this is effectively the same as GoToNode, except you can add ReturnToOverride, which replaces
    // the screen caller with a different node.
    public record GoToScreen(UIScreen Screen, UINode? ReturnToOverride = null) : UIResult;

    public record ReturnToGroupCaller : UIResult;

    public record ReturnToTargetGroupCaller(UIGroup Target) : UIResult {
        public ReturnToTargetGroupCaller(UINode node) : this(node.Group) { }
    }

    public record ReturnToScreenCaller(int Ascensions = 1) : UIResult;

    public static implicit operator UIResult(UINode node) => new GoToNode(node);
    public static implicit operator UIResult(UIGroup group) => new GoToNode(group);
}

public abstract class UIController : CoroutineRegularUpdater {
    public static readonly CoroutineOptions AnimOptions = new(true, CoroutineType.StepTryPrepend);
    public static readonly Event<Unit> UIEventQueued = new();
    public abstract record CacheInstruction {
        public record ToOption(int OptionIndex) : CacheInstruction;
        public record ToGroup(int? ScreenIndex, int GroupIndex) : CacheInstruction;

        public record ToGroupNode(int NodeIndex) : CacheInstruction;
    }

    /// <summary>
    /// True if the UI HTML has been built (in FirstFrame)
    /// </summary>
    protected bool Built { get; private set; } = false;
    
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
        new(0.5f, (a, b, t) => M.Lerp(a, b, Easers.EIOSine(t)));
    public UIScreen MainScreen { get; protected set; } = null!;
    public DisturbedAnd UpdatesEnabled { get; } = new();
    public DisturbedAnd TransitionEnabled { get; } = new();
    public Stack<UINode> ScreenCall { get; } = new();
    public Stack<(UIGroup group, UINode? node)> GroupCall { get; } = new();
    
    public UINode? Current { get; private set; }
    public OverrideEvented<ICursorState> CursorState { get; } = new(new NullCursorState());

    //Fields for event-based changes 
    //Not sure if I want to generalize these to properly event-based...
    public UIPointerCommand? QueuedEvent { get; private set; }

    public void QueueEvent(UIPointerCommand cmd) {
        QueuedEvent = cmd;
        UIEventQueued.OnNext(default);
    }

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
    public SFXConfig? showOptsSound;
    
    public bool MenuActive => Current != null;
    //TODO what happens if you screw with the 16x9 frame? how would you measure this in non-ideal conditions?
    public int XMLWidth => UISettings.referenceResolution.x;
    public int XMLHeight => UISettings.referenceResolution.y;

    protected List<CacheInstruction> GetInstructionsToNode(UINode? c) {
        var revInds = new List<CacheInstruction>();
        var groupStack = new Stack<(UIGroup group, UINode? node)>(GroupCall.Reverse());
        var screenStack = new Stack<UINode>(ScreenCall.Reverse());
        void GoToThisNode() {
            revInds.Add(new CacheInstruction.ToGroupNode(c.IndexInGroup));
            for (int ii = c.Group.Nodes.Count - 1; ii >= 0; --ii)
                if (c.Group.Nodes[ii] is IBaseLROptionNode opt) {
                    revInds.Add(new CacheInstruction.ToOption(opt.Index));
                    revInds.Add(new CacheInstruction.ToGroupNode(((UINode)opt).IndexInGroup));
                }
        }
        while (c != null) {
            if ((groupStack.TryPeek(out var g) && (g.group.Screen == c.Screen))) {
                GoToThisNode();
                revInds.Add(new CacheInstruction.ToGroup(null, c.Screen.Groups.IndexOf(c.Group)));
                var (g_, c_) = groupStack.Pop();
                c = c_ ?? g_.EntryNode;
            } else if (screenStack.TryPeek(out _)) {
                GoToThisNode();
                revInds.Add(new CacheInstruction.ToGroup(Screens.IndexOf(c.Screen), c.Screen.Groups.IndexOf(c.Group)));
                c = screenStack.Pop();
            }  else {
                GoToThisNode();
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
        UIContextMenu ? UICommand.ContextMenu :
        null;

    //Note: it should be possible to use Awake instead of FirstFrame w.r.t UIDocument being instantiated, 
    // but many menus depend on binding to services,
    // and services are not reliably queryable until FirstFrame.
    public override void FirstFrame() {
        if (uiRenderer == null)
            uiRenderer = ServiceLocator.Find<UIBuilderRenderer>();
        var uid = GetComponent<UIDocument>();
        //higher sort order is more visible, so give them lower priority
        tokens.Add(uiRenderer.RegisterController(this, -(int)(uid.panelSettings.sortingOrder * 1000 + uid.sortingOrder)));
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
        tokens.Add(UpdatesEnabled.AddDisturbance(TransitionEnabled));
        if (OpenOnInit) {
            _ = ((Func<Task>)(async () => {
                await Open();
                await DoReturn();
            }))().ContinueWithSync();
        }
    }

    private async Task DoReturn() {
        if (ReturnTo == null) return;
        if (Current == null)
            throw new Exception("ReturnTo exists, but Current is null");
        foreach (var inst in ReturnTo) {
            switch (inst) {
                case CacheInstruction.ToGroup toGroup:
                    await OperateOnResult(new UIResult.GoToNode(
                        toGroup.ScreenIndex == null ? 
                            Current.Group.Screen.Groups[toGroup.GroupIndex].EntryNode :
                            Screens[toGroup.ScreenIndex.Value]!.Groups[toGroup.GroupIndex].EntryNode), null);
                    break;
                case CacheInstruction.ToGroupNode toGroupNode:
                    await OperateOnResult(new UIResult.GoToNode(Current.Group.Nodes[toGroupNode.NodeIndex]), null);
                    break;
                case CacheInstruction.ToOption toOption:
                    if (Current is IBaseLROptionNode opt) {
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
                UIContainer.Add(s.Build(XMLUtils.Prefabs.TypeMap));
        Built = true;
    }
    protected void BuildLate(UIScreen s) => 
        UIContainer.Add(s.Build(XMLUtils.Prefabs.TypeMap));
    


    /// <summary>
    /// Redraws the current screen, or disables the UI display if there is no current node.
    /// <br/>Other screens are not affected.
    /// </summary>
    public void Redraw() {
        //This can occur if this gets called before FirstFrame
        if (UIRoot == null) return;
        if (Current == null) {
            UIRoot.style.display = DisplayStyle.None;
            return;
        }
        Profiler.BeginSample("UI Redraw");
        UIRoot.style.display = DisplayStyle.Flex;
        var states = new Dictionary<UINode, UINodeSelection>();
        var nextIsPopupSrc = Current.Group is PopupUIGroup;
        foreach (var (grp, node) in GroupCall) {
            if (node != null)
                states[node] = nextIsPopupSrc ? UINodeSelection.PopupSource : UINodeSelection.GroupCaller;
            nextIsPopupSrc = grp is PopupUIGroup;
        }
        foreach (var n in Current.Group.Nodes)
            states[n] = UINodeSelection.GroupFocused;
        states[Current] = UINodeSelection.Focused;
        //Other screens don't need to be redrawn
        var dependentGroups = Current.Group.Hierarchy.SelectMany(g => g.DependentGroups).ToHashSet();
        foreach (var g in Current.Screen.Groups) {
            var fallback = dependentGroups.Contains(g) ? UINodeSelection.GroupFocused : UINodeSelection.Default;
            foreach (var n in g.Nodes)
                n.UpdateSelection(states.TryGetValue(n, out var s) ? s : fallback);
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
    
    
    private Task _OperateOnResult(UIResult? result, UITransitionOptions? opts) {
        if (result == null || (result is UIResult.GoToNode {NoOpIfSameNode:true} gTo && gTo.Target == Current) ||
            result is UIResult.StayOnNode) {
            if (result != null) Redraw();
            return Task.CompletedTask;
        } 
        opts ??= UITransitionOptions.DontAnimate;
        var next = Current;
        List<(UIGroup group, UINode? node)> LeftCalls = new();
        List<(UIGroup group, UINode? node)> EntryCalls = new();
        foreach (var r in new[] { result }.Unroll()) {
            var prev = next;
            void TransferToNodeSameScreen(UINode target) {
                if (target.Group != prev?.Group) {  
                    var th = target.Group.Hierarchy;
                    var ch = prev?.Group.Hierarchy;
                    var intersection = UIGroupHierarchy.GetIntersection(ch, th);
                    var lastSource = (group: prev?.Group!, node: prev);
                    //Pop until we reach the intersection
                    for (var x = ch; x != intersection; x = x!.Parent) {
                        if (lastSource.group != null)
                            LeftCalls.Add(lastSource);
                        lastSource = GroupCall.Pop();
                    }
                    //If the intersection is the target group, then add an extra LeftCall and EntryCall for the node
                    // being swapped out at the intersection, so RemovedFromGroupStack can be called properly
                    if (th == intersection) {
                        if (lastSource.node != target) {
                            LeftCalls.Add(lastSource);
                            EntryCalls.Add((lastSource.group, target));
                        }
                    } else
                        //Push until we reach the target
                        foreach (var x in th.PrefixRemainder(intersection)) {
                            if (lastSource.group != null)
                                GroupCall.Push(lastSource);
                            lastSource = (x, x == target.Group ? target : 
                                (x.HasInteractableNodes ? 
                                    (x.Nodes.Contains(x.EntryNode) ? x.EntryNode : x.FirstInteractableNode) : 
                                    null));
                            EntryCalls.Add(lastSource);
                        }
                }
                next = target;
            }
            switch (r) {
                case UIResult.DestroyMenu:
                    next = null;
                    opts = UITransitionOptions.DontAnimate;
                    if (prev != null)
                        LeftCalls.Add((prev.Group, prev));
                    LeftCalls.AddRange(GroupCall);
                    GroupCall.Clear();
                    ScreenCall.Clear();
                    break;
                case UIResult.GoToScreen goToScreen:
                    if (goToScreen.Screen != prev?.Screen && (goToScreen.ReturnToOverride ?? prev) is { } returnTo) {
                        ScreenCall.Push(returnTo);
                        prev = null;
                        TransferToNodeSameScreen(goToScreen.Screen.Groups[0].EntryNode);
                    }
                    break;
                case UIResult.GoToNode goToNode:
                    if (goToNode.Target.Destroyed)
                        break;
                    if (goToNode.Target.Screen != prev?.Screen && prev != null) {
                        ScreenCall.Push(prev);
                        prev = null;
                    }
                    TransferToNodeSameScreen(goToNode.Target);
                    break;
                case UIResult.ReturnToGroupCaller:
                    next = null;
                    while (GroupCall.Count > 0 && next == null) {
                        LeftCalls.Add((prev!.Group, prev));
                        next = GroupCall.Pop().node;
                    }
                    if (next == null)
                        throw new Exception("Return-to-group resulted in a null node");
                    break;
                case UIResult.ReturnToTargetGroupCaller rtg:
                    if (prev == null) throw new Exception("Current must be present for return-to-target op");
                    var pn = (prev.Group, prev);
                    while (GroupCall.TryPop(out var n)) {
                        LeftCalls.Add(pn);
                        pn = n!;
                        next = n.node;
                        if (n.group == rtg.Target)
                            break;
                    }
                    if (next == null)
                        throw new Exception("Return-to-target resulted in a null node");
                    if (next.Destroyed)
                        next = pn.Group.EntryNode;
                    break;
                case UIResult.ReturnToScreenCaller sc:
                    if (prev == null) throw new Exception("Current must be present for return-to-screen op");
                    var ngn = (prev.Group, (UINode?)prev);
                    for (int ii = 0; ii < sc.Ascensions; ++ii)
                        if (ScreenCall.TryPop(out var s)) {
                            while (GroupCall.TryPeek(out var g) && (g.group.Screen == prev.Screen)) {
                                LeftCalls.Add(ngn);
                                ngn = GroupCall.Pop();
                            }
                            LeftCalls.Add(ngn);
                            ngn = (s.Group, next = s);
                        }
                    if (next == null)
                        throw new Exception("Return-to-screen resulted in a null node");
                    break;
            }
        }

        var leftGroups = new List<UIGroup>();
        foreach (var (g, n) in LeftCalls) {
            foreach (var (ge, ne) in EntryCalls) {
                if (g == ge) {
                    if (n != ne)
                        n?.RemovedFromGroupStack();
                    goto end;
                }
            }
            n?.RemovedFromGroupStack();
            leftGroups.Add(g);
            end: ;
        }
        var entryGroups = EntryCalls.Select(c => c.group).Except(LeftCalls.Select(c => c.group)).ToList();
        //Logs.Log($"Left: {string.Join(",", LeftCalls.Select(x => x.ToString()))}, Entered: {string.Join(",", EntryCalls.Select(x => x.ToString()))}");
        //Logs.Log($"Left: {string.Join(",", leftGroups.Select(x => x.ToString()))}, Entered: {string.Join(",", entryGroups.Select(x => x.ToString()))}");
        return TransitionToNode(next, opts, leftGroups, entryGroups)
            .ContinueWithSync(result.OnPostTransition);
    }

    public Task OperateOnResult(UIResult? result, UITransitionOptions? opts) {
        if (uiOperations.Count == 0 && TransitionEnabled)
            return _OperateOnResult(result, opts);
        var tcs = new TaskCompletionSource<Unit>();
        uiOperations.Enqueue(() => _OperateOnResult(result, opts).Pipe(tcs));
        return tcs.Task;
    }
    public override void RegularUpdate() {
        base.RegularUpdate();
        if (ETime.FirstUpdateForScreen && Current != null) {
            UIVisualUpdateEv.OnNext(ETime.ASSUME_SCREEN_FRAME_TIME);
            BackgroundOpacity.Update(ETime.ASSUME_SCREEN_FRAME_TIME);
        }

        while (ETime.FirstUpdateForScreen) {
            if (TransitionEnabled && uiOperations.TryDequeue(out var nxt))
                _ = nxt();
            else if (UpdatesEnabled && IsActiveCurrentMenu && Current != null) {
                bool doCustomSFX = false;
                UICommand? command = null;
                UIResult? result = null;
                bool silence = false;
                if (QueuedEvent is UIPointerCommand.Goto mgt) {
                    if (!mgt.Target.Destroyed && mgt.Target.Screen == Current.Screen &&
                        mgt.Target != Current && UIGroupHierarchy.CanTraverse(Current, mgt.Target)) {
                        result = CursorState.Value.PointerGoto(Current, mgt.Target);
                        if (result is UIResult.GoToNode { Target: {} target} && target != Current)
                            command = (Current.Group is UIColumn) == (target.Group == Current.Group) ?
                                UICommand.Up : UICommand.Right;
                    }
                } else if (CursorState.Value.CustomEventHandling(Current).Try(out var r)) {
                    result = r;
                    doCustomSFX = true;
                } else {
                    command =
                        (QueuedEvent is UIPointerCommand.NormalCommand uic && QueuedEvent.ValidForCurrent(Current)) ?
                            uic.Command :
                            CurrentInputCommand;
                    if (command.Try(out var cmd))
                        result = CursorState.Value.Navigate(Current, cmd);
                    silence = (QueuedEvent as UIPointerCommand.NormalCommand)?.Silent ?? silence;
                }
                QueuedEvent = null;
                if (result is UIResult.GoToNode gTo && gTo.Target == Current)
                    result = new UIResult.StayOnNode(gTo.NoOpIfSameNode);
                if (result != null && !silence) {
                    ISFXService.SFXService.Request(
                        result switch {
                            UIResult.StayOnNode { Action: UIResult.StayOnNodeType.NoOp } => failureSound,
                            UIResult.StayOnNode { Action: UIResult.StayOnNodeType.Silent } => null,
                            _ => command switch {
                                UICommand.Left => leftRightSound,
                                UICommand.Right => leftRightSound,
                                UICommand.Up => upDownSound,
                                UICommand.Down => upDownSound,
                                UICommand.Confirm => confirmSound,
                                UICommand.Back => backSound,
                                UICommand.ContextMenu => showOptsSound,
                                _ => null
                            }
                        });
                } else if (doCustomSFX)
                    //Probably need to get this from the custom node handling? idk
                    ISFXService.SFXService.Request(leftRightSound);
                if (result != null && result is not UIResult.StayOnNode)
                    OperateOnResult(result, UITransitionOptions.Default).ContinueWithSync();
                break;
            } else
                break;
        }
    }
    
    #region Transition

    public void GoToNth(int grpIndex, int nodeIndex) =>
        OperateOnResult(new UIResult.GoToNode(MainScreen.Groups[grpIndex].Nodes[nodeIndex]), null);
    protected Task Open() {
        if (StartingNode != null)
            return OperateOnResult(new UIResult.GoToNode(StartingNode), null);
        else {
            foreach (var g in Screens.First(x => x != null)!.Groups) 
                if (g.HasEntryNode) 
                    return OperateOnResult(new UIResult.GoToNode(g.EntryNode), null);
            throw new Exception($"Couldn't open menu {gameObject.name}");
        }
    }

    protected Task Close() => OperateOnResult(new UIResult.DestroyMenu(), null);

    private readonly Queue<Func<Task>> uiOperations = new();
    private async Task TransitionToNode(UINode? next, UITransitionOptions opts, List<UIGroup> leftGroups, List<UIGroup> enteredGroups) {
        var prev = Current;
        if (prev == next) {
            return;
        }
        var screenChanged = next?.Screen != prev?.Screen;
        using var token = TransitionEnabled.AddConst(false);
        prev?.Leave(opts.Animate, CursorState.Value, leftGroups.Count == 0 && enteredGroups.Try(0) is PopupUIGroup);
        var tasks = new List<Task>();
        foreach (var g in leftGroups)
            if (g.LeaveGroup() is { IsCompletedSuccessfully: false} task)
                tasks.Add(task);
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
        if (screenChanged) {
            prev?.Screen.ExitStart();
            next?.Screen.EnterStart(prev == null);
        }
        tasks.Clear();
        foreach (var g in enteredGroups)
            if (g.EnterGroup() is { IsCompletedSuccessfully: false} task)
                tasks.Add(task);
        
        Current = next;
        next?.Enter(opts.Animate, CursorState.Value);
        Redraw();
        
        if (screenChanged && next != null)
            tasks.Add(TransitionScreen(prev?.Screen, next.Screen, opts));
        if (tasks.Count > 0)
            await Task.WhenAll(tasks);

        if (screenChanged) {
            prev?.Screen.ExitEnd();
            next?.Screen.EnterEnd();
        }
        if (next == null) {
            ScreenCall.Clear();
            GroupCall.Clear();
            //uiOperations.Clear();
        }
    }

    public virtual async Task TransitionScreen(UIScreen? from, UIScreen to, UITransitionOptions opts) {
        if (from == null || !opts.Animate) {
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
        var t = opts.ScreenTransitionTime;
        var ep = GetRandomSlideEndpoint();
        var epxml = new Vector2(ep.x * XMLWidth, ep.y * -XMLHeight);
        var eput = new Vector2(ep.x * MainCamera.ScreenWidth, ep.y * MainCamera.ScreenHeight);
        //to.HTML.transform.position = -epxml;
        if (to.SceneObjects != null)
            to.SceneObjects.transform.position = -eput;
        to.HTML.style.opacity = 0;
        async Task FadeIn() {
            if (opts.DelayScreenFadeIn)
                await RUWaitingUtils.WaitForUnchecked(this, Cancellable.Null, t / 2f, false);
            await Task.WhenAll(
                to.HTML.FadeTo(1, t, Easers.EIOSine).Run(this),
                to.SceneObjects == null ?
                    Task.CompletedTask :
                    to.SceneObjects.transform.GoTo(Vector3.zero, t, Easers.EIOSine).Run(this)
            );
        }
        await Task.WhenAll(
            from.HTML.FadeTo(0, opts.ScreenTransitionTime, Easers.EIOSine).Run(this),
            from.SceneObjects == null ? Task.CompletedTask : 
                from.SceneObjects.transform.GoTo(eput, opts.ScreenTransitionTime, Easers.EIOSine).Run(this),
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
    /// <returns>True iff the cursor was moved (which also redraws the screen).</returns>
    public bool MoveCursorAwayFromNode(UINode n) {
        if (n == Current) {
            if (!n.Destroyed) n.Leave(false, CursorState.Value, false);
            Current = n.Group.ExitNode;
            Redraw();
            return true;
        } else
            return false;
    }

    public Task PlayAnimation(ITransition anim) => anim.Run(this, UIController.AnimOptions);
    
    public override int UpdatePriority => UpdatePriorities.UI;

    protected override void OnDisable() {
        foreach (var s in Screens) {
            s?.MarkScreenDestroyed();
        }
        base.OnDisable();
    }


    [ContextMenu("Debug group call stack")]
    public void DebugGroupCallStack() => Logs.Log(string.Join("; ", 
        GroupCall.Select(gn => $"{gn.node?.IndexInGroup}::{gn.group}")));
    [ContextMenu("Debug group hierarchy")]
    public void DebugGroupHierarcy() => Logs.Log(Current?.Group.ToString() ?? "No current node");
}
}