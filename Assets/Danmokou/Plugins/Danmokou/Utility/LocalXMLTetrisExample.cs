using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using BagoumLib;
using BagoumLib.Cancellation;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.Core.DInput;
using Danmokou.DMath;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.UIElements;

public static class TetrisHelpers {
    public static decimal MaxMinAvg<T>(this IEnumerable<T> items, Func<T, int> sel) {
        var max = int.MinValue;
        var min = int.MaxValue;
        foreach (var item in items) {
            var x = sel(item);
            if (x > max)
                max = x;
            if (x < min)
                min = x;
        }
        return (max + min) / 2m;
    }
}

public readonly struct Pt {
    public int x { get; init; }
    public int y { get; init; }
    public Pt(int x, int y) {
        this.x = x;
        this.y = y;
    }
    public static Pt operator +(Pt a, Pt b) => new(a.x + b.x, a.y + b.y);
    public static Pt operator -(Pt a, Pt b) => new(a.x - b.x, a.y - b.y);
    public static bool operator ==(Pt a, Pt b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(Pt a, Pt b) => !(a == b);
    public static implicit operator Pt((int x, int y) tup) => new(tup.x, tup.y);

    public override int GetHashCode() => (x, y).GetHashCode();

    public override bool Equals(object? obj) => obj is Pt other && this == other;

    public void Deconstruct(out int x, out int y) {
        x = this.x;
        y = this.y;
    }
}

public record Boxel(Pt Offset) {

    public static implicit operator Boxel((int x, int y) pt) => new(pt);
    public static implicit operator Boxel(Pt pt) => new(pt);
}
public class ProtoBlock {
    public Boxel[] Boxels { get; init; }
    public (decimal x, decimal y) RotationCenter { get; }
    public ProtoBlock(Boxel[] Boxels, (decimal x, decimal y)? RotationCenter = null) {
        this.Boxels = Boxels;
        if (RotationCenter is { } rc)
            this.RotationCenter = rc;
        else {
            var xc = Boxels.MaxMinAvg(pt => pt.Offset.x);
            var yc = Boxels.MaxMinAvg(pt => pt.Offset.y);
            var xcm = Math.Abs(xc % 1m);
            var ycm = Math.Abs(yc % 1m);
            if (xcm != ycm) {
                //The rotation center must be either a block (xc,yc integerial)
                //or the spot between four blocks (xc,yc%1 = 0.5).
                //If the default logic would set the rotation center to a spot between
                // two blocks, then prefer to shift it so that it's located on a block.
                if (xcm == 0.5m) {
                    xc = Math.Sign(xc) * Math.Floor(Math.Abs(xc));
                } else {
                    yc = Math.Sign(yc) * Math.Floor(Math.Abs(yc));
                }
            }
            this.RotationCenter = (xc, yc);
        }
    }
}

public record Grid(Pt Min, Pt Max) {
    public int Width => Max.x - Min.x;
    public int Height => Max.y - Min.y;
}

public enum BlockStatus : int {
    Confirmed = 2,
    Edit = 1,
    New = 0
}
public class Block {
    public ProtoBlock Proto { get; }
    public Grid Grid { get; }
    public Pt Position { get; private set; }
    public int Rotation { get; private set; }
    public BlockStatus Status { get; set; } = BlockStatus.Confirmed;
    public Boxel[] Boxels => Proto.Boxels;
    public Evented<bool> Destroyed { get; } = new(false);

    public Block(ProtoBlock typ, Grid grid) {
        Proto = typ;
        Grid = grid;
        UpdatePosition(new(0, 0), out _);
    }

    public Pt Rotate(Pt offset) {
        var rdelta = RotateDelta((offset.x, offset.y));
        return offset + rdelta;
    }
    public Pt RotateDelta((decimal x, decimal y) offset) {
        var (rotatedOffsetX, rotatedOffsetY) = Rotation switch {
            0 => (offset.x, offset.y),
            90 => (-offset.y, offset.x),
            180 => (-offset.x, -offset.y),
            270 => (offset.y, -offset.x),
            _ => throw new Exception($"Unsupported rotation {Rotation}")
        };
        var rotDeltaX = rotatedOffsetX - offset.x;
        var rotDeltaY = rotatedOffsetY - offset.y;
        if (rotDeltaX != (int) rotDeltaX)
            throw new Exception("Non-integer rotation");
        if (rotDeltaY != (int) rotDeltaY)
            throw new Exception("Non-integer rotation");
        return new Pt((int) rotDeltaX, (int) rotDeltaY);
    }

    public Vector2 RotationCenterPosition {
        get {
            var (dx, dy) = RotateDelta(Proto.RotationCenter);
            return new((float)(Proto.RotationCenter.x + dx), (float)(Proto.RotationCenter.y + dy));
        }
    }

    public Pt UpdatePosition(Pt newPos, out Pt adjustedDelta) {
        adjustedDelta = PushInPosition(newPos);
        return Position = newPos + adjustedDelta;
    }

    public Pt UpdateRotation(int newRotation) {
        newRotation %= 360;
        if (newRotation < 0) newRotation += 360;
        var prevRotCenter = RotateDelta(Proto.RotationCenter);
        Rotation = newRotation;
        return UpdatePosition(Position + prevRotCenter - RotateDelta(Proto.RotationCenter), out _);
    }

    public Pt PushInPosition(Pt pos) {
        var dx = 0;
        var dy = 0;
        void SetNewDelta(ref int delta, int add) {
            if (delta < 0 && add > 0 || delta > 0 && add < 0)
                throw new Exception("Circular!");
            delta += add;
        }
        while (true) {
            foreach (var boxel in Proto.Boxels) {
                var bp = pos + Rotate(boxel.Offset) + (dx, dy);
                if (bp.x < Grid.Min.x) {
                    SetNewDelta(ref dx, Grid.Min.x - bp.x);
                } else if (bp.x >= Grid.Max.x) {
                    SetNewDelta(ref dx, Grid.Max.x - 1 - bp.x);
                } else if (bp.y < Grid.Min.y) {
                    SetNewDelta(ref dy, Grid.Min.y - bp.y);
                } else if (bp.y >= Grid.Max.y) {
                    SetNewDelta(ref dy, Grid.Max.y - 1 - bp.y);
                } else continue;
                goto new_delta;
            }
            break;
            new_delta: ;
        }
        return (dx, dy);
    }

    public Pt BoxelPosition(Boxel b) => Position + Rotate(b.Offset);

    public bool AnyBoxelPositionIs(Pt pt) {
        for (int ii = 0; ii < Boxels.Length; ++ii)
            if (BoxelPosition(Boxels[ii]) == pt)
                return true;
        return false;
    }

    public IEnumerable<Pt> BoxelPositions() =>
        Proto.Boxels.Select(BoxelPosition);

    public bool Overlaps(Block other) {
        if (other == this) return false;
        for (int ii = 0; ii < Boxels.Length; ++ii)
            if (other.AnyBoxelPositionIs(BoxelPosition(Boxels[ii])))
                return true;
        return false;
    }

    public bool OverlapsAny(IList<Block> others) {
        for (int ii = 0; ii < others.Count; ++ii)
            if (Overlaps(others[ii]))
                return true;
        return false;
    }
}

public class LocalXMLTetrisExample : CoroutineRegularUpdater {
    public VisualTreeAsset itemVTA = null!;
    public XMLDynamicMenu Menu { get; private set; } = null!;
    public List<Block> Blocks { get; } = new();
    public Grid Grid { get; } = new Grid((0, 0), (20, 13));
    private UIFreeformGroup BlocksGrp = null!;

    public Block? FirstBlockAt(Pt loc) {
        foreach (var b in Blocks) {
            if (b.AnyBoxelPositionIs(loc))
                return b;
        }
        return null;
    }
    public Sprite[] ItemTypes = new Sprite[6];
    public Sprite[] Shadows = new Sprite[6];
    public ProtoBlock[] BlockTypes = null!;
    private UIGroup[] slots = null!;
    private UINode? LastSlot = null;

    public (Sprite item, Sprite shadow) SpriteForBlock(ProtoBlock b) {
        var ind = BlockTypes.IndexOf(b);
        return (ItemTypes[ind], Shadows[ind]);
    }
    public UINode SlotAt(Pt loc) => slots[loc.y].Nodes[loc.x];
    
    public override void FirstFrame() {
        Menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = Menu.MainScreen;
        var pbI = new ProtoBlock(new Boxel[] {(0, -1), (0, 0), (0, 1), (0, 2)});
        var pbO = new ProtoBlock(new Boxel[] {(0, 0), (1, 0), (0, 1), (1, 1)});
        var pbT = new ProtoBlock(new Boxel[] {(-1, 0), (0, 0), (1, 0), (0, -1)}, (0m, 0m));
        var pbL = new ProtoBlock(new Boxel[] {(0, 2), (0, 1), (0, 0), (1, 0)}, (0.5m, 0.5m));
        var pbJ = new ProtoBlock(new Boxel[] {(0, 2), (0, 1), (0, 0), (-1, 0)}, (-0.5m, 0.5m));
        var pbS = new ProtoBlock(new Boxel[] {(1, 0), (0, 0), (0, -1), (-1, -1)}, (0.5m, -0.5m));
        var pbZ = new ProtoBlock(new Boxel[] {(-1, 0), (0, 0), (0, -1), (1, -1)}, (-0.5m, -0.5m));
        BlockTypes = new[] {
            pbS, pbZ, pbJ, pbL, pbT, pbI, pbO
        };
        BlocksGrp = new UIFreeformGroup(Menu.MainScreen, null) { Interactable = false };
        Menu.FreeformGroup.AddGroupDynamic(BlocksGrp);
        
        var dim = 120f;
        var grid = s.Container.AddColumn().ConfigureAbsolute()
            .WithAbsolutePosition(1920 - 300, 1080).SetWidthHeight(new Vector2(Grid.Width, Grid.Height) * dim);
        grid.style.justifyContent = Justify.SpaceBetween;

        //.reverse so y=0 is at the bottom
        var rows = Grid.Height.Range().Select(_ => grid.AddRow()).Reverse().ToArray();
        slots = Grid.Height.Range().Select(ir => new UIRow(new UIRenderExplicit(s, _ => rows[ir]), 
                Grid.Width.Range().Select(ic => new UINode().WithView(new SlotView(new(this, (ic, ir))))
            ))).Cast<UIGroup>()
            .ToArray();
        
        Menu.FreeformGroup.AddGroupDynamic(new VGroup(slots));
        
        var adderRender = new UIRenderExplicit(s, _ => {
            var ve = s.Container.AddScrollColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 600, 1080).SetWidthHeight(new(600, 1200));
            ve.style.backgroundColor = new Color(0.2f, 0.13f, 0.2f, 0.8f);
            return ve.Q<ScrollView>();
        });
        var adders = new UIColumn(adderRender, BlockTypes.Select(b => new EmptyNode()
            .WithView(n => new ProtoView(new(this, b, n)))));
        
        Menu.FreeformGroup.AddGroupDynamic(new VGroup(adders));
        
        RunRIEnumerator(AddItems());
    }

    public IEnumerator AddItems() {
        yield return null;
        while (!ETime.FirstUpdateForScreen)
            yield return null;
        for (int ii = 0; ii < Grid.Height * Grid.Width; ii += 36) {
            var blk = new Block(BlockTypes[RNG.GetInt(0, BlockTypes.Length)], Grid) { Status = BlockStatus.Confirmed };
            blk.UpdateRotation(90 * RNG.GetInt(0, 4));
            blk.UpdatePosition((ii % Grid.Width, ii / Grid.Width), out _);
            AddBlock(blk);
        }
    }

    public Block AddBlock(Block b) {
        Blocks.Add(b);
        BlocksGrp.AddNodeDynamic(new EmptyNode().WithView(new BlockView(new(this, b))));
        return b;
    }

    public void RemoveBlock(Block b) {
        Blocks.Remove(b);
        b.Destroyed.OnNext(true);
    }

    private class TetrisCS : CustomCursorState, ICursorState {
        public Block Block { get; }
        private int origRot;
        private Pt origLoc;
        public UINode Source { get; }
        public LocalXMLTetrisExample Menu { get; }

        public TetrisCS(Block b, UINode source, LocalXMLTetrisExample menu) : base(menu.Menu) {
            Block = b;
            origLoc = b.Position;
            origRot = b.Rotation;
            Source = source;
            Menu = menu;
            if (b.Status > BlockStatus.Edit)
                b.Status = BlockStatus.Edit;
        }

        public UIResult PointerGoto(UINode current, UINode target) {
            if (target.MaybeView<SlotView>() is not { } view) return UIGroup.SilentNoOp;
            var toLoc = Block.UpdatePosition(view.VM.Loc, out _);
            var toNode = Menu.SlotAt(toLoc);
            if (toNode == current)
                return new UIResult.StayOnNode(true);
            return new UIResult.GoToNode(toNode);
        }

        public UIResult? CustomEventHandling(UINode current) {
            var currPos = Block.Position;
            if (InputManager.GetKeyTrigger(KeyCode.Q).Active) {
                if (currPos != Block.UpdateRotation(Block.Rotation + 90)) {
                    return new UIResult.GoToNode(Menu.SlotAt(Block.Position));
                } else
                    return new UIResult.StayOnNode();
            }
            if (InputManager.GetKeyTrigger(KeyCode.E).Active) {
                if (currPos != Block.UpdateRotation(Block.Rotation - 90)) {
                    return new UIResult.GoToNode(Menu.SlotAt(Block.Position));
                } else
                    return new UIResult.StayOnNode();
            }
            return null;
        }

        public override UIResult Navigate(UINode current, UICommand cmd) {
            if (cmd == UICommand.Back) {
                if (Block.Status >= BlockStatus.Edit) {
                    Block.UpdateRotation(origRot);
                    Block.UpdatePosition(origLoc, out _);
                    Block.Status = BlockStatus.Confirmed;
                } else {
                    Menu.RemoveBlock(Block);
                }
                Destroy();
                return new UIResult.GoToNode(Source, NoOpIfSameNode:false);
            } else if (cmd == UICommand.Confirm) {
                if (Block.OverlapsAny(Menu.Blocks)) {
                    current.SetTooltip(current.MakeTooltip(current.SimpleTTGroup("Can't place a block here :(")));
                    return new UIResult.StayOnNode(true);
                } else {
                    Block.Status = BlockStatus.Confirmed;
                    current.SetTooltip(current.MakeTooltip(current.SimpleTTGroup("Block placed!")));
                    Destroy();
                    return new UIResult.StayOnNode();
                }
            }
            var res = current.Navigate(cmd, this);
            if (res is UIResult.GoToNode { Target: { } t })
                return PointerGoto(current, t);
            return res;
        }
    }

    private class ProtoViewModel : UIViewModel {
        public LocalXMLTetrisExample Src { get; }
        public ProtoBlock Proto { get; }
        private readonly UINode n;
        public ProtoViewModel(LocalXMLTetrisExample src, ProtoBlock proto, UINode n) {
            Src = src;
            Proto = proto;
            this.n = n;
        }

        public override long GetViewHash() => n.Selection.GetHashCode();
    }
    
    /// <summary>
    /// View for the Prototype blocks that can be pulled from the sidebar onto the grid.
    /// </summary>
    private class ProtoView : UIView<ProtoViewModel> {
        private VisualElement shadow = null!;
        public ProtoView(ProtoViewModel viewModel) : base(viewModel) {}

        public override void NodeBuilt(UINode node) {
            base.NodeBuilt(node);
            var (item, shadowSpr) = VM.Src.SpriteForBlock(VM.Proto);
            Node.NodeHTML.ConfigureImage(item);
            Node.NodeHTML.Add(shadow = new VisualElement()
                .ConfigureFloatingImage(shadowSpr, XMLUtils.Pivot.TopLeft)
                .AddTransition("opacity", 0.3f));
            shadow.style.opacity = 0;
            Node.OnConfirm = (n, cs) => {
                if (cs is NullCursorState) {
                    var block = new Block(VM.Proto, VM.Src.Grid) { Status = BlockStatus.New };
                    var tcs = new TetrisCS(block, n, VM.Src);
                    var res = tcs.PointerGoto(n, VM.Src.LastSlot ?? VM.Src.SlotAt((0, 0)));
                    //AddBlock will create a node; we want to do this after Goto
                    // so the node's starting position is set correctly
                    VM.Src.AddBlock(block);
                    return res;
                }
                return null;
            };
        }

        protected override BindingResult Update(in BindingContext context) {
            shadow.style.opacity = (Node.Selection >= UINodeSelection.PopupSource) ? 1 : 0;
            return base.Update(in context);
        }
    }

    private class BlockViewModel : UIViewModel {
        public LocalXMLTetrisExample Src { get; }
        public Block Block { get; }
        public LazyEvented<bool> AnyComponentIsFocused { get; } 
        public LazyEvented<bool> BlockIsConfirmed { get; }

        public BlockViewModel(LocalXMLTetrisExample src, Block block) {
            Src = src;
            Block = block;
            AnyComponentIsFocused = new(() => {
                if (Src.Menu.CursorState.Value is TetrisCS tcs)
                    return tcs.Block == Block;
                for (int ii = 0; ii < Block.Boxels.Length; ++ii) {
                    var n = Src.SlotAt(Block.BoxelPosition(Block.Boxels[ii]));
                    if (n.Selection >= UINodeSelection.PopupSource) {
                        return true;
                    }
                }
                return false;
            });
            BlockIsConfirmed = new(() => Block.Status == BlockStatus.Confirmed);
        }

        public override long GetViewHash() {
            AnyComponentIsFocused.Recompute();
            BlockIsConfirmed.Recompute();
            return (Block.Position, Block.Rotation, AnyComponentIsFocused.Value,
                BlockIsConfirmed.Value).GetHashCode();
        }
    }

    /// <summary>
    /// View for the blocks on the grid.
    /// </summary>
    private class BlockView : UIView<BlockViewModel> {
        private Cancellable? enterAnimCT;
        private Cancellable? flashAnimCT;
        private Cancellable? rotateCT;
        private VisualElement shadow = null!;
        public BlockView(BlockViewModel viewModel) : base(viewModel) { }

        public override void NodeBuilt(UINode node) {
            base.NodeBuilt(node);
            Tokens.Add(VM.Block.Destroyed.Subscribe(b => {
                if (b)
                    Node.Remove();
            }));
            var (itemSprite, shadowSprite) = VM.Src.SpriteForBlock(VM.Block.Proto);
            Node.NodeHTML.Add(shadow = new VisualElement()
                .ConfigureFloatingImage(shadowSprite, XMLUtils.Pivot.TopLeft)
                .AddTransition("opacity", 0.2f)
                .AddTransition("-unity-background-image-tint-color", 0.4f));
            Node.NodeHTML.ConfigureFloatingImage(itemSprite)
                .SetRecursivePickingMode(PickingMode.Ignore)
                .WithAbsolutePositionCentered()
                .AddTransition("left", .2f)
                .AddTransition("top", .2f);
            Tokens.Add(VM.AnyComponentIsFocused.Subscribe(x => {
                if (x) {
                    enterAnimCT?.SoftCancel();
                    enterAnimCT = new();
                    Node.Controller.PlayAnimation(
                        Node.HTML.transform.ScaleTo(1.04f, 0.14f, Easers.EOutSine, enterAnimCT)
                        .Then(() => Node.HTML.transform.ScaleTo(1f, 0.13f, cT: enterAnimCT)));
                    
                    Node.HTML.PlaceInFront(Node.HTML.parent.Children().Last());
                }
                shadow.style.opacity = x ? 1 : 0;
            }));
            Tokens.Add(VM.BlockIsConfirmed.Subscribe(x => {
                flashAnimCT?.SoftCancel();
                if (!x) {
                    flashAnimCT = new();
                    Node.Controller.PlayAnimation(
                        Node.HTML.FadeTo(0.6f, 1f, cT: flashAnimCT)
                            .Then(() => Node.HTML.FadeTo(1f, 1f, cT: flashAnimCT))
                            .Loop()
                    );
                }
            }));
            //do this on construction to avoid the lerp in
            RenderPosition();
        }

        private void RenderPosition() {
            var b = VM.Block;
            var slot = VM.Src.SlotAt(b.Position);
            Node.HTML.WithAbsolutePosition(
                slot.WorldLocation.center
                + b.RotationCenterPosition
                    .PtMul(new(slot.WorldLocation.width, -slot.WorldLocation.height))
            );
        }

        protected override BindingResult Update(in BindingContext context) {
            var showAsFocus = VM.AnyComponentIsFocused;
            Node.HTML.EnableInClassList("focus", showAsFocus);
            Node.HTML.EnableInClassList("group", !showAsFocus);
            RenderPosition();
            
            //CSS transition:rotate is unreliable. Also, CSS rotation is CW by default.
            rotateCT?.Cancel();
            var target = BMath.GetClosestAroundBound(360, Node.HTML.transform.rotation.eulerAngles.z, -VM.Block.Rotation);
            if (IsFirstRender()) {
                Node.HTML.transform.rotation = Quaternion.Euler(0, 0, target);
            } else {
                rotateCT = new();
                Node.Controller.PlayAnimation(Node.HTML.transform.RotateTo(new(0, 0, target),
                    0.12f, Easers.EOutSine, cT: rotateCT));
            }
            
            shadow.style.unityBackgroundImageTintColor = 
                VM.BlockIsConfirmed ? Color.white : 
                VM.Block.OverlapsAny(VM.Src.Blocks) ? new(1f, 0.3f, 0.4f) : new(0.4f, 0.8f, 1f);
            return base.Update(in context);
        }
    }

    public class SlotViewModel : UIViewModel {
        public LocalXMLTetrisExample Src { get; }
        public Pt Loc { get; }
        public SlotViewModel(LocalXMLTetrisExample src, Pt loc) {
            Src = src;
            Loc = loc;
        }
        public override long GetViewHash() => 0;
    }

    /// <summary>
    /// View for the grid slots.
    /// </summary>
    public class SlotView : UIView<SlotViewModel> {
        public override VisualTreeAsset Prefab => VM.Src.itemVTA;
        public SlotView(SlotViewModel viewModel) : base(viewModel) { }

        public override void NodeBuilt(UINode node) {
            base.NodeBuilt(node);
            Node.OnEnter = (n, cs) => VM.Src.LastSlot = n;
            Node.OnConfirm = (n, cs) => {
                if (VM.Src.FirstBlockAt(VM.Loc) is { } b) {
                    _ = new TetrisCS(b, n, VM.Src);
                    return new UIResult.GoToNode(VM.Src.SlotAt(b.Position), false);
                }
                return null;
            };
            Node.PrepareContextMenu((n, cs) => {
                if (cs is NullCursorState && VM.Src.FirstBlockAt(VM.Loc) is { } b) {
                    return new UINode[] {
                        new FuncNode("Delete", () => {
                            VM.Src.RemoveBlock(b);
                            return new UIResult.ReturnToTargetGroupCaller(n);
                        })
                    };
                }
                return null;
            });
            
        }
    }
}

