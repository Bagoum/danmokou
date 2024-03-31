using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using BagoumLib.Transitions;
using Danmokou.Behavior;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.GameInstance;
using Danmokou.UI;
using Danmokou.UI.XML;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;

namespace System.Runtime.CompilerServices {
internal static class IsExternalInit {}
}
public class MyInventorySwapCS : CustomCursorState, ICursorState {
    public int FromIndex { get; }
    public UINode Source { get; }
    public UIGroup? Tooltip { get; }
    public MyInventorySwapCS(int fromIndex, UINode source, LocalXMLInventoryExample menu) : base(menu.Menu) {
        FromIndex = fromIndex;
        Source = source;
        var item = menu.inventory[fromIndex] ?? throw new Exception("Item required for swap cursor");
        Tooltip = source.MakeTooltip(rs => new UIColumn(rs, new EmptyNode { OnBuilt = n => {
            n.HTML.style.backgroundImage = new(item.s);
            n.HTML.SetWidthHeight(new(140, 140));
        } }), (_, ve) => {
            ve.AddToClassList("tooltip-above");
            ve.SetPadding(10, 10, 10, 10);
        });
    }

    public void UpdateTooltipPosition(UINode next) =>
        next.NodeHTML.SetTooltipAbsolutePosition(Tooltip?.Render.HTML);

    public override void Destroy() {
        base.Destroy();
        if (Tooltip is null) return;
        _ = Tooltip.LeaveGroup().ContinueWithSync(() => {
            Tooltip.Destroy();
            (Tooltip.Render as UIRenderConstructed)?.Destroy();
        });
    }


    public override UIResult Navigate(UINode current, UICommand cmd) {
        if (cmd == UICommand.Back) {
            Destroy();
            return new UIResult.GoToNode(Source, NoOpIfSameNode:false);
        }
        return current.Navigate(cmd, this);
    }
}

public record MyInventoryItem(Sprite s, int ct) {
    public int ct { get; set; } = ct;
    public string Name { get; } = s.name;

    public static bool IsSameType(MyInventoryItem? a, MyInventoryItem? b) =>
        a != null && b != null && a.s == b.s;
}

public class LocalXMLInventoryExample : CoroutineRegularUpdater {
    public VisualTreeAsset itemVTA = null!;
    public XMLDynamicMenu Menu { get; private set; } = null!;
    public MyInventoryItem?[] inventory { get; } = new MyInventoryItem?[60];
    public Sprite[] ItemTypes = new Sprite[6];
    
    public override void FirstFrame() {
        Menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = Menu.MainScreen;

        for (int ii = 0; ii < inventory.Length; ii += 3) {
            inventory[ii] = new MyInventoryItem(ItemTypes[RNG.GetInt(0, ItemTypes.Length)], RNG.GetInt(2, 10));
        }

        var w = 10;
        var h = 6;
        var dim = 250f;
        var details = new UIRenderExplicit(s, _ => {
            var ve = s.Container.AddColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 600, 1080).SetWidthHeight(new(600, 800));
            ve.style.backgroundColor = new Color(0.2f, 0.13f, 0.2f, 0.8f);
            return ve;
        });
        
        var grid = s.Container.AddColumn().ConfigureAbsolute()
            .WithAbsolutePosition(1920 - 300, 1080).SetWidthHeight(new Vector2(w, h) * dim);
        grid.style.justifyContent = Justify.SpaceBetween;

        var rows = h.Range().Select(ir => new UIRow(new UIRenderExplicit(s, _ => {
            var row = grid.AddRow();
            return row;
        }), w.Range().Select(ic => {
            var index = ir * w + ic;
            return new UINode() {
                Prefab = itemVTA,
                ShowHideGroup = new UIColumn(details, new UINode {
                    Prefab = XMLUtils.Prefabs.PureTextNode
                }.WithCSS(XMLUtils.small1Class, XMLUtils.fontBiolinumClass)
                .WithView(new LabelView<MyInventoryItem?>(new(() => inventory[index], 
                    item => item != null ? $"Item type: {item.Name}\nCount: {item.ct}" : "No item at this index")))) {
                    Interactable = false
                },
                OnEnter = (n, cs) => {
                    if (cs is MyInventorySwapCS swap)
                        swap.UpdateTooltipPosition(n);
                },
                OnConfirm = (n, cs) => {
                    if (cs is MyInventorySwapCS swap) {
                        if (swap.FromIndex == index)
                            return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                        ref var swapFrom = ref inventory[swap.FromIndex];
                        ref var swapTo = ref inventory[index];
                        if (MyInventoryItem.IsSameType(swapFrom, swapTo)) {
                            if (swapFrom!.ct + swapTo!.ct > 10) {
                                swapFrom.ct += swapTo.ct - 10;
                                swapTo.ct = 10;
                            } else {
                                swapTo.ct += swapFrom.ct;
                                swapFrom = null;
                            }
                        } else
                            (swapFrom, swapTo) = (swapTo, swapFrom);
                        swap.Destroy();
                        n.RemakeTooltip(cs);
                        return new UIResult.StayOnNode();
                    } else {
                        n.SetTooltip(n.MakeTooltip(rs => new UIColumn(rs, new UINode("temporary toolip") 
                            { Prefab = XMLUtils.Prefabs.PureTextNode })));
                        return null;
                    }
                },
            }
            .WithView(new InventorySlotDataView(new(this, index)))
            .PrepareTooltip(() => inventory[index] is {} item ? (LString)$"{item.Name} x{item.ct}" : null)
            .PrepareContextMenu((n, cs) => {
                if (cs is NullCursorState && inventory[index] is {} item) {
                    return new UINode[] {
                        new FuncNode("Move", fn => {
                            _ = new MyInventorySwapCS(index, n, this);
                            return new UIResult.ReturnToTargetGroupCaller(n);
                        })
                    };
                } else return null;
            });
        }))).Cast<UIGroup>().ToArray();

        var g = new VGroup(rows);
        Menu.FreeformGroup.AddGroupDynamic(g);
        
    }

    private class InventorySlotViewModel : UIViewModel {
        private readonly LocalXMLInventoryExample src;
        public int Index { get; }
        public MyInventoryItem? Item => src.inventory[Index];
        public InventorySlotViewModel(LocalXMLInventoryExample src, int index) {
            this.src = src;
            Index = index;
        }

        public override long GetViewHash() => Item?.GetHashCode() ?? 0;
    }

    private class InventorySlotDataView : UIView<InventorySlotViewModel> {
        public InventorySlotDataView(InventorySlotViewModel viewModel) : base(viewModel) { }

        protected override BindingResult Update(in BindingContext context) {
            var title = Node.HTML.Q<Label>("Content");
            if (ViewModel.Item is { } item) {
                title.style.display = DisplayStyle.Flex;
                title.style.backgroundImage = new(item.s);
                title.text = $"{item.ct}";
            } else
                title.style.display = DisplayStyle.None;
            /* proof of concept for changing styling based on current cursor state
            var bg = n.HTML.Q("BG");
            bg.style.backgroundColor = (n.Controller.CursorState.Value is MyInventorySwapCS) ?
                new Color(0.2f, 0.4f, 0.6f) :
                new StyleColor(StyleKeyword.Null);
            */
            return base.Update(in context);
        }
    }

}
