using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SRPG;
using Danmokou.SRPG.Actions;
using Danmokou.SRPG.Nodes;
using Danmokou.UI;
using Danmokou.UI.XML;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class LocalXMLSRPGExamples : CoroutineRegularUpdater, IStateRealizer {
    private CameraInfo gridCam = null!;
    public GameObject worldSpaceUITK = null!;
    public GameObject turnChanger = null!;
    public VisualTreeAsset itemVTA = null!;
    public Sprite arrowStraight = null!;
    public Sprite arrowEnd = null!;
    public Sprite arrowCurve = null!;
    public GameState State { get; private set; } = null!;
    public Tilemap[] tilemaps = null!;
    public XMLDynamicMenu worldUI = null!;
    public XMLDynamicMenu overlayUI = null!;

    public SRPGDataConfig config = null!;
    
    public Dictionary<string, GameObject> unitDisplays = null!;
    private readonly Dictionary<Unit, IUnitDisplay> realizedUnits = new();
    
    //move to Request
    private Cancellable lifetime = new();
    private Cancellable? animToken;
    public ICancellee AnimCT() => JointCancellee.From(lifetime, Cancellable.Replace(ref animToken));

    protected override void BindListeners() {
        base.BindListeners();
        Listen(GameManagement.ReturnToMainMenuCancellation, _ => lifetime.Cancel());
    }

    public override void FirstFrame() {
        unitDisplays = config.UnitDisplays.ToDictionary(ud => ud.Name, ud => ud.prefab);
        var minLoc = tilemaps[0].cellBounds.min;
        var maxLoc = tilemaps[0].cellBounds.max;
        foreach (var t in tilemaps) {
            minLoc = Vector3Int.Min(minLoc, t.cellBounds.min);
            maxLoc = Vector3Int.Max(maxLoc, t.cellBounds.max);
        }
        var bound = new BoundsInt(minLoc, maxLoc - minLoc);
        var tilemapTr = tilemaps[0].gameObject.transform;
        var tilemapTrPos = tilemapTr.position;
        var dims = tilemaps[0].cellSize.PtMul(tilemapTr.lossyScale).PtMul(bound.size);
        var gridQuad = new WorldQuad(new(tilemapTrPos - dims/2f, dims), tilemapTrPos.z, Quaternion.Euler(tilemapTr.eulerAngles));
        
        var map = new Node[bound.size.y][];
        for (int ih = 0; ih < bound.size.y; ++ih) {
            map[ih] = new Node[bound.size.x];
            for (int iw = 0; iw < bound.size.x; ++iw) {
                map[ih][iw] =SRPGUtils.MakeNode(config.NodeMatchers, tilemaps, 
                    bound.min + new Vector3Int(iw, ih, 0));
            }
        }
        var edges = new List<Edge>();
        for (int ih = 0; ih < bound.size.y; ++ih) {
            for (int iw = 0; iw < bound.size.x; ++iw) {
                void AddEdge(int ah, int aw, int dir) =>
                    edges.Add(new(map[ih][iw], map[ah][aw]) { Direction = dir });
                if (ih > 0)
                    AddEdge(ih - 1, iw, 270);
                if (ih < bound.size.y - 1)
                    AddEdge(ih + 1, iw, 90);
                if (iw > 0)
                    AddEdge(ih, iw - 1, 180);
                if (iw < bound.size.x - 1)
                    AddEdge(ih, iw + 1, 0);
            }
        }
        var t1 = new Faction("Player");
        var t2 = new Faction("Enemy") { FlipSprite = true };
        State = new(this, map.SelectMany(x => x), edges, t1, t2);
        State.AddActionFast(new NewUnit(map[3][3], new(t1, "Reimu", 4)));
        State.AddActionFast(new NewUnit(map[5][4], new(t1, "Marisa", 5)));
        State.AddActionFast(new NewUnit(map[8][8], new(t2, "Yukari", 6)));
        _ = State.AddAction(new GameState.StartGame(t1), Cancellable.Null);

        gridCam = CameraRenderer.FindCapturer(1 << tilemaps[0].gameObject.layer).Value.CamInfo;
        AddToken(ServiceLocator.Find<WorldCameraContainer>().RestrictCameraPan(gridCam, gridQuad,null));
        var render = new UIRenderExplicit(worldUI.MainScreen.ContainerRender, ve => ve.AddColumn().UnboundSize())
            ;/*.WithView(new FixedXMLView(new(new WorldTrackingXML(gridCam, () => new(minLoc.x, maxLoc.y), null) {
                Pivot = XMLUtils.Pivot.TopLeft
            })));*/
        var rows = bound.size.y.Range().Select(ir => new UIRow(new UIRenderExplicit(render, ve => ve.AddRow()), 
                bound.size.x.Range().Select(ic => new UINode(new TileView(new(this, map[bound.size.y-1-ir][ic]))
                    /*, new FixedXMLView(new(new WorldTrackingXML(gridCam, 
                        () => gridBounds.TopLeft + new Vector2(0.5f + ic, -0.5f - ir), () => new(1,1))))
                    */))) { AllowWraparoundMovement = false } as UIGroup)
            .ToArray();
        
        var gridGroup = new VGroup(render, rows) { AllowWraparoundMovement = false };
        worldUI.FreeformGroup.AddGroupDynamic(gridGroup);
        Instantiate(worldSpaceUITK).GetComponent<WorldSpaceUITK>().Initialize(new(worldUI.UISettings) {
            Quad = gridQuad,
            Layer = LayerMask.NameToLayer("Player"),
            SortingOrder = 10,
            SortingLayerName = "Wall"
        });
        overlayUI.FreeformGroup.AddNodeDynamic(new UINode("HELLO WORLD"));
        //since we want to render the "turn change" object above UI, we need to rerender the UI
        Instantiate(worldSpaceUITK).GetComponent<WorldSpaceUITK>().Initialize(new(overlayUI.UISettings));
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (worldUI.LastOperationFrame < ETime.FrameNumber && (InputManager.IsLeftClick || InputManager.UIConfirm))
            animToken?.SoftCancel();
        if (InputManager.GetKeyTrigger(KeyCode.B).Active) {
            var mostRecent = State.MostRecentAction;
            Logs.Log((State.Undo() ? "Undid " : "Failed to undo ") + mostRecent);
        }
        //Logs.Log($"{Input.mouseScrollDelta.y:F4}");
    }
    
    public void Instantiate(NewUnit nu) {
        var u = nu.Unit;
        var disp = UnityEngine.Object.Instantiate(unitDisplays[u.Key]).GetComponent<UnitDisplay>();
        disp.Initialize(u);
        realizedUnits[u] = disp;
    }

    private IUnitDisplay? FindUnit(Unit u) => realizedUnits.GetValueOrDefault(u);

    public void SetUnitLocation(Unit u, Node? from, Node? to) {
        if (FindUnit(u) is not { } disp) return;
        if (to is null) {
            disp.Destroy();
            realizedUnits.Remove(u);
        } else
            disp.SetLocation(to.CellAnchor);
    }
    
    public Task? Animate(MoveUnit ev, ICancellee cT) {
        if (ev.Path is null || FindUnit(ev.Unit) is not {} disp) return Task.CompletedTask;
        var tcs = new TaskCompletionSource<System.Reactive.Unit>();
        disp.RunRIEnumerator(_Animate());
        return tcs.Task;
        
        IEnumerator _Animate() {
            var time = 0.5f;
            for (var elapsed = 0f; elapsed < time && !cT.Cancelled; elapsed += ETime.FRAME_TIME) {
                var effT = Easers.EIOSine(elapsed / time);
                //rounding errors can make effT close enough to 1 for idx to be path.Count
                var idx = Math.Min(ev.Path.Count - 1, (int)Math.Floor(effT * ev.Path.Count));
                var prevLoc = (ev.Path.Try(idx - 1) ?? ev.From).CellAnchor;
                disp.SetLocation(
                    Vector3.Lerp(prevLoc, ev.Path[idx].CellAnchor, effT * ev.Path.Count - idx));
                yield return null;
            }
            tcs.SetResult(default);
        }
    }

    public Task? Animate(GameState.StartGame ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(null, ev.FirstFaction, false, cT, done));
        return t;
    }

    public Task? Animate(GameState.SwitchFactionTurn ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(ev.From, ev.To, ev.To.Name.Value.Contains("Player"), cT, done));
        return t;
    }
    
    /// <summary>
    /// View for each tile.
    /// </summary>
    public class TileView : UIView<TileView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public LocalXMLSRPGExamples Src { get; }
            public Node Node { get; }

            public Model(LocalXMLSRPGExamples src, Node node) {
                this.Src = src;
                Node = node;
            }

            public override long GetViewHash() => 
                (Tile: Node, (Src.worldUI.CursorState.Value as UnitActionCS)?.Version ?? -1).GetHashCode();

            UIResult? IUIViewModel.OnConfirm(UINode n, ICursorState cs) {
                if (cs is NullCursorState && Node.Unit != null) {
                    if (Node.Unit.Status is UnitStatus.CanMove) {
                        _ = new UnitActionCS(Node, n, Src);
                        return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
                    } else
                        return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                }
                return null;
            }

            UIResult? IUIViewModel.OnContextMenu(UINode node, ICursorState cs) =>
                PopupUIGroup.CreateContextMenu(node);

            TooltipProxy? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                return node.MakeTooltip(UINode.SimpleTTGroup($"{Node.Type.Description}"));
            }
        }
        
        public override VisualTreeAsset? Prefab => VM.Src.itemVTA;
        public TileView(Model viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            if (cs is UnitActionCS actor)
                actor.UpdateTargetNode(VM.Node, node);
            ServiceLocator.Find<WorldCameraContainer>()
                .TrackTarget(VM.Node.CellCenter, VM.Src.gridCam, new(0.5f, 0.5f, 0.24f, 0.27f, 0));
        }

        //void IUIView.OnAddedToNavHierarchy(UINode node) => VM.Src.CurrentIndex = VM.Index;
        //void IUIView.OnRemovedFromNavHierarchy(UINode node) => VM.Src.CurrentIndex = null;

        public override void UpdateHTML() {
            HTML.Q<Label>("Content").text = $"{VM.Node.EntryCost(default!)}";
            var bg = HTML.Q("BG");
            var arrow = HTML.Q("Arrow");
            if (VM.Src.worldUI.CursorState.Value is UnitActionCS ua && ua.costs.ContainsKey(VM.Node)) {
                bg.style.backgroundColor = new Color(0.2f, 0.4f, 0.6f, 0.8f);
                var path = ua.currentPath;
                var ind = path.IndexOf(VM.Node);
                if (ind < 1)
                    arrow.style.backgroundImage = null as Texture2D;
                else {
                    var dirTo = VM.Src.State.Graph.FindEdge(path[ind-1], VM.Node).Direction;
                    if (ind == path.Count - 1) {
                        arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowEnd);
                        arrow.transform.rotation = Quaternion.Euler(0, 0, -dirTo); //CSS rotation is CW
                    } else {
                        var dirFrom = VM.Src.State.Graph.FindEdge(VM.Node, path[ind+1]).Direction;
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
        public Node Source { get; }
        public Unit Unit => Source.Unit ?? throw new Exception();
        public UINode SourceNode { get; }
        private LocalXMLSRPGExamples Menu { get; }
        public readonly Dictionary<Node, double> costs;
        public readonly Dictionary<Node, Node> prev;
        public readonly List<Node> currentPath = ListCache<Node>.Get();
        public int Version { get; private set; }

        public UnitActionCS(Node source, UINode sourceNode, LocalXMLSRPGExamples menu) : base(menu.worldUI) {
            this.Menu = menu;
            this.Source = source;
            this.SourceNode = sourceNode;
            (costs, prev) = SRPGUtils.Dijkstra(Source, (from, nearby) => from.OutgoingEdges(Unit, nearby), Unit.Move);
            Tooltip = sourceNode.MakeTooltip(UINode.SimpleTTGroup($"Unit {Unit.Name}"), (_, ve) => {
                ve.AddToClassList("tooltip-above");
                ve.SetPadding(10, 10, 10, 10);
            });
            UpdateTargetNode(Source, SourceNode);
        }
        
        public void UpdateTargetNode(Node target, UINode next) {
            if (!costs.ContainsKey(target)) return;
            ++Version;
            if (target == Source) {
                currentPath.Cleared().Add(Source);
                goto end;
            } else if (currentPath.Count > 0 && Menu.State.Graph.TryFindEdge(currentPath[^1], target) != null) {
                SRPGUtils.PruneCycle(currentPath.Added(target));
                var totalCost = 0.0;
                for (int ii = 1; ii < currentPath.Count; ++ii)
                    totalCost += Menu.State.Graph.FindEdge(currentPath[ii - 1], currentPath[ii]).Cost(Unit);
                if (totalCost <= Unit.Move)
                    goto end;
            }
            SRPGUtils.ReconstructPath(prev, target, currentPath.Cleared());
            end: ;
            Tooltip?.Track(next);
        }

        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                Destroy();
                return new UIResult.GoToNode(SourceNode, NoOpIfSameNode:false);
            } else if (cmd == UICommand.Confirm) {
                if (current.MaybeView<TileView>() is { VM: { Node: { } t } } && costs.ContainsKey(t) && t.Unit is null) {
                    return new UIResult.AfterTask(async () => {
                        await Menu.State.AddAction(new MoveUnit(Source, t, Unit, currentPath), Menu.AnimCT());
                        Destroy();
                        return new UIResult.GoToNode(current, NoOpIfSameNode: false);
                    });
                }
            }
            return current.Navigate(cmd, this);
        }

        public override void Destroy() {
            base.Destroy();
            DictCache<Node, double>.Consign(costs);
            DictCache<Node, Node>.Consign(prev);
            ListCache<Node>.Consign(currentPath);
        }
    }

}
