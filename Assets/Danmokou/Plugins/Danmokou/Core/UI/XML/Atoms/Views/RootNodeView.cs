using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using Danmokou.Core;
using Danmokou.DMath;
using Unity.Properties;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
/// <summary>
/// A view/model for basic node handling, such as turning the node display on/off, animating the node on focus,
///  and adding CSS classes based on the node focus.
/// </summary>
public class RootNodeViewModel : UIViewModel {
    public UINode Node { get; }
    public LString? Description { get; }
    public Func<long>? NodeIsVisibleHash { get; set; }
    public Func<long>? NodeIsEnabledHash { get; set; }
    
    public RootNodeViewModel(UINode node, LString? description) {
        Node = node;
        Description = description;
    }
    public override long GetViewHash() {
        Profiler.BeginSample("RootNodeView hash computation");
        var hc = (Node.Selection, 
            NodeIsEnabledHash?.Invoke() ?? (Node.IsEnabled ? 1 : 0), 
            NodeIsVisibleHash?.Invoke() ?? (Node.IsVisible ? 1 : 0), 
            Node.Render.ShouldBeVisibleInTree).GetHashCode();
        Profiler.EndSample();
        return hc;
    }
}

/// <inheritdoc cref="RootNodeViewModel"/>
public class RootNodeView : UIView<RootNodeViewModel>, IUIView {
    private Cancellable? enterLeaveAnim;
    /// <summary>
    /// Animation played when focus is placed on the node. Defaults to <see cref="DefaultEnterAnimation"/>.
    /// </summary>
    public Func<UINode, ICancellee, Task?>? EnterAnimation { get; set; } = DefaultEnterAnimation;

    /// <summary>
    /// Animation played when focus is removed from the node. Defaults to null.
    /// </summary>
    public Func<UINode, ICancellee, Task?>? LeaveAnimation { get; set; } = null;
    
    /// <summary>
    /// Animation played when the node is first rendered. Defaults to null.
    /// </summary>
    public Func<UINode, ICancellee, Task>? OnFirstRenderAnimation { get; set; }
    public RootNodeView(UINode node, LString? description = null) : base(new(node, description)) { }

    //Normally we don't need to apply update CSS immediately after building the node,
    // but since there's a transitio on on node.opacity, a "fade in" effect will occur if we allow
    // UITK to apply the initial visibility class at the end of the frame.
    public override void OnBuilt(UINode node) {
        base.OnBuilt(node);
        ReprocessForLanguageChange();
    }

    public override void ReprocessForLanguageChange() {
        Update(default);
        UpdateLabel();
    }

    void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
        if (!animate || !EnterAnimation.Try(out var anim)) return;
        _ = anim(node, Cancellable.Replace(ref enterLeaveAnim))?.ContinueWithSync();
    }

    void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) {
        if (!animate || !LeaveAnimation.Try(out var anim)) return;
        _ = anim(node, Cancellable.Replace(ref enterLeaveAnim))?.ContinueWithSync();
    }


    protected override BindingResult Update(in BindingContext context) {
        var nh = Node.HTML;
        nh.EnableInClassList("focus", Node.Selection is UINodeSelection.Focused or UINodeSelection.PopupSource);
        nh.EnableInClassList("group", Node.Selection is UINodeSelection.GroupFocused);
        nh.EnableInClassList("selected", Node.Selection is UINodeSelection.GroupCaller);
        nh.EnableInClassList("visible", Node.Selection is UINodeSelection.Default);
        nh.EnableInClassList(disabledClass, !Node.IsEnabled);
        //If the render target is going invisible, then don't update visibility
        // (important for tooltip scale-out and other animation effects)
        if (IsFirstRender() || Node.Render.ShouldBeVisibleInTree) {
            var vis = Node.IsVisible;
            nh.EnableInClassList("invisible", !vis);
            if (vis && IsFirstVisibleRender()) {
                _ = OnFirstRenderAnimation?.Invoke(Node, Cancellable.Null).ContinueWithSync();
            }
        }
        return base.Update(in context);
    }

    public void UpdateLabel() {
        if (VM.Description?.Value is {} desc) {
            var label = Node.HTML.Q<Label>();
            if (label != null)
                label.text = desc;
        }
    }
    
    /// <summary>
    /// Turn off enter and leave animations on this node.
    /// </summary>
    public void DisableAnimations() => EnterAnimation = LeaveAnimation = null;
    
    /// <summary>
    /// Set an animation to be played when the node first renders.
    /// </summary>
    public void OnFirstRender(Func<UINode, ICancellee, ITransition> tweener) =>
        OnFirstRenderAnimation = (n, cT) => 
            Node.Controller.PlayAnimation(tweener(n, cT));
    
    public static readonly Func<UINode, ICancellee, Task?> DefaultEnterAnimation = (n, cT) =>
        n.Controller.PlayAnimation(
            n.HTML.transform.ScaleTo(1.02f, 0.1f, Easers.EOutSine, cT)
            .Then(() => n.HTML.transform.ScaleTo(1f, 0.13f, cT: cT)));
}
}