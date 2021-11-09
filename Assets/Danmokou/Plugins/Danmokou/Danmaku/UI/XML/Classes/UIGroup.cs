using System;
using System.Collections.Generic;
using System.Linq;
using Danmokou.DMath;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.UIResult;

namespace Danmokou.UI.XML {
public record UIGroup {
    public bool Visible { get; private set; } = false;
    public NUIScreen Screen { get; }
    public UIRenderSpace Render { get; }
    private List<NUINode>? _nodes;
    public Func<IEnumerable<NUINode>>? LazyNodes { get; init; }
    public List<NUINode> Nodes => _nodes ??= LazyNodes?.Invoke().ToList() ?? new List<NUINode>();
    public bool Interactable { get; init; } = true;
    public NUINode? EntryNode { get; set; }
    public NUINode? ExitNode { get; set; }
    private Dictionary<Type, VisualTreeAsset>? buildMap;

    public UIController Controller => Screen.Controller;
    public int EntryIndex {
        get {
            if (EntryNode != null)
                return Nodes.IndexOf(EntryNode);
            for (int ii = 0; ii < Nodes.Count; ++ii)
                if (!Nodes[ii].Passthrough)
                    return ii;
            throw new Exception("No valid entry nodes for UIGroup");
        }
    }

    public UIGroup(NUIScreen container, UIRenderSpace render, params NUINode[] nodes) {
        (Screen = container).AddGroup(this);
        (Render = render).AddGroup(this);
        if (nodes.Length > 0)
            _nodes = nodes.ToList();
    }

    public void Build(Dictionary<Type, VisualTreeAsset> map) {
        buildMap = map;
        foreach (var n in Nodes)
            n.Group = this;
        foreach (var n in Nodes)
            n.Build(map);
    }

    public void AddNode(NUINode n) {
        Nodes.Add(n);
        n.Group = this;
        if (buildMap != null)
            n.Build(buildMap);
    }

    public void RemoveNode(NUINode n) {
        n.TearDown();
        Nodes.Remove(n);
    }
    
    public void Show() {
        Render.HideAllGroups();
        Visible = true;
    }

    public void Hide() {
        Visible = false;
    }

    protected UIResult NavigateToPreviousNode(NUINode node) {
        var bInd = Nodes.IndexOf(node);
        var ii = bInd - 1;
        for (; ii != bInd; ii = M.Mod(Nodes.Count, ii - 1))
            if (!Nodes[ii].Passthrough)
                break;
        return new GoToNode(this, ii);
    }

    protected UIResult NavigateToNextNode(NUINode node) {
        var bInd = Nodes.IndexOf(node);
        var ii = bInd + 1;
        for (; ii != bInd; ii = M.Mod(Nodes.Count, ii + 1))
            if (!Nodes[ii].Passthrough)
                break;
        return new GoToNode(this, ii);
    }

    protected UIResult GoToShowHideGroupIfExists(NUINode node) =>
        node.ShowHideGroup == null || !node.ShowHideGroup.Interactable || node.IsEnabled ?
            new StayOnNode(true) :
            new GoToNode(node.ShowHideGroup, null);

    protected UIResult GoToPreviousScreenOrExitNode(NUINode node) =>
        (ExitNode != null && Controller.ScreenCall.Count == 0) ?
            new GoToNode(this, Nodes.IndexOf(ExitNode)) :
            new ReturnToScreenCaller();

    /// <summary>
    /// Handle default navigation for UINodes.
    /// </summary>
    public virtual UIResult Navigate(NUINode node, UICommand req) => req switch {
        UICommand.Left => new ReturnToGroupCaller(),
        UICommand.Right => GoToShowHideGroupIfExists(node),
        UICommand.Up => NavigateToPreviousNode(node),
        UICommand.Down => NavigateToNextNode(node),
        UICommand.Confirm => GoToShowHideGroupIfExists(node),
        UICommand.Back => GoToPreviousScreenOrExitNode(node),
        _ => throw new ArgumentOutOfRangeException(nameof(req), req, null)
    };
}
}