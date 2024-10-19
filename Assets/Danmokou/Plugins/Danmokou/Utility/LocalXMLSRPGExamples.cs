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
using BagoumLib.Reflection;
using BagoumLib.Tasks;
using CommunityToolkit.HighPerformance;
using Danmokou.Behavior;
using Danmokou.Behavior.Display;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.SRPG;
using Danmokou.SRPG.Diffs;
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
    public Sprite arrowStraight = null!;
    public Sprite arrowEnd = null!;
    public Sprite arrowCurve = null!;
    public GameState State { get; private set; } = null!;
    public Tilemap[] tilemaps = null!;
    public XMLDynamicMenu worldUI = null!;
    private WorldSpaceUITK worldRender = null!;
    public XMLDynamicMenu overlayUI = null!;

    public SRPGDataConfig config = null!;
    
    public Dictionary<string, GameObject> unitDisplays = null!;
    private readonly Dictionary<Unit, IUnitDisplay> realizedUnits = new();
    public Unit? currentlyViewingLeftUnit;
    public Unit? currentlyViewingRightUnit;
    public Unit? lastViewedLeftUnit;
    public Unit? lastViewedRightUnit;
    public AttackOptionView? currentAttackOption;
    private UnitActionCS? UnitActionCursor => worldUI.CursorState.Value as UnitActionCS;
    
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
        var worldQuad = new WorldQuad(new(tilemapTrPos - dims/2f, dims), tilemapTrPos.z, Quaternion.Euler(tilemapTr.eulerAngles));
        
        var map = new Node[bound.size.y,bound.size.x];
        for (int ih = 0; ih < bound.size.y; ++ih) {
            for (int iw = 0; iw < bound.size.x; ++iw) {
                map[ih,iw] = SRPGUtils.MakeNode(config.NodeMatchers, tilemaps, 
                    bound.min, new Vector2Int(iw, ih));
            }
        }
        var edges = new List<Edge>();
        for (int ih = 0; ih < bound.size.y; ++ih) {
            for (int iw = 0; iw < bound.size.x; ++iw) {
                void AddEdge(int ah, int aw, int dir) =>
                    edges.Add(new(map[ih,iw], map[ah,aw]) { Direction = dir });
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
        var t1 = new Faction("Player Faction", new Color32(33,127,209,255));
        var t2 = new Faction("Enemy Faction", new Color32(196,35,64,255)) { FlipSprite = true };
        State = new(this, map, edges, t1, t2);
        State.AddActionFast(new NewUnit(map[3,3], 
            new(t1, "Reimu", new(30, 10, 20, 12, 3), new BasicAttackSkill("Needles", 8, 9, 10))
        ));
        State.AddActionFast(new NewUnit(map[5,4], 
            new(t1, "Marisa", new(24, 26, 14, 18, 6), new BasicAttackSkill("Broom", 1, 1, 8))
        ));
        State.AddActionFast(new NewUnit(map[8,8], 
            new(t2, "Yukari", new(40, 30, 24, 10, 8)
                //new BasicAttackSkill(1, 1, 4)
                )
        ));
        _ = State.AddAction(new GameState.StartGame(t1), AnimCT()).ContinueWithSync();

        gridCam = CameraRenderer.FindCapturer(1 << tilemaps[0].gameObject.layer).Value.CamInfo;
        AddToken(ServiceLocator.Find<WorldCameraContainer>().RestrictCameraPan(gridCam, worldQuad,null));
        var render = new UIRenderExplicit(worldUI.MainScreen.ContainerRender, ve => ve.AddColumn().UnboundSize())
            ;/*.WithView(new FixedXMLView(new(new WorldTrackingXML(gridCam, () => new(minLoc.x, maxLoc.y), null) {
                Pivot = XMLUtils.Pivot.TopLeft
            })));*/
        var rows = bound.size.y.Range().Select(ir => new UIRow(new UIRenderExplicit(render, ve => ve.AddRow()), 
                bound.size.x.Range().Select(ic => new UINode(new TileView(new(this, map[bound.size.y-1-ir,ic]))
                    /*, new FixedXMLView(new(new WorldTrackingXML(gridCam, 
                        () => gridBounds.TopLeft + new Vector2(0.5f + ic, -0.5f - ir), () => new(1,1))))
                    */))) { AllowWraparoundMovement = false } as UIGroup)
            .ToArray();
        
        var gridGroup = new VGroup(render, rows) { AllowWraparoundMovement = false };
        worldUI.FreeformGroup.AddGroupDynamic(gridGroup);
        (worldRender = Instantiate(worldSpaceUITK).GetComponent<WorldSpaceUITK>()).Initialize(new(worldUI.UISettings) {
            Quad = worldQuad,
            Layer = LayerMask.NameToLayer("Player"),
            SortingOrder = 10,
            SortingLayerName = "Wall"
        });
        overlayUI.FreeformGroup.AddGroupDynamic(MakeCharBlock(overlayUI.MainScreen.Q("CharBlockLeft"), true));
        overlayUI.FreeformGroup.AddGroupDynamic(MakeCharBlock(overlayUI.MainScreen.Q("CharBlockRight"), false));
        //since we want to render the "turn change" object above UI, we need to rerender the UI
        Instantiate(worldSpaceUITK).GetComponent<WorldSpaceUITK>().Initialize(new(overlayUI.UISettings));
    }

    private UIGroup MakeCharBlock(UIRenderSpace rs, bool isLeft) {
        var cbview = new CharBlockRSView(new(this, isLeft));
        var grp = new UIFreeformGroup(rs, SRPGUtils.AllStats.Except(new[]{Stat.CurrHP})
            .Select(s => new UINode(new StatlineView(new(cbview, s))) {
                Builder = ve => ve.Q(s.Abbrev()).Children().First()
        })) {
            GoBackWhenMouseLeavesNode = true
        };
        cbview.VM.Vis = grp;
        rs.WithView(cbview);
        return grp;
    }

    public override void RegularUpdate() {
        base.RegularUpdate();
        if (worldUI.LastOperationFrame < ETime.FrameNumber && overlayUI.LastOperationFrame < ETime.FrameNumber 
                                                           && (InputManager.IsLeftClick || InputManager.UIConfirm))
            animToken?.SoftCancel();
        if (InputManager.GetKeyTrigger(KeyCode.B).Active && State.MustActUnit is null) {
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
        if (ev.Path is null || ev.Path.Count <= 1 || FindUnit(ev.Unit) is not {} disp) return Task.CompletedTask;
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

    public async Task? Animate(GameState.StartGame ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        using var _ = worldUI.OperationsEnabled.AddConst(false);
        using var __ = overlayUI.OperationsEnabled.AddConst(false);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(null, ev.FirstFaction, !ev.FirstFaction.FlipSprite, cT, done));
        await t;
    }

    public async Task? Animate(GameState.SwitchFactionTurn ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        lastViewedLeftUnit = lastViewedRightUnit = null;
        using var _ = worldUI.OperationsEnabled.AddConst(false);
        using var __ = overlayUI.OperationsEnabled.AddConst(false);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(ev.From, ev.To, !ev.To.FlipSprite, cT, done));
        await t;
    }
    
    public class CharBlockRSView : UIView<CharBlockRSView.Model>, IUIView {
        public record Model(LocalXMLSRPGExamples Menu, bool Left) : IUIViewModel {
            public UIGroup Vis { get; set; } = null!;
            public Unit? Display => Left 
                    ? Menu.currentlyViewingLeftUnit ?? Menu.UnitActionCursor?.Unit ?? Menu.lastViewedLeftUnit
                    : Menu.currentlyViewingRightUnit ?? Menu.lastViewedRightUnit;

            long IUIViewModel.GetViewHash() =>
                (Menu.State.NActions, Display).GetHashCode();
        }

        public CharBlockRSView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UIRenderSpace render) {
            base.OnBuilt(render);
            RS.UseSourceVisible().WithPopupAnim();
            VM.Vis.Visibility.ManualUpdateLocalVisibility(GroupVisibility.TreeHidden, false);
        }

        public override void UpdateHTML() {
            if (VM.Display is not { } u) {
                _ = VM.Vis.Visibility.ManualUpdateLocalVisibility(GroupVisibility.TreeHidden)?.ContinueWithSync();
                return;
            }
            HTML.Q<Label>("CharName").text = u.Name;
            _ = VM.Vis.Visibility.ManualUpdateLocalVisibility(GroupVisibility.TreeVisible)?.ContinueWithSync();
        }
    }

    public class StatlineView : UIView<StatlineView.Model> {
        public record Model(CharBlockRSView Parent, Stat Stat) : IUIViewModel {
            public Unit? Unit => Parent.VM.Display;
            public long GetViewHash() => (Unit, Unit?.Team.State.NActions).GetHashCode();
            public int Val(Stat? s = null) => Unit!.Stats.EffectiveStat(s ?? Stat);

            TooltipProxy? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                return node.MakeTooltip(UINode.SimpleTTGroup($"stat {Stat} has val {Val()}"));
            }
        }
        public StatlineView(Model viewModel) : base(viewModel) { }

        public override void UpdateHTML() {
            var target = HTML.Q<Label>();
            if (VM.Unit is null) return;
            if (VM.Stat is Stat.MaxHP or Stat.CurrHP)
                target.text = $"{VM.Stat.Abbrev()}  <size=90><voffset=-0.06em>{VM.Val(Stat.CurrHP)}</voffset></size> / {VM.Val(Stat.MaxHP)}";
            else
                target.text = $"{VM.Stat.Abbrev()} {VM.Val()}";
        }
    }

    public class AttackOptionView : UIView<AttackOptionView.Model>, IUIView {
        public record Model(LocalXMLSRPGExamples Src, Unit Unit, IUnitSkill Skill): IConstUIViewModel {
        }
        public AttackOptionView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            HTML.Q<Label>().text = VM.Skill.Name;
        }

        public void OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.Src.currentAttackOption = this;
        }

        public void OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) {
            if (popupType is null)
                VM.Src.currentAttackOption = null;
        }
    }
    
    /// <summary>
    /// View for each tile.
    /// </summary>
    public class TileView : UIView<TileView.Model>, IUIView {
        public record Model(LocalXMLSRPGExamples Src, Node Node) : IUIViewModel {
            long IUIViewModel.GetViewHash() => 
                (Node, Src.UnitActionCursor?.Version ?? -1, Src.currentAttackOption).GetHashCode();

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
        
        public override VisualTreeAsset? Prefab => VM.Src.config.TileVTA;
        public TileView(Model viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.Src.currentlyViewingLeftUnit = VM.Src.currentlyViewingRightUnit = null;
            if (VM.Node.Unit is { } u) {
                if (u.Team.FlipSprite)
                    VM.Src.lastViewedRightUnit = VM.Src.currentlyViewingRightUnit = u;
                else
                    VM.Src.lastViewedLeftUnit = VM.Src.currentlyViewingLeftUnit = u;
            }
            if (cs is UnitActionCS actor)
                actor.UpdateTargetNode(VM.Node, node);
            ServiceLocator.Find<WorldCameraContainer>()
                .TrackTarget(VM.Node.CellCenter, VM.Src.gridCam, new(0.5f, 0.5f, 0.24f, 0.27f, 0));
        }

        //void IUIView.OnAddedToNavHierarchy(UINode node) => VM.Src.CurrentIndex = VM.Index;
        //void IUIView.OnRemovedFromNavHierarchy(UINode node) => VM.Src.CurrentIndex = null;

        private readonly Color attackableColor = new(0.65f, 0.25f, 0.15f, 0.8f);
        private readonly Color movableColor = new Color(0.15f, 0.5f, 0.6f, 0.8f);
        private readonly Color attackOrMoveColor = new Color(0.2f, 0.35f, 0.6f, 0.8f);
        private readonly Color invisColor = new Color(0.5f, 0.5f, 0.5f, 0f);
        public override void UpdateHTML() {
            HTML.Q<Label>("Content").text = $"{VM.Node.EntryCost(default!)}";
            var bg = HTML.Q("BG");
            var arrow = HTML.Q("Arrow");
            arrow.style.backgroundImage = null as Texture2D;
            var bgc = invisColor;
            if (VM.Src.worldUI.CursorState.Value is UnitActionCS ua) {
                var attackable = ua.attackable.Contains(VM.Node);
                if (ua.reachable.ContainsKey(VM.Node)) {
                    bgc = attackable ? attackOrMoveColor : movableColor;
                    var path = ua.currentPath;
                    var ind = path.IndexOf(VM.Node);
                    if (ind >= 1) {
                        var dirIn = VM.Src.State.Graph.FindEdge(path[ind - 1], VM.Node).Direction;
                        if (ind == path.Count - 1) {
                            arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowEnd);
                            arrow.transform.rotation = Quaternion.Euler(0, 0, -dirIn); //CSS rotation is CW
                        } else {
                            var dirOut = VM.Src.State.Graph.FindEdge(VM.Node, path[ind + 1]).Direction;
                            if (dirOut != dirIn) {
                                arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowCurve);
                                arrow.transform.rotation = Quaternion.Euler(0, 0, (dirIn, dirOut) switch {
                                    (0, 90) or (270, 180) => 0,
                                    (180, 90) or (270, 0) => 90,
                                    (90, 0) or (180, 270) => 180,
                                    _ => 270
                                });
                            } else {
                                arrow.style.backgroundImage = new StyleBackground(VM.Src.arrowStraight);
                                arrow.transform.rotation = Quaternion.Euler(0, 0, -dirIn);
                            }
                        }
                    }
                } else if (attackable) {
                    bgc = attackableColor;
                }
            } else if (VM.Src.currentAttackOption is { } attack) {
                var dist = (attack.VM.Unit.Location!.Index - VM.Node.Index).Abs().Sum();
                if (dist >= attack.VM.Skill.MinRange && dist <= attack.VM.Skill.MaxRange)
                    bgc = attackableColor;
            }
            bg.style.backgroundColor = bgc;
        }
    }

    public class UnitActionCS : CustomCursorState, ICursorState {
        public Node Source { get; }
        public Unit Unit { get; }
        public UINode SourceNode { get; }
        private LocalXMLSRPGExamples Menu { get; }
        public readonly Dictionary<Node, double> reachable;
        public readonly HashSet<Node> attackable;
        public readonly Dictionary<Node, Node> prev;
        public readonly List<Node> currentPath = ListCache<Node>.Get();
        public int Version { get; private set; }

        public UnitActionCS(Node source, UINode sourceNode, LocalXMLSRPGExamples menu) : base(menu.worldUI) {
            this.Menu = menu;
            this.Source = source;
            this.Unit = Source.Unit ?? throw new Exception($"No unit exists at {Source}!");
            this.SourceNode = sourceNode;
            (reachable, prev) = SRPGUtils.Dijkstra(Source, (from, nearby) => from.OutgoingEdges(Unit, nearby), Unit.Stats.Move);
            var attackableOffsets = new List<Vector2Int>();
            foreach (var s in Unit.AttackSkills)
                SRPGUtils.PointsAtGridRange(s.MinRange, s.MaxRange, attackableOffsets);
            var attackableOffsetsHS = attackableOffsets.ToHashSet();
            attackable = reachable.Keys
                .SelectMany(n => attackableOffsetsHS.Select(offset => n.Index + offset))
                .SelectNotNull(index => Menu.State.TryNodeAt(index))
                .ToHashSet();
            Tooltip = sourceNode.MakeTooltip(UINode.SimpleTTGroup($"Unit {Unit.Name}"), (_, ve) => {
                ve.AddToClassList("tooltip-above");
                ve.SetPadding(10, 10, 10, 10);
            });
            UpdateTargetNode(Source, SourceNode);
        }
        
        public void UpdateTargetNode(Node target, UINode next) {
            if (!reachable.ContainsKey(target)) return;
            ++Version;
            if (target == Source) {
                currentPath.Cleared().Add(Source);
                goto end;
            } else if (currentPath.Count > 0 && Menu.State.Graph.TryFindEdge(currentPath[^1], target) != null) {
                SRPGUtils.PruneCycle(currentPath.Added(target));
                var totalCost = 0.0;
                for (int ii = 1; ii < currentPath.Count; ++ii)
                    totalCost += Menu.State.Graph.FindEdge(currentPath[ii - 1], currentPath[ii]).Cost(Unit);
                if (totalCost <= Unit.Stats.Move)
                    goto end;
            }
            SRPGUtils.ReconstructPath(prev, target, currentPath.Cleared());
            end: ;
            Tooltip?.Track(next);
        }

        private UIRenderSpace MakeOptsColumnRS(Vector2 pivot, Vector2 leftTop) =>
            new UIRenderConstructed(Menu.overlayUI.MainScreen.AbsoluteTerritory, new(ve => 
                ve.AddColumn().UnsetSize().ConfigureAndPositionAbsolute(pivot, leftTop))).WithFastPopupAnim();

        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                Destroy();
                return new UIResult.GoToNode(SourceNode, NoOpIfSameNode:false);
            }
            if (cmd != UICommand.Confirm)
                goto fail;

            if (current.MaybeView<TileView>() is not { VM: { Node: { } t } } 
                || !reachable.ContainsKey(t) || (t != Source && t.Unit is not null))
                goto fail;
            
            Menu.lastViewedLeftUnit = Unit;
            return new UIResult.AfterTask(async () => {
                await Menu.State.AddAction(new MoveUnit(Source, t, Unit, currentPath), Menu.AnimCT());
                var actionOpts = new UIColumn(
                    MakeOptsColumnRS(XMLUtils.Pivot.Left, UIBuilderRenderer.ScreenToXML(new Vector2(0.06f, 0) + 
                            Menu.worldRender.PanelToScreen(current.PanelLocation.center))), new UINode?[] {
                        !Unit.AttackSkills.Any() ? null :
                            new FuncNode("Attack", n => new UIResult.GoToNode(
                                new UIColumn(MakeOptsColumnRS(XMLUtils.Pivot.TopLeft, n.XMLLocation.XMaxYMin()),
                                    Unit.Skills.Where(s => s.Type is UnitSkillType.Attack).Select(s =>
                                        new UINode(new AttackOptionView(new(Menu, Unit, s))) {
                                            Prefab = Menu.config.ActionNodeVTA
                                })) {
                                    Parent = n.Group,
                                    DestroyOnLeave = true,
                                    OverlayAlphaOverride = 0,
                                }.WithLeaveHideVisibility()
                            )),
                        new FuncNode("Wait", n => new UIResult.ReturnToGroupCaller {
                            OnPostTransition = () =>
                                _ = Menu.State.AddAction(new UnitWait(Unit), Menu.AnimCT())
                                    .ContinueWithSync()
                        }), 
                        new FuncNode("Go Back", n => {
                            if (Menu.State.Undo()) {
                                if (Menu.worldUI.QueuedInput is null)
                                    _ = Menu.worldUI.OperateOnResultAnim(new UIResult.GoToNode(SourceNode))
                                        .ContinueWithSync();
                                return new UIResult.ReturnToGroupCaller();
                            } else return new UIResult.StayOnNode(true);
                        })
                    }.WithNodeMod(n => n.Prefab = Menu.config.ActionNodeVTA)) {
                    DestroyOnLeave = true,
                    OverlayAlphaOverride = 0,
                    NavigationCanLeaveGroup = false,
                    ExitIndexOverride = -1
                }.WithLeaveHideVisibility();
                Menu.overlayUI.FreeformGroup.AddGroupDynamic(actionOpts);
                await Menu.overlayUI.OperateOnResultAnim(new UIResult.GoToNode(actionOpts));
                Destroy();
                return new UIResult.GoToNode(current, NoOpIfSameNode: false);
            });
            
            
            fail: ;
            return current.Navigate(cmd, this);
        }

        public override void Destroy() {
            base.Destroy();
            DictCache<Node, double>.Consign(reachable);
            DictCache<Node, Node>.Consign(prev);
            ListCache<Node>.Consign(currentPath);
        }
    }

}
