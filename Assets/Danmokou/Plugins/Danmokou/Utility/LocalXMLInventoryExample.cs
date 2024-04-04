using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Functional;
using BagoumLib.Mathematics;
using BagoumLib.Reflection;
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
        var item = menu[fromIndex] ?? throw new Exception("Item required for swap cursor");
        Tooltip = source.MakeTooltip(rs => new UIColumn(rs, new EmptyNode { OnBuilt = n => {
            n.HTML.style.backgroundImage = new(item.s);
            n.HTML.SetWidthHeight(new(140, 140));
        } }), (_, ve) => {
            ve.AddToClassList("tooltip-above");
            ve.SetPadding(10, 10, 10, 10);
        });
    }

    public void UpdateTooltipPosition(UINode next) =>
        next.HTML.SetTooltipAbsolutePosition(Tooltip?.Render.HTML);

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

public enum ItemColor: int {
    Red = 1,
    Blue = 2,
    Green = 3,
    Gold = 4,
    Purple = 5
}

public enum ItemTrait {
    Defense1,
    Defense2,
    Defense3,
    Attack1,
    Attack2,
    Attack3,
    Hot,
    Cold,
    Warm,
    Freezing
}

public record ItemTraitCategory(LString Description, params ItemTrait[] Traits) {
    public static readonly ItemTraitCategory Defense = new("Defense", ItemTrait.Defense3, ItemTrait.Defense2, ItemTrait.Defense1);
    public static readonly ItemTraitCategory Attack = new("Attack", ItemTrait.Attack3, ItemTrait.Attack2, ItemTrait.Attack1);
    public static readonly ItemTraitCategory Temperature = new("Temperature", ItemTrait.Freezing, ItemTrait.Hot, ItemTrait.Cold,
        ItemTrait.Warm);
    public static ItemTraitCategory[] Categories { get; } = {
        Defense, Attack, Temperature
    };

    public bool Matches(ItemTrait trait) => Array.IndexOf(Traits, trait) > -1;
    public bool Matches(InvItem item) => item.Traits.Any(Matches);

    public override string ToString() => Description.Value;
}
public record InvItem(Sprite s, int Count) {
    public int Count { get; set; } = Count;
    public string Name { get; } = s.name;
    public ItemColor Color { get; set; } = ItemColor.Red;
    public ItemTrait[] Traits { get; set; } = Array.Empty<ItemTrait>();

    public static bool IsSameType(InvItem? a, InvItem? b) =>
        a != null && b != null && a.s == b.s;

}
public class LocalXMLInventoryExample : CoroutineRegularUpdater {
    public VisualTreeAsset itemVTA = null!;
    public XMLDynamicMenu Menu { get; private set; } = null!;
    public Sprite[] ItemTypes = new Sprite[6];
    public InvItem?[] _inventory { get; } = new InvItem?[60];
    public List<Lookup<InvItem>> LookupLayers { get; } = new();
    public CompiledLookup<InvItem> Lookup { get; private set; } = null!;
    private Lookup<InvItem>? TabFilter;
    private UIFreeformGroup LayersGrp { get; set; } = null!;

    public void AddLookup(Lookup<InvItem> layer) {
        if (LookupLayers.Count > 0 && LookupLayers[^1].Hidden)
            LookupLayers.Insert(LookupLayers.Count - 1, layer);
        else
            LookupLayers.Add(layer);
        AddToken(layer.WhenDestroyed(() => {
            LookupLayers.Remove(layer);
            RecompileLookup();
        }));
        LayersGrp.AddNodeDynamic(new UINode(new LayerView(new(this, layer))));
        RecompileLookup();
    }

    public void RecompileLookup() => Lookup = CompiledLookup<InvItem>.From(_inventory, LookupLayers);
    public InvItem? this[int ii] => Lookup[ii];
    
    public override void FirstFrame() {
        Menu = ServiceLocator.Find<XMLDynamicMenu>();
        var s = Menu.MainScreen;
        var colors = ReflectionUtils.GetEnumVals<ItemColor>();
        var traits = ReflectionUtils.GetEnumVals<ItemTrait>();

        for (var ii = 0f; ii < _inventory.Length; ii += 1.5f) {
            var tlen = RNG.GetInt(2, 5);
            _inventory[(int)ii] = new InvItem(ItemTypes.Random(), RNG.GetInt(2, 10)) {
                Color = colors.Random(),
                Traits = tlen.Range().Select(_ => traits.Random()).ToArray()
            };
        }

        var w = 10;
        var h = 6;
        var dim = 250f;
        var details = new UIRenderExplicit(s, _ => {
            var ve = s.Container.AddColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 700, 1080 - 550).SetWidthHeight(new(900, 900));
            ve.style.backgroundColor = new Color(0.3f, 0.223f, 0.3f, 0.8f);
            return ve;
        });
        Menu.FreeformGroup.AddGroupDynamic(LayersGrp = new UIFreeformGroup(new UIRenderExplicit(s, _ => {
            var ve = s.Container.AddColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 700, 1080 + 550).SetWidthHeight(new(900, 900));
            ve.style.backgroundColor = new Color(0.2f, 0.13f, 0.2f, 0.8f);
            return ve;
        })));

        //AddLookup(new Lookup<InvItem>.Sort<int>(it => it.Count, "Count", true));
        //AddLookup(new Lookup<InvItem>.Filter(it => it.Color != ItemColor.Gold, "Filter out Gold"));
        //AddLookup(new Lookup<InvItem>.Sort<ItemColor>(it => it.Color, "Color", false));
        RecompileLookup();
        
        var grid = s.Container.AddColumn().ConfigureAbsolute()
            .WithAbsolutePosition(1920 - 550, 1080 + 100).SetWidthHeight(new Vector2(w, h) * dim + new Vector2(0, 240));
        grid.style.justifyContent = Justify.SpaceBetween;

        var tabRender = grid.AddRow().SetHeight(240);
        
        var rows = h.Range().Select(ir => new UIRow(new UIRenderExplicit(s, _ => {
            var row = grid.AddRow();
            return row;
        }), w.Range().Select(ic => {
            var index = ir * w + ic;
            return new UINode(new InventorySlotView(new(this, index))) {
                Prefab = itemVTA,
                ShowHideGroup = new UIColumn(details, new UINode(
                    new LabelView<InvItem?>(new(() => this[index], item => {
                        if (item != null) {
                            var s = $"Item type: {item.Name}\nCount: {item.Count}\nColor: {item.Color}";
                            foreach (var t in item.Traits)
                                s += $"\nTrait: {t}";
                            return s;
                        } else
                            return "No item at this index";
                    }))) {
                    Prefab = XMLUtils.Prefabs.PureTextNode
                }.WithCSS(XMLUtils.small1Class, XMLUtils.fontBiolinumClass)) {
                    Interactable = false
                }
            };
        }))).Cast<UIGroup>().ToArray();

        var gridGroup = new VGroup(rows);
        var tabRow = new UIRow(new UIRenderExplicit(s, _ => tabRender), colors.Cast<ItemColor?>().Prepend(null)
            .Select(x => new UINode(x?.ToString() ?? "All") {
                Prefab = Prefabs.HeaderNode,
            }.Bind(new TabView(new(this, x)))));
        Menu.FreeformGroup.AddGroupDynamic(new VGroup(tabRow, gridGroup));
        Menu.FreeformGroup.AddGroupDynamic(LayersGrp);
        
        Menu.FreeformGroup.AddNodeDynamic(new UIButton("Add Filter", UIButton.ButtonType.Confirm, n => {
            var exclude = new Evented<bool>(false);
            var types = new[] {
                ((LString)"Color", new Selector<ItemColor>(ReflectionUtils.GetEnumVals<ItemColor>(), 
                        c => c.ToString(), Multiselect: true).AsFilterContinuation<InvItem>(
                            (vals, x) => Array.IndexOf(vals, x.Color) > -1, exclude, "Color")
                    ),
                ((LString)"Trait", new Selector<ItemTrait>(ReflectionUtils.GetEnumVals<ItemTrait>(), 
                        c => c.ToString(), Multiselect: true).AsFilterContinuation<InvItem>(
                            (vals, x) => x.Traits.Any(t => Array.IndexOf(vals, t) > -1), exclude, "Trait")
                    ),
                ((LString)"Trait Category", new Selector<ItemTraitCategory>(ItemTraitCategory.Categories, 
                        c => c.Description, Multiselect: true).AsFilterContinuation<InvItem>(
                        (vals, x) => vals.Any(v => v.Matches(x)), exclude, "Trait Cat."))
            };
            var selectType = new Selector<(LString, Continuation<Selector, Lookup<InvItem>.Filter>)>(types, x => x.Item1);
            var nodes = new UINode[types.Length + 3];
            nodes[0] = selectType.SelectorDropdown("Filter by:");
            nodes[1] = new LROptionNode<bool>("Mode", exclude, new (LString, bool)[] {
                ("Include", false), ("Exclude", true)
            });
            //empty passthrough node to keep size of box consistent even when nothing is selected
            nodes[2] = new PassthroughNode("") { VisibleIf = () => !selectType.FirstSelected.Valid };
            for (int ii = 0; ii < types.Length; ++ii) {
                var (desc, sel) = types[ii];
                nodes[ii + 3] = sel.Extract().SelectorDropdown(LString.Format("{0}:", desc));
                nodes[ii + 3].VisibleIf = () => selectType.FirstSelected.Try(out var x) && x.Item2 == sel;
            }
            return PopupUIGroup.CreatePopup(n, "Add Filter", rs => new UIColumn(rs, nodes), 
                new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                new UIButton(LocalizedStrings.Controls.confirm, UIButton.ButtonType.Confirm, b => {
                    this.AddLookup(selectType.FirstSelected.Value.Item2.Realize());
                    return n.ReturnToGroup;
                }) { EnabledIf = () => selectType.FirstSelected.Try(out var sel) && sel.Item2.Extract().IsAnySelected }
            }), (_, ve) => ve.SetWidth(1200));
            
        }) {
            OnBuilt = n => n.HTML.ConfigureAbsolute().WithAbsolutePosition(1800, 200)
        });
        
        Menu.FreeformGroup.AddNodeDynamic(new UIButton("Add Sort", UIButton.ButtonType.Confirm, n => {
            var selector = new Selector<Lookup<InvItem>.Sort>(new Lookup<InvItem>.Sort[] {
                new Lookup<InvItem>.Sort<int>(x => x.Count, "Count"),
                new Lookup<InvItem>.Sort<ItemColor>(x => x.Color, "Color"),
                new Lookup<InvItem>.Sort<string>(x => x.Name, "Name"),
            }, s => s.Feature);
            var reverse = new Evented<bool>(false);
            return PopupUIGroup.CreatePopup(n, "Add Sort", rs => {
                return new UIColumn(rs, 
                    selector.SelectorDropdown("Sort by:"), 
                    new LROptionNode<bool>("Order", reverse, new (LString, bool)[] {
                        ("Asc", false), ("Desc", true)
                    }));
            }, new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                new UIButton("Confirm", UIButton.ButtonType.Confirm, b => {
                    this.AddLookup(selector.FirstSelected.Value with { Reverse = reverse.Value });
                    return n.ReturnToGroup;
                }) { EnabledIf = () => selector.FirstSelected.Valid }
            }), (_, ve) => ve.SetWidth(1200));
        }) {
            OnBuilt = n => n.HTML.ConfigureAbsolute().WithAbsolutePosition(2200, 200)
        });
    }

    private record TabViewModel(LocalXMLInventoryExample Src, ItemColor? Filter) : IConstUIViewModel {
    }
    
    private class TabView : UIView<TabViewModel>, IUIView {
        public TabView(TabViewModel viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.Src.TabFilter?.Destroy();
            VM.Src.TabFilter = new Lookup<InvItem>.Filter(x => x.Color == (VM.Filter ?? x.Color), "", "", false) { Hidden = true };
            VM.Src.AddLookup(VM.Src.TabFilter);
        }
    }

    private class LayerViewModel : UIViewModel, IUIViewModel {
        private LocalXMLInventoryExample Src { get; }
        public Lookup<InvItem> Layer { get; }
        public Event<int> MovedToIndex = new();
        public LayerViewModel(LocalXMLInventoryExample src,  Lookup<InvItem> layer) {
            this.Src = src;
            this.Layer = layer;
        }

        public UIResult? OnConfirm(UINode node, ICursorState cs) {
            Layer.Enabled = !Layer.Enabled;
            Src.RecompileLookup();
            return new UIResult.StayOnNode();
        }

        public UIResult? OnContextMenu(UINode node, ICursorState cs) {
            if (cs is NullCursorState) {
                var idx = Src.LookupLayers.IndexOf(Layer);
                return PopupUIGroup.CreateContextMenu(node, new UINode?[] {
                    (idx > 0) ? new FuncNode("Move up", () => {
                        (Src.LookupLayers[idx], Src.LookupLayers[idx - 1]) =
                            (Src.LookupLayers[idx - 1], Src.LookupLayers[idx]);
                        Src.RecompileLookup();
                        //We don't need to fire MovedToIndex on the other item since
                        // the movement of this item will make the other once naturally have the correct index
                        MovedToIndex.OnNext(idx - 1);
                        return node.ReturnToGroup;
                    }) : null,
                    (idx < Src.LookupLayers.Count - 1 && !Src.LookupLayers[idx+1].Hidden) ? new FuncNode("Move down", () => {
                        (Src.LookupLayers[idx], Src.LookupLayers[idx + 1]) =
                            (Src.LookupLayers[idx + 1], Src.LookupLayers[idx]);
                        Src.RecompileLookup();
                        MovedToIndex.OnNext(idx + 1);
                        return node.ReturnToGroup;
                    }) : null,
                    new FuncNode("Delete", () => {
                        Layer.Destroy();
                        return node.ReturnToGroup;
                    })
                });
            }
            return null;
        }

        public override long GetViewHash() => Layer.GetHashCode();
    }

    private class LayerView : UIView<LayerViewModel> {
        public LayerView(LayerViewModel viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Node.WithCSS(XMLUtils.small1Class, XMLUtils.fontBiolinumClass);
            Node.VisibleIf = () => !VM.Layer.Hidden;
            Tokens.Add(VM.Layer.WhenDestroyed(() => Node.Remove()));
            Tokens.Add(VM.MovedToIndex.Subscribe(idx => Node.MoveToIndex(idx)));
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>().text = VM.Layer.Descr;
            Node.HTML.style.color = VM.Layer.Enabled ? new Color(1, 1, 1, 1) : new Color(0.8f, 0.7f, 0.7f, 0.8f);
            return base.Update(in context);
        }
    }

    private class InventorySlotViewModel : UIViewModel, IUIViewModel {
        public LocalXMLInventoryExample Src { get; }
        public int Index { get; }
        public InvItem? Item => Src[Index];
        public InventorySlotViewModel(LocalXMLInventoryExample src, int index) {
            this.Src = src;
            Index = index;
        }

        public override long GetViewHash() => Item?.GetHashCode() ?? 0;

        public UIResult? OnConfirm(UINode n, ICursorState cs) {
            if (cs is MyInventorySwapCS swap) {
                if (swap.FromIndex == Index)
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                if (Src.Lookup is not CompiledLookup<InvItem>.SourceData inv) {
                    n.SetTooltip(n.MakeTooltip(n.SimpleTTGroup("Cannot reorder items when sort/filter is active")));
                    return null;
                }
                ref var swapFrom = ref inv.Data[swap.FromIndex];
                ref var swapTo = ref inv.Data[Index];
                if (InvItem.IsSameType(swapFrom, swapTo)) {
                    if (swapFrom!.Count + swapTo!.Count > 10) {
                        swapFrom.Count += swapTo.Count - 10;
                        swapTo.Count = 10;
                    } else {
                        swapTo.Count += swapFrom.Count;
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
        }

        public UIResult? OnContextMenu(UINode node, ICursorState cs) {
            if (cs is NullCursorState && Src[Index] is {} item) {
                return PopupUIGroup.CreateContextMenu(node, new UINode[] {
                    new FuncNode("Move", fn => {
                        _ = new MyInventorySwapCS(Index, node, Src);
                        return node.ReturnToGroup;
                    })
                });
            } else return null;
        }

        public UIGroup? Tooltip(UINode node, ICursorState cs, bool prevExists) {
            if (Src[Index] is { } item)
                return node.MakeTooltip(node.SimpleTTGroup($"{item.Name} x{item.Count}"));
            return null;
        }
    }

    /// <summary>
    /// View for each inventory slot (which may itself contain an item).
    /// </summary>
    private class InventorySlotView : UIView<InventorySlotViewModel>, IUIView {
        public override VisualTreeAsset? Prefab => VM.Src.itemVTA;
        public InventorySlotView(InventorySlotViewModel viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            if (cs is MyInventorySwapCS swap)
                swap.UpdateTooltipPosition(node);
        }

        protected override BindingResult Update(in BindingContext context) {
            var title = Node.HTML.Q<Label>("Content");
            if (ViewModel.Item is { } item) {
                title.style.display = DisplayStyle.Flex;
                title.style.backgroundImage = new(item.s);
                title.text = $"{item.Count}";
                title.style.color = item.Color switch {
                    ItemColor.Red => new Color(0.84f, 0.27f, 0.32f),
                    ItemColor.Blue => new Color(0.3f, 0.58f, 0.91f),
                    ItemColor.Green => new Color(0.19f, 0.78f, 0.36f),
                    ItemColor.Gold => new Color(0.98f, 0.83f, 0.17f),
                    ItemColor.Purple => new Color(0.71f, 0.17f, 0.82f),
                    _ => throw new ArgumentOutOfRangeException()
                };
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
