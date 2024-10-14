using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using UnityEngine;

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
        public Vector2? Loc { get; init; } = null;
        public override bool ValidForCurrent(UINode current) => 
            Source == current || Source == null || Command == UICommand.Back;
    }
    
    
    public record Goto(UINode Target) : UIPointerCommand;
}


public abstract record UIResult {
    public UITransitionOptions? Options { get; init; }
    
    /// <summary>
    /// If this UIResult results in a transition between nodes or between screens, then this callback is invoked
    ///  after the transition is complete.
    /// </summary>
    public Action? OnPostTransition { get; init; }
    
    /// <summary>
    /// Disable the UI display and set current to null. (This has no animation.)
    /// </summary>
    public record CloseMenuFast : UIResult;

    /// <summary>
    /// Animate a menu closed using CloseWithAnimation.
    /// </summary>
    public record CloseMenu : UIResult;

    public enum StayOnNodeType {
        DidSomething,
        NoOp,
        Silent
    }

    public SequentialResult Then(UIResult second) => new SequentialResult(this, second);

    public record SequentialResult(params UIResult[] results) : UIResult, IUnrollable<UIResult> {
        public IEnumerable<UIResult> Values => results;
    }

    public record AfterTask(Func<Task<UIResult>> Delayed) : UIResult;
    
    public record Lazy(Func<UIResult> Delayed) : UIResult;

    public static Lazy LazyGoBackFrom(UINode source)
        => new Lazy(() => source.Controller.Navigate(source, UICommand.Back));

    public record StayOnNode(StayOnNodeType Action) : UIResult {
        public StayOnNode(bool IsNoOp = false) : this(IsNoOp ? StayOnNodeType.NoOp : StayOnNodeType.DidSomething) { }
    }

    public record GoToNode(UINode Target, bool NoOpIfSameNode = true) : UIResult {
        public GoToNode(UIGroup Group, int? Index = null) : 
            this(Index.Try(out var i) ? Group.Nodes[Math.Clamp(i, 0, Group.Nodes.Count-1)] : Group.EntryNode) {}
    }

    public record GoToSibling(int Index, bool NoOpIfSameNode = true) : UIResult;

    //Note: this is effectively the same as GoToNode, except you can add ReturnToOverride, which replaces
    // the screen caller with a different node.
    public record GoToScreen(UIScreen Screen, UINode? ReturnToOverride = null) : UIResult;

    public record ReturnToGroupCaller(int Ascensions = 1) : UIResult;

    public record ReturnToTargetGroupCaller(UIGroup Target) : UIResult {
        public ReturnToTargetGroupCaller(UINode node) : this(node.Group) { }
    }

    public record ReturnToScreenCaller(int Ascensions = 1) : UIResult;

    public static implicit operator UIResult(UINode node) => new GoToNode(node);
    public static implicit operator UIResult(UIGroup group) => new GoToNode(group);

}


}