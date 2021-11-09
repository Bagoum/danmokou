using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using UnityEngine.UIElements;

namespace Danmokou.UI.XML {

public class UIScreen {
    public List<UIGroup> Groups { get; } = new();
    //Don't need links to RenderSpaces
    protected UINode[] top;
    public virtual UINode[] Top => top;
    public UINode First => Top[0];
    public UIScreen? calledBy { get; private set; }
    public UINode? lastCaller { get; private set; }
    public XMLMenu Container { get; }

    public UINode GoToNested(UINode caller, UINode target) {
        lastCaller = caller;
        target.screen.calledBy = this;
        target.screen.onEnter?.Invoke();
        onExit?.Invoke();
        return target;
    }

    public UINode StartingNode => lastCaller ?? top[0];
    public UINode? ExitNode { get; set; }

    public UINode? GoBack() {
        if (calledBy?.StartingNode != null) {
            onExit?.Invoke();
            calledBy.onEnter?.Invoke();
        }
        return calledBy?.StartingNode;
    }

    public void RunPreExit() => onPreExit?.Invoke();
    public void RunPreEnter() => onPreEnter?.Invoke();
    public void RunPostEnter() => onPostEnter?.Invoke();

    public UIScreen(XMLMenu container, params UINode?[] nodes) {
        Container = container;
        top = nodes.Where(x => x != null).ToArray()!;
        foreach (var n in top) n.Siblings = top;
        foreach (var n in ListAll()) n.screen = this;
    }

    public UINode[] AssignNewNodes(UINode[] nodes) {
        top = nodes;
        foreach (var l in Lists) l.Clear();
        foreach (var n in top) n.Siblings = top;
        foreach (var n in ListAll()) n.screen = this;
        BuildChildren();
        return top;
    }

    public UINode AddTopNode(UINode node) {
        top = top.Append(node).ToArray();
        foreach (var n in top) n.Siblings = top;
        foreach (var n in ListAll()) n.screen = this;
        BuildChild(node);
        return node;
    }

    public IEnumerable<UINode> ListAll() => top.SelectMany(x => x.ListAll());
    public bool HasNode(UINode x) => ListAll().Contains(x);

    public void ResetStates() {
        foreach (var n in ListAll()) n.state = NodeState.Invisible;
    }

    public void ApplyStates() {
        foreach (var n in ListAll()) n.ApplyState();
    }

    public VisualElement Bound { get; private set; } = null!;

    private List<ScrollView>? _lists;
    public List<ScrollView> Lists => _lists ??= Bound.Query<ScrollView>().ToList();
    private Dictionary<Type, VisualTreeAsset> buildMap = null!;

    public VisualElement Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        Bound = (overrideBuilder == null ? map[typeof(UIScreen)] : overrideBuilder).CloneTree();
        BuildChildren();
        return Bound;
    }
    private void BuildChildren() => ListAll().ForEach(BuildChild);
    public void BuildChild(UINode node) => node.Build(buildMap, Lists[node.Depth]);

    private VisualTreeAsset? overrideBuilder;

    public UIScreen With(VisualTreeAsset builder) {
        overrideBuilder = builder;
        return this;
    }
    
    private Action? onPreExit;
    /// <summary>
    /// This is run on exit transition start
    /// </summary>
    public UIScreen OnPreExit(Action cb) {
        onPreExit = cb;
        return this;
    }
    private Action? onExit;
    /// <summary>
    /// This is run at exit transition midpoint
    /// </summary>
    public UIScreen OnExit(Action cb) {
        onExit = cb;
        return this;
    }

    private Action? onEnter;
    /// <summary>
    /// This is run at entry transition midpoint
    /// </summary>
    public UIScreen OnEnter(Action cb) {
        onEnter = cb;
        return this;
    }
    private Action? onPreEnter;
    /// <summary>
    /// This is run on entry transition start
    /// </summary>
    public UIScreen OnPreEnter(Action cb) {
        onPreEnter = cb;
        return this;
    }
    private Action? onPostEnter;
    /// <summary>
    /// This is run on entry transition end
    /// </summary>
    public UIScreen OnPostEnter(Action cb) {
        onPostEnter = cb;
        return this;
    }
}


public class LazyUIScreen : UIScreen {
    private readonly Func<UINode[]> loader;

    public override UINode[] Top {
        get {
            if (top.Length == 0)
                AssignNewNodes(loader());
            return top;
        }
    }

    public LazyUIScreen(XMLMenu container, Func<UINode[]> loader) : base(container) {
        this.loader = loader;
    }
}

}