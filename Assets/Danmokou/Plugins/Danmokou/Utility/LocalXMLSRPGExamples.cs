using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public class LocalXMLSRPGExamples : CoroutineRegularUpdater, IStateRealizer, ISRPGExecutor {
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

    [field:SerializeField] public SRPGDataConfig Config { get; set; } = null!;
    
    public Dictionary<string, GameObject> unitDisplays = null!;
    private readonly Dictionary<Unit, UnitDisplay> realizedUnits = new();
    public IEnumerable<UnitDisplay> AllUnits => realizedUnits.Values;
    private Node? lastViewedNode;
    public Unit? currentlyViewingLeftUnit;
    public Unit? currentlyViewingRightUnit;
    public Unit? lastViewedLeftUnit;
    public Unit? lastViewedRightUnit;
    private ISkillUsage? CurrSkill;
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
        unitDisplays = Config.UnitDisplays.ToDictionary(ud => ud.Name, ud => ud.prefab);
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
                map[ih,iw] = SRPGUtils.MakeNode(Config.NodeMatchers, tilemaps, 
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
        State.AddDiffFast(new NewUnit(map[3,3], 
            new(t1, "Reimu", new(30, 10, 20, 12, 3), UnitSkill.ReimuNeedles.S, UnitSkill.ReimuDebuff.S, UnitSkill.ReimuBuff.S)
        ));
        State.AddDiffFast(new NewUnit(map[5,4], 
            new(t1, "Marisa", new(24, 26, 14, 18, 6), UnitSkill.MarisaBroom.S) {
                Movement = MovementFlags.Flying
            }
        ));
        State.AddDiffFast(new NewUnit(map[8,8], 
            new(t2, "Yukari", new(1, 30, 24, 10, 8), UnitSkill.YukariGapPower.S)
        ));
        State.AddDiff(new GameState.StartGame(0), AnimCT()).Log();

        gridCam = CameraRenderer.FindCapturer(1 << tilemaps[0].gameObject.layer).Value.CamInfo;
        AddToken(ServiceLocator.Find<WorldCameraContainer>().RestrictCameraPan(gridCam, worldQuad,null));
        var render = new UIRenderExplicit(worldUI.MainScreen.ContainerRender, ve => ve.AddColumn().UnboundSize())
            ;/*.WithView(new FixedXMLView(new(new WorldTrackingXML(gridCam, () => new(minLoc.x, maxLoc.y), null) {
                Pivot = XMLUtils.Pivot.TopLeft
            })));*/
        var rows = bound.size.y.Range().Select(ir => new UIRow(new UIRenderExplicit(render, ve => ve.AddRow()), 
                bound.size.x.Range().Select(ic => new UINode(new TileWDView(new(this, map[bound.size.y-1-ir,ic]))
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

        overlayUI.MainScreen.Q("NodeInfo").WithView(new TileInfoRSView(new(this)));
        overlayUI.FreeformGroup.AddGroupDynamic(MakeCharBlock(overlayUI.MainScreen.Q("CharBlockLeft"), true));
        overlayUI.FreeformGroup.AddGroupDynamic(MakeCharBlock(overlayUI.MainScreen.Q("CharBlockRight"), false));
        //since we want to render the "turn change" object above UI, we need to rerender the UI
        Instantiate(worldSpaceUITK).GetComponent<WorldSpaceUITK>().Initialize(new(overlayUI.UISettings));
    }

    private UIGroup MakeCharBlock(UIRenderSpace rs, bool isLeft) {
        var cbview = new CharBlockRSView(new(this, isLeft));
        var grp = new UIFreeformGroup(rs, SRPGUtils.AllStats.Except(new[]{Stat.CurrHP})
            .Select(s => new UINode(new StatlineOVView(new(cbview, s))) {
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
        disp.Initialize(this, u);
        realizedUnits[u] = disp;
    }

    public void Uninstantiate(NewUnit nu) {
        var u = nu.Unit;
        if (FindUnit(u) is not { } disp) return;
        disp.Uninstantiate();
        realizedUnits.Remove(u);
    }

    public void Disable(GraveyardUnit gu) {
        if (FindUnit(gu.Unit) is not { } disp) return;
        disp.gameObject.SetActive(false);
    }
    
    public void Undisable(GraveyardUnit gu) {
        if (FindUnit(gu.Unit) is not { } disp) return;
        disp.gameObject.SetActive(true);
    }

    public UnitDisplay? FindUnit(Unit u) => realizedUnits.GetValueOrDefault(u);

    public void SetUnitLocation(Unit u, Node? to) {
        if (FindUnit(u) is not { } disp) return;
        disp.SetLocation(to);
    }
    
    public Task? Animate(MoveUnit ev, ICancellee cT) {
        if (ev.Path is null || ev.Path.Count <= 1 || FindUnit(ev.Unit) is not {} disp) return null;
        return disp.AnimateMove(ev, cT);
    }
    
    public Task? Animate(GraveyardUnit ev, ICancellee cT) {
        if (FindUnit(ev.Unit) is not {} disp) return null;
        return disp.SendToGraveyard(ev, cT);
    }
    
    public Task? Animate(UseUnitSkill ev, ICancellee cT, SubList<IGameDiff> caused) {
        if (FindUnit(ev.Unit) is not {} disp) return null;
        return disp.AnimateAttack(ev, cT, caused);
    }
    
    public Task? Animate(ReduceUnitHP ev, ICancellee cT) {
        if (FindUnit(ev.Target) is { } disp) 
            DropHelpers.DropDropLabel(disp.Beh.Location, DropHelpers.Red, $"{ev.Damage}", 1f, size:2);
        return null;
    }

    public async Task? Animate(GameState.StartGame ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        using var _ = worldUI.OperationsEnabled.AddConst(false);
        using var __ = overlayUI.OperationsEnabled.AddConst(false);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(null, State.TurnOrder[ev.FirstFactionIdx], 
                !State.TurnOrder[ev.FirstFactionIdx].FlipSprite, cT, done));
        await t;
    }

    public async Task? Animate(GameState.SwitchFactionTurn ev, ICancellee cT) {
        var done = WaitingUtils.GetAwaiter(out var t);
        lastViewedLeftUnit = lastViewedRightUnit = null;
        using var _ = worldUI.OperationsEnabled.AddConst(false);
        using var __ = overlayUI.OperationsEnabled.AddConst(false);
        Instantiate(turnChanger).GetComponent<TurnChangeAnimator>()
            .Initialize(new(State.TurnOrder[ev.FromIdx], State.TurnOrder[ev.NextIdx], 
                !State.TurnOrder[ev.NextIdx].FlipSprite, cT, done));
        await t;
    }

    public class TileInfoRSView : UIView<TileInfoRSView.Model> {
        public record Model(LocalXMLSRPGExamples Menu) : IUIViewModel {
            public long GetViewHash() => (Menu.lastViewedNode, Menu.UnitActionCursor).GetHashCode();
        }
        public TileInfoRSView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UIRenderSpace render) {
            base.OnBuilt(render);
            RS.WithPopupAnim().OverrideLocalVisibility(false, fast: true).Log();
        }

        public override void UpdateHTML() {
            base.UpdateHTML();
            if (VM.Menu.lastViewedNode is not {} node) {
                RS.OverrideLocalVisibility(false).Log();
            } else {
                RS.OverrideLocalVisibility(true).Log();
                HTML.Q<Label>("NodeInfoTitle").text = node.Description;
                HTML.Q<Label>("movText").text =
                    VM.Menu.UnitActionCursor is not { } cs ?
                        "\u00A0" :
                        MovCost(node.EntryCost(cs.Unit));
                HTML.Q<Label>("healText").text = AsTileMod(node.Type.Heal);
                HTML.Q<Label>("atkText").text = AsTileMod(node.Type.Power);
                HTML.Q<Label>("defText").text = AsTileMod(node.Type.Shield);
            }
        }

        private string MovCost(float x) {
            if (x < 1)
                return StatlineOVView.AsBuff($"{x:.0}");
            if (x <= 1)
                return "1";
            if (x >= INodeType.MAXCOST)
                return StatlineOVView.AsDebuff("X");
            if (Math.Abs(x - Math.Round(x)) < M.MAG_ERR) {
                return StatlineOVView.AsDebuff($"{Math.Round(x)}");
            }
            return StatlineOVView.AsDebuff($"{x:0.0}");
        }

        private string AsTileMod(int x) => x switch {
            > 0 => StatlineOVView.AsBuff(x.ToString()),
            < 0 => StatlineOVView.AsDebuff(x.ToString()),
            _ => "-"
        };
    }

    public class CharBlockRSView : UIView<CharBlockRSView.Model> {
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
                VM.Vis.Visibility.ManualUpdateLocalVisibility(GroupVisibility.TreeHidden)?.Log();
                return;
            }
            HTML.Q<Label>("CharName").text = u.Name;
            HTML.EnableInClassList("theme-red", u.Team.FlipSprite);
            HTML.EnableInClassList("theme-blue", !u.Team.FlipSprite);
            var disp = VM.Menu.FindUnit(u);
            if (disp != null)
                HTML.Q("CharBG").style.backgroundImage = new(disp.Portrait);
            VM.Vis.Visibility.ManualUpdateLocalVisibility(GroupVisibility.TreeVisible)?.Log();
        }
    }

    public class StatlineOVView : UIView<StatlineOVView.Model> {
        public static string AsDelta(int delta) => delta switch {
            > 0 => AsBuff(delta.AsDelta()),
            < 0 => AsDebuff(delta.AsDelta()),
            _ => ""
        };
        public static string AsBuff(string txt) => $"<color=#79ecac>{txt}</color>";
        public static string AsDebuff(string txt) => $"<color=#d86974>{txt}</color>";
        public record Model(CharBlockRSView Parent, Stat Stat) : IUIViewModel {
            public Unit? Unit => Parent.VM.Display;
            public long GetViewHash() => (Unit, Unit?.Team.State.NActions).GetHashCode();
            public int Val(Stat? s = null) => Unit!.Stats.EffectiveStat(s ?? Stat);
            public int BaseVal(Stat? s = null) => Unit!.Stats.BaseStat(s ?? Stat);

            TooltipProxy IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                var sb = new StringBuilder();
                var val = BaseVal();
                sb.Append($"Base {Stat.Name()}: {val}");
                foreach (var m in Unit!.Stats.Mods) {
                    var nxtVal = m.ApplyMod(Stat, val);
                    var diff = nxtVal - val;
                    if (diff != 0)
                        sb.Append($"\n{m.Source.Name}: {AsDelta(diff)}");
                }

                return node.MakeTooltip(UINode.SimpleTTGroup(sb.ToString()),
                    Parent.VM.Left ? XMLUtils.Pivot.BotLeft : XMLUtils.Pivot.BotRight, (_, ve) =>
                        ve.AddAnchorClass(Parent.VM.Left ? XMLUtils.Pivot.TopLeft : XMLUtils.Pivot.TopRight)
                            .AddToClassList("tooltip-panel1"));
            }
        }
        public StatlineOVView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Node.Flags |= UINodeFlag.SendEnterLeaveOnPointerEv;
        }

        public override void UpdateHTML() {
            var target = HTML.Q<Label>();
            if (VM.Unit is null) return;
            if (VM.Stat is Stat.MaxHP or Stat.CurrHP) {
                var (chp, mhp) = (VM.Val(Stat.CurrHP), VM.Val(Stat.MaxHP));
                string chps;
                if (chp > mhp)
                    chps = AsBuff(chp.ToString());
                else if (chp <= 0)
                    chps = AsDebuff("0");
                else
                    chps = chp.ToString();
                target.text = $"{VM.Stat.Abbrev()}  <size=90><voffset=-0.06em>{chps}</voffset></size> / {mhp}";
            } else {
                var v = VM.Val();
                var cmp = v.CompareTo(VM.BaseVal());
                var valstr = cmp switch {
                    > 0 => AsBuff(v.ToString()),
                    < 0 => AsDebuff(v.ToString()),
                    _ => v.ToString()
                };
                target.text = $"{VM.Stat.Abbrev()} {valstr}";
            }
        }
    }

    public class AttackOptionOVView : UIView<AttackOptionOVView.Model>, IUIView {
        public record Model(LocalXMLSRPGExamples Src, Unit Unit, IUnitSkill Skill) : IConstUIViewModel, ISkillUsage {
            UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                var target = new UIColumn(Src.overlayUI.MainScreen, EmptyNode.MakeUnselector(null))
                    { Parent = node.Group, DestroyOnLeave = true };
                //create a CS for targeting the skill on world layer, and go to an unselector node on overlay layer
                _ = new SkillTargetSelCS(this, Src);
                return new UIResult.GoToNode(target);
            }

            TooltipProxy? IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                return node.MakeTooltip(UINode.SimpleTTGroup(Skill.Description),
                    XMLUtils.Pivot.TopLeft, builder: (_, ve) =>
                        ve.AddAnchorClass(XMLUtils.Pivot.TopRight).AddToClassList("tooltip-panel1"));
            }
        }
        public AttackOptionOVView(Model viewModel) : base(viewModel) { }

        public override VisualTreeAsset? Prefab => VM.Src.Config.ActionNodeVTA;

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            HTML.Q<Label>().text = VM.Skill.Name;
        }

        public void OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.Src.CurrSkill = VM;
        }

        public void OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) {
            if (popupType is null && ReferenceEquals(VM.Src.CurrSkill, VM))
                VM.Src.CurrSkill = null;
        }
    }

    /// <summary>
    /// View for each tile (world UI).
    /// </summary>
    public class TileWDView : UIView<TileWDView.Model>, IUIView {
        public record Model(LocalXMLSRPGExamples Src, Node Node) : IUIViewModel {
            long IUIViewModel.GetViewHash() =>
                (Node, Src.UnitActionCursor?.Version ?? -1, Src.CurrSkill).GetHashCode();

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

            UIResult IUIViewModel.OnContextMenu(UINode node, ICursorState cs) =>
                PopupUIGroup.CreateContextMenu(node);

            /*TooltipProxy IUIViewModel.Tooltip(UINode node, ICursorState cs, bool prevExists) {
                return node.MakeTooltip(UINode.SimpleTTGroup($"{Node.Type.Description}"));
            }*/
        }
        
        public override VisualTreeAsset Prefab => VM.Src.Config.TileVTA;
        public TileWDView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Node.HTML.SetWidthHeight(UIBuilderRenderer.ToUIXMLDims(VM.Src.tilemaps[0].layoutGrid.cellSize));
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.Src.lastViewedNode = VM.Node;
            VM.Src.currentlyViewingLeftUnit = VM.Src.currentlyViewingRightUnit = null;
            if (VM.Node.Unit is { } u) {
                var invertSides = cs is SkillTargetSelCS skill && skill.Unit.Team == u.Team;
                if (u.Team.FlipSprite != invertSides)
                    VM.Src.lastViewedRightUnit = VM.Src.currentlyViewingRightUnit = u;
                else
                    VM.Src.lastViewedLeftUnit = VM.Src.currentlyViewingLeftUnit = u;
            }
            if (cs is UnitActionCS actor)
                actor.UpdateTargetNode(VM.Node, node);
            ServiceLocator.Find<WorldCameraContainer>()
                .TrackTarget(VM.Node.CellCenter, VM.Src.gridCam, new(0.5f, 0.5f, 0.24f, 0.27f, 0));
        }

        void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, PopupUIGroup.Type? popupType) {
            if (popupType is null)
                VM.Src.lastViewedNode = null;
        }

        //void IUIView.OnAddedToNavHierarchy(UINode node) => VM.Src.CurrentIndex = VM.Index;
        //void IUIView.OnRemovedFromNavHierarchy(UINode node) => VM.Src.CurrentIndex = null;

        private readonly Color attackableColor = new(0.65f, 0.25f, 0.15f, 0.8f);
        private readonly Color movableColor = new(0.15f, 0.5f, 0.6f, 0.8f);
        private readonly Color attackOrMoveColor = new(0.2f, 0.35f, 0.6f, 0.8f);
        private readonly Color invisColor = new(0.5f, 0.5f, 0.5f, 0f);
        public override void UpdateHTML() {
            HTML.Q<Label>("Content").text = "";
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
            } else if (VM.Src.CurrSkill?.Reachable(VM.Node) is true) {
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
            Tooltip = sourceNode.MakeTooltip(UINode.SimpleTTGroup($"Unit {Unit.Name}"),XMLUtils.Pivot.Bottom, (_, ve) => {
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
        
        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                Destroy();
                return new UIResult.GoToNode(SourceNode, NoOpIfSameNode:false);
            }
            if (cmd != UICommand.Confirm)
                goto fail;
            if (current.MaybeView<TileWDView>() is not { VM: { Node: { } t } } 
                || !reachable.ContainsKey(t) || (t != Source && t.Unit is not null))
                goto fail;
            
            Menu.lastViewedLeftUnit = Unit;
            
            return new UIResult.AfterTask(async () => {
                await Menu.State.AddDiff(new MoveUnit(Source, t, Unit, currentPath), Menu.AnimCT());
                var actionOpts = new UIColumn(
                    MakeOptsColumnRS(XMLUtils.Pivot.Left, UIBuilderRenderer.ScreenToXML(new Vector2(0.06f, 0) + 
                            Menu.worldRender.PanelToScreen(current.PanelLocation.center))), new UINode?[] {
                        MakeAttackNode(),
                        new FuncNode("Wait", n => new UIResult.ReturnToGroupCaller {
                            OnPostTransition = () => Menu.State.AddDiff(new UnitWait(Unit), Menu.AnimCT()).Log()
                        }), 
                        new FuncNode("Go Back", n => {
                            if (Menu.State.Undo()) {
                                if (Menu.worldUI.QueuedInput is null)
                                    Menu.worldUI.OperateOnResultAnim(new UIResult.GoToNode(SourceNode)).Log();
                                return new UIResult.ReturnToGroupCaller();
                            } else return new UIResult.StayOnNode(true);
                        })
                    }.WithNodeMod(n => n.Prefab = Menu.Config.ActionNodeVTA)) {
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
        
        private FuncNode? MakeAttackNode() {
            if (!Unit.AttackSkills.Any()) return null;
            return new FuncNode("Attack", n => new UIResult.GoToNode(
                new UIColumn(MakeOptsColumnRS(XMLUtils.Pivot.TopLeft, n.XMLLocation.XMaxYMin()),
                    Unit.Skills.Where(s => s.Type is UnitSkillType.Attack).Select(s =>
                        new UINode(new AttackOptionOVView(new(Menu, Unit, s)))
                    )) {
                    Parent = n.Group,
                    DestroyOnLeave = true,
                    OverlayAlphaOverride = 0,
                    OnGoToChild = (g, t) => t is not null ?
                        null :
                        g.Parent!.Visibility
                            .ManualUpdateLocalVisibility(GroupVisibility.TreeVisibleLocalHidden),
                    OnReturnFromChild = g => g.Parent!.Visibility
                        .ManualUpdateLocalVisibility(GroupVisibility.TreeVisible)
                }.WithLocalLeaveHideVisibility()
            ));
        }

        private UIRenderSpace MakeOptsColumnRS(Vector2 pivot, Vector2 leftTop) =>
            new UIRenderConstructed(Menu.overlayUI.MainScreen.AbsoluteTerritory, new(ve => 
                ve.AddColumn().UnsetSize().ConfigureAndPositionAbsolute(pivot, leftTop))).WithFastPopupAnim();


        public override void Destroy() {
            base.Destroy();
            DictCache<Node, double>.Consign(reachable);
            DictCache<Node, Node>.Consign(prev);
            ListCache<Node>.Consign(currentPath);
        }
    }

    private class SkillTargetSelCS : CustomCursorState, ICursorState, ISkillUsage {
        public UINode SourceNode { get; }
        public Unit Unit { get; }
        public IUnitSkill Skill { get; }
        private LocalXMLSRPGExamples Menu { get; }
        private int Rotation { get; set; } = 0;

        public SkillTargetSelCS(ISkillUsage skill, LocalXMLSRPGExamples menu) : base(menu.worldUI) {
            this.SourceNode = menu.worldUI.Current ?? throw new Exception(":(");
            this.Menu = menu;
            this.Unit = skill.Unit;
            this.Skill = skill.Skill;
            Menu.CurrSkill = this;
        }

        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                Destroy();
                Menu.overlayUI.OperateOnResultAnim(new UIResult.ReturnToGroupCaller()).Log();
                return new UIResult.GoToNode(SourceNode, NoOpIfSameNode:false);
            }
            if (cmd != UICommand.Confirm) 
                goto fail;
            if (current.MaybeView<TileWDView>() is not { VM: { Node: { } t } })
                goto fail;
            if (!Skill.Reachable(Unit, t) || Skill.Shape.HitsAnyUnit(Unit.State, t, Rotation) is null)
                goto fail;

            return new UIResult.AfterTask(async () => {
                await Menu.State.AddDiff(new UseUnitSkill(Unit, t, Skill, Rotation), Menu.AnimCT());
                Menu.overlayUI.OperateOnResultFast(Menu.overlayUI.GoToUnselect).Log();
                Destroy();
                return new UIResult.GoToNode(current, NoOpIfSameNode: false);
            });
            
            fail: ;
            return current.Navigate(cmd, this);
        }

        public override void Destroy() {
            base.Destroy();
            Menu.CurrSkill = null;
        }
    }

    private interface ISkillUsage {
        Unit Unit { get; }
        IUnitSkill Skill { get; }

        bool Reachable(Node target) => Skill.Reachable(Unit, target);
    }

}
