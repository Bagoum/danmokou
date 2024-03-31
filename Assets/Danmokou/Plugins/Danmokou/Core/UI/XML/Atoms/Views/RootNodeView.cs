using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Reflection;
using BagoumLib.Transitions;
using Danmokou.Core;
using Unity.Properties;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
public class RootNodeViewModel : UIViewModel {
    public UINode Node { get; }
    public Func<long>? NodeIsVisibleHash { get; set; }
    public Func<long>? NodeIsEnabledHash { get; set; }
    
    public RootNodeViewModel(UINode node) {
        Node = node;
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
public class RootNodeView : UIView<RootNodeViewModel> {
    /// <summary>
    /// Animation played when the node is first rendered. Defaults to null.
    /// </summary>
    public Func<UINode, ICancellee, Task>? OnFirstRenderAnimation { get; set; }
    public RootNodeView(UINode node) : base(new(node)) { }

    //Normally we don't need to apply update CSS immediately after building the node,
    // but since there's a transitio on on node.opacity, a "fade in" effect will occur if we allow
    // UITK to apply the initial visibility class at the end of the frame.
    public override void NodeBuilt(UINode node) {
        base.NodeBuilt(node);
        Update(default);
    }

    protected override BindingResult Update(in BindingContext context) {
        //If the render target is going invisible, then don't bother redrawing
        //(important for tooltip scale-out)
        if (Node.Render.ShouldBeVisibleInTree) {
            var nh = Node.NodeHTML;
            var vis = Node.IsVisible;
            nh.EnableInClassList("invisible", !vis);
            nh.EnableInClassList("focus", Node.Selection is UINodeSelection.Focused or UINodeSelection.PopupSource);
            nh.EnableInClassList("group", Node.Selection is UINodeSelection.GroupFocused);
            nh.EnableInClassList("selected", Node.Selection is UINodeSelection.GroupCaller);
            nh.EnableInClassList("visible", Node.Selection is UINodeSelection.Default);
            nh.EnableInClassList(disabledClass, !Node.IsEnabled);
            if (vis && IsFirstRender()) {
                _ = Node.PlayAnimation(OnFirstRenderAnimation);
            }
        }
        return base.Update(in context);
    }
    
    /// <summary>
    /// Set an animation to be played when the node first renders.
    /// </summary>
    public void OnFirstRender(Func<UINode, ICancellee, ITransition> tweener) =>
        OnFirstRenderAnimation = (n, cT) => tweener(n, cT).Run(Node.Controller, UIController.AnimOptions);
}
}