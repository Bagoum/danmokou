using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.DataStructures;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Plugins.Danmokou.Utility;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

public record Edge<V>(V From, V To, double Cost) : IEdge<V> {
    public int Direction { get; init; } = 0;
}

public class Tile {
    public int Cost { get; init; }
    public Unit? Unit { get; set; }
}

public record Unit(string Name, int Move) {
}

public class LocalXMLSRPGExamples : CoroutineRegularUpdater {
    public GameObject tilePrefab = null!;
    public VisualTreeAsset itemVTA = null!;
    public Sprite arrowStraight = null!;
    public Sprite arrowEnd = null!;
    public Sprite arrowCurve = null!;
    public XMLDynamicMenu Menu { get; private set; } = null!;
    private int h = 6;
    private int w = 10;
    public Tile[][] Map { get; private set; } = null!;
    public Graph<Edge<Tile>, Tile> Graph { get; private set; } = null!;

    public override void FirstFrame() {
        Menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = Menu.MainScreen;
        Map = new Tile[h][];
        for (int ih = 0; ih < h; ++ih) {
            Map[ih] = new Tile[w];
            for (int iw = 0; iw < w; ++iw) 
                Map[ih][iw] = new Tile {
                    Cost = (ih, iw) switch {
                        (3, 3) => 2,
                        (4, 3) => 2,
                        (4, 4) => 3,
                        (2, 4) => 4,
                        _ => 1
                    }
                };
        }
        Map[2][2].Unit = new("A", 5);
        Map[4][4].Unit = new("B", 4);
        var edges = new List<Edge<Tile>>();
        for (int ih = 0; ih < h; ++ih) {
            for (int iw = 0; iw < w; ++iw) {
                void AddEdge(int ah, int aw, int dir) =>
                    edges.Add(new(Map[ih][iw], Map[ah][aw], Map[ah][aw].Cost) { Direction = dir });
                if (ih > 0)
                    AddEdge(ih - 1, iw, 90);
                if (ih < h - 1)
                    AddEdge(ih + 1, iw, 270);
                if (iw > 0)
                    AddEdge(ih, iw - 1, 180);
                if (iw < w - 1)
                    AddEdge(ih, iw + 1, 0);
            }
        }
        Graph = new(Map.SelectMany(x => x), edges);
        
        var rows = h.Range().Select(ir => new UIRow(s.ScreenRender, 
                w.Range().Select(ic => {
                    var tileGO = GameObject.Instantiate(tilePrefab, transform);
                    tileGO.transform.position = new Vector3(-7 + ic, 2 - ir, RNG.GetFloatOffFrame(-1f, 2f));
                    return tileGO.GetComponent<SRPGTileHelper>().Initialize(this, (ir, ic));
                })) as UIGroup)
            .ToArray();
        
        var gridGroup = new VGroup(rows);
        Menu.FreeformGroup.AddGroupDynamic(gridGroup);
        
    }
    
    

    /// <summary>
    /// View for each tile.
    /// </summary>
    public class TileView : UIView<TileView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public LocalXMLSRPGExamples Src { get; }
            public (int r, int c) Index { get; }
            public Tile Tile => Src.Map[Index.r][Index.c];
            
            public Model(LocalXMLSRPGExamples src, (int r, int c) index) {
                this.Src = src;
                Index = index;
            }

            public override long GetViewHash() => 
                (Tile, (Src.Menu.CursorState.Value as UnitActionCS)?.Version ?? -1).GetHashCode();

            UIResult? IUIViewModel.OnConfirm(UINode n, ICursorState cs) {
                if (cs is NullCursorState && Tile.Unit is not null) {
                    _ = new UnitActionCS(Tile, n, Src);
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
                }
                return null;
            }

            UIResult? IUIViewModel.OnContextMenu(UINode node, ICursorState cs) =>
                PopupUIGroup.CreateContextMenu(node);

            UIGroup? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                return node.MakeTooltip(UINode.SimpleTTGroup($"{Tile.Cost}"));
            }
        }
        
        public override VisualTreeAsset? Prefab => VM.Src.itemVTA;
        public TileView(Model viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            if (cs is UnitActionCS actor)
                actor.UpdateTargetNode(VM.Tile, node);
        }

        //void IUIView.OnAddedToNavHierarchy(UINode node) => VM.Src.CurrentIndex = VM.Index;
        //void IUIView.OnRemovedFromNavHierarchy(UINode node) => VM.Src.CurrentIndex = null;

        public override void UpdateHTML() {
            HTML.Q<Label>("Content").text = $"{VM.Tile.Cost}";
            HTML.Q<Label>("UnitName").text = $"{VM.Tile.Unit?.Name ?? ""}";
            var bg = HTML.Q("BG");
            var arrow = HTML.Q("Arrow");
            if (VM.Src.Menu.CursorState.Value is UnitActionCS ua && ua.costs.ContainsKey(VM.Tile)) {
                bg.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                var path = ua.currentPath;
                var ind = path.IndexOf(VM.Tile);
                if (ind < 1)
                    arrow.style.backgroundImage = null as Texture2D;
                else {
                    var dirTo = VM.Src.Graph.FindEdge(path[ind-1], VM.Tile).Direction;
                    if (ind == path.Count - 1) {
                        arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowEnd);
                        arrow.transform.rotation = Quaternion.Euler(0, 0, -dirTo); //CSS rotation is CW
                    } else {
                        var dirFrom = VM.Src.Graph.FindEdge(VM.Tile, path[ind+1]).Direction;
                        if (dirFrom != dirTo) {
                            arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowCurve);
                            arrow.transform.rotation = Quaternion.Euler(0, 0, (dirTo, dirFrom) switch {
                                (0, 90) or (270, 180) => 0,
                                (180, 90) or (270, 0) => 90,
                                (90, 0) or (180, 270) => 180,
                                _ => 270
                            });
                        } else {
                            arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowStraight);
                            arrow.transform.rotation = Quaternion.Euler(0, 0, -dirTo);
                        }
                    }
                }
            } else {
                bg.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0);
                arrow.style.backgroundImage = null as Texture2D;
            }
        }
    }
    
    

    public class UnitActionCS : CustomCursorState, ICursorState {
        public Tile Source { get; }
        public Unit Unit => Source.Unit ?? throw new Exception();
        public UINode SourceNode { get; }
        private LocalXMLSRPGExamples Menu { get; }
        public readonly Dictionary<Tile, double> costs;
        public readonly Dictionary<Tile, Tile> prev;
        public readonly List<Tile> currentPath = ListCache<Tile>.Get();
        public int Version { get; private set; }

        public UnitActionCS(Tile source, UINode sourceNode, LocalXMLSRPGExamples menu) : base(menu.Menu) {
            this.Menu = menu;
            this.Source = source;
            this.SourceNode = sourceNode;
            (costs, prev) = SRPGUtils.Dijkstra(Source, (from, nearby) => {
                foreach (var e in menu.Graph.Outgoing(from))
                    nearby.Add((e.To, e.Cost)); //NB: cost may be dependent on unit
            }, Unit.Move);
            Tooltip = sourceNode.MakeTooltip(UINode.SimpleTTGroup($"Unit {Unit.Name}"), (_, ve) => {
                ve.AddToClassList("tooltip-above");
                ve.SetPadding(10, 10, 10, 10);
            });
            UpdateTargetNode(Source, SourceNode);
        }
        
        public void UpdateTargetNode(Tile target, UINode next) {
            if (!costs.ContainsKey(target)) return;
            ++Version;
            if (target == Source) {
                currentPath.Cleared().Add(Source);
                goto end;
            } else if (currentPath.Count > 0 && Menu.Graph.TryFindEdge(currentPath[^1], target) != null) {
                SRPGUtils.PruneCycle(currentPath.Added(target));
                var totalCost = 0.0;
                for (int ii = 1; ii < currentPath.Count; ++ii)
                    totalCost += Menu.Graph.FindEdge(currentPath[ii - 1], currentPath[ii]).Cost;
                if (totalCost <= Unit.Move)
                    goto end;
            }
            SRPGUtils.ReconstructPath(prev, target, currentPath.Cleared());
            end: ;
            next.HTML.SetTooltipAbsolutePosition(Tooltip?.Render.HTML);
        }

        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                Destroy();
                return new UIResult.GoToNode(SourceNode, NoOpIfSameNode:false);
            } else if (cmd == UICommand.Confirm) {
                if (current.MaybeView<TileView>() is { VM: { Tile: { } t } } && costs.ContainsKey(t) && t.Unit is null) {
                    t.Unit = Source.Unit;
                    Source.Unit = null;
                    Destroy();
                    return new UIResult.GoToNode(current, NoOpIfSameNode:false);
                }
            }
            return current.Navigate(cmd, this);
        }

        public override void Destroy() {
            base.Destroy();
            DictCache<Tile, double>.Consign(costs);
            DictCache<Tile, Tile>.Consign(prev);
            ListCache<Tile>.Consign(currentPath);
        }
    }

    
}
