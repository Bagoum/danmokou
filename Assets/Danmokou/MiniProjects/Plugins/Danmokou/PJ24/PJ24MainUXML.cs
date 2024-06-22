using System;
using System.Collections.Generic;
using System.Linq;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Newtonsoft.Json;
using Suzunoya.ADV;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiniProjects.PJ24 {

public class PJ24MainUXML : UIController {
    private abstract record Viewing {
        public abstract bool Matches(ItemInstance inst);
        public record ItemType(Item item) : Viewing {
            public override bool Matches(ItemInstance inst) => inst.Type == item;
        }

        public record Requirement(RecipeComponent cmp) : Viewing {
            public override bool Matches(ItemInstance inst) => cmp.Matches(inst);
        }
    }
    public VisualTreeAsset screenMainVTA = null!;
    public VisualTreeAsset screenInventoryVTA = null!;
    public VisualTreeAsset screenSynth1VTA = null!;
    public VisualTreeAsset screenSynth2VTA = null!;
    public VisualTreeAsset screenSynth3VTA = null!;
    public VisualTreeAsset nodeMainMenuOptionVTA = null!;
    public VisualTreeAsset nodeItemVTA = null!;
    public VisualTreeAsset nodeTextOnlyVTA = null!;
    public VisualTreeAsset nodeSynthComponentVTA = null!;
    public VisualTreeAsset popupVTA = null!;
    public VisualTreeAsset popupButtonVTA = null!;
    private PJ24GameDef.Executing exec = null!;
    private ProposedSynth nextRecipe = null!;
    private readonly Evented<CurrentSynth?> evSynth = new(null);
    private CurrentSynth? Synth => evSynth.Value;
    private Viewing? viewingItem = null;
    private ItemInstance? viewingItemInst = null;
    private UIGroup gSynthItemInstances = null!;
    private UIGroup gItemInstances = null!;
    private UINode synthItemSelOKNode = null!;
    private UINode synthFinalizeNode = null!;
    public SFXConfig craftSFX = null!;
    
    public PJ24GameDef.PJ24ADVData Data => exec.Data;

    private UIScreen ScreenSynth1 = null!;
    private UIScreen ScreenSynth2 = null!;
    private UIScreen ScreenSynth3 = null!;
    private UIScreen ScreenInventory = null!;
    protected override UIScreen?[] Screens => new[] { MainScreen, ScreenSynth1, ScreenSynth2, ScreenSynth3, ScreenInventory };
    protected override bool CaptureFallthroughInteraction => false;
    protected override bool OpenOnInit => false;

    public override void FirstFrame() {
        exec = ServiceLocator.Find<PJ24GameDef.Executing>();
        nextRecipe = new ProposedSynth(exec.Data);
        ScreenSynth1 = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenSynth1VTA,
            Builder = (s, ve) => s.HTML.pickingMode = PickingMode.Ignore
        };
        UIRenderSpace rSynthItemDetails = null!;
        ScreenSynth2 = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenSynth2VTA,
            Builder = (s, ve) => {
                s.HTML.pickingMode = PickingMode.Ignore;
                rSynthItemDetails = new UIRenderExplicit(s, ve => ve.Q("ItemDetails")).WithPopupAnim();
                _ = rSynthItemDetails.HTML;
                rSynthItemDetails.OverrideVisibilityV(false);
            },
        };
        ScreenSynth3 = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenSynth3VTA,
            Builder = (s, ve) => s.HTML.pickingMode = PickingMode.Ignore
        };
        ScreenSynth3.AddFreeformGroup(ServiceLocator.Find<XMLDynamicMenu>().DefaultUnselectorConfirm);
        UIColumn gSynthComponents = null!;
        
        var rSynthItemInstances = new UIRenderExplicit(ScreenSynth2, ve => ve.Q("ItemInstanceList"))
            { VisibleWhenSourcesVisible = true }.WithPopupAnim();
        gSynthItemInstances = new UIColumn(new UIRenderColumn(rSynthItemInstances, 0));
        var gSynthItemSelect = new VGroup(gSynthItemInstances, new UIColumn(rSynthItemInstances, 
                synthItemSelOKNode = new UIButton(null, UIButton.ButtonType.Confirm, n => {
                        Synth!.CommitSelection();
                        if (Synth.FirstUnsatisfiedIndex is { } ind)
                            return new UIResult.GoToNode(gSynthComponents!, ind);
                        return new UIResult.GoToNode(synthFinalizeNode);
                }) {
                EnabledIf = () => Synth?.CurrentComponentSatisfied is true,
                Prefab = popupButtonVTA,
                BuildTarget = rs => rs.Q("ConfirmButton")
            }.Bind(new SynthItemSelConfirmView(new(this))))) {
                EntryNodeOverride = new(() => gSynthItemInstances.EntryNode),
                OnEnter = _ => rSynthItemDetails.OverrideVisibilityV(true),
                OnLeave = _ => {
                    Synth!.CancelSelection();
                    return rSynthItemDetails.OverrideVisibilityV(false);
                },
            }
            .WithLeaveHideVisibility();
        
        var rSynthComponents = new UIRenderExplicit(ScreenSynth2, ve => ve.Q("SynthBoard"));
        gSynthComponents = new UIColumn(new UIRenderColumn(rSynthComponents, 0), 4.Range().Select(i => {
            var view = new SynthComponentView(new(this, i));
            return new UINode(view) {
                Prefab = nodeSynthComponentVTA,
                VisibleIf = () => view.VM.Cmp != null
            };
        }));
        var gSynthConfirm = new UIColumn(rSynthComponents,
            synthFinalizeNode = new UIButton("Synthesize!", UIButton.ButtonType.Confirm, n => {
                    viewingItemInst = Data.ExecuteSynthesis(Synth!);
                    UpdateInventoryHTML(true);
                    ISFXService.SFXService.Request(craftSFX);
                    return new UIResult.GoToScreen(ScreenSynth3) {
                        Options = new() { DelayScreenFadeInRatio = 2.5f },
                        OnPostTransition = () => {
                            exec.Manager.SkipAndFlush();
                            _ = exec.Manager.ExecuteVN(exec.CraftedItem(viewingItemInst), true)
                                .ContinueWithSync(() =>
                                    OperateOnResult(new UIResult.ReturnToScreenCaller(3), UITransitionOptions.Default));
                            exec.UpdateDataV(_ => { });
                        }
                    };
                }) {
                EnabledIf = () => Synth?.CurrentSelection is null && Synth?.AllComponentsSatisfied() is true,
                Prefab = popupButtonVTA,
                BuildTarget = rs => rs.Q("ConfirmButton")
            });
        gSynthItemSelect.Parent = gSynthComponents;
        ScreenSynth2.SetFirst(new VGroup(gSynthComponents, gSynthConfirm) {
            EntryNodeOverride = new(() => gSynthComponents.EntryNode),
            OnEnter = _ => {
                ShowTraits(ScreenSynth2.HTML.Q("Traits"), true, new());
                return null;
            }
        });
        
        var rCraftCount = new UIRenderExplicit(ScreenSynth1, ve => ve.Q("CraftingCount")) 
            { VisibleWhenSourcesVisible = true }.WithPopupAnim();
        var gCraftCount = new UIColumn(rCraftCount, new LROptionNode<int>("", 
            new PropTwoWayBinder<int>(nextRecipe, "Count", nextRecipe),
            () => nextRecipe.MaxCount is var mc and > 0 ?
                mc.Range()
                .Select(i => ((LString)($"{i + 1} / {mc}"), i + 1)).ToArray() :
                new[] { ((LString)"1 / 0", 1) }) {
            Builder = ve => ve.Q("CountSelector"),
            OnConfirm = (n, cs) => {
                Synth?.Dispose();
                evSynth.Value = nextRecipe.StartSynth();
                Listen(Synth!.SelectionChanged, _ => 
                    ShowTraits(ScreenSynth2.HTML.Q("Traits"), true, Alchemy.CombineTraits(Synth.Selected)));
                return new UIResult.GoToNode(ScreenSynth2);
            }
        }).WithLeaveHideVisibility();
        var rRecipeList = new UIRenderExplicit(ScreenSynth1, ve => ve.Q("RecipeList")) 
            { VisibleWhenSourcesVisible = true }.WithPopupAnim();
        
        var gRecipeList = new UIColumn(new UIRenderColumn(rRecipeList, 0), Item.Items.Select(item => {
            if (item.Recipe is null)
                return null;
            return new UINode(new RecipeNodeView(new(this, item))) {
                Prefab = nodeItemVTA,
                OnConfirm = (n, cs) => Data.NumCanCraft(item.Recipe) > 0 ? 
                    new UIResult.GoToNode(gCraftCount) : new UIResult.StayOnNode(true)
            };
        })) { OverrideRenderVisibilityOnGoToChild = rRecipeList };
        gCraftCount.Parent = gRecipeList;
        ScreenSynth1.SetFirst(gRecipeList);
        
        ScreenInventory = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenInventoryVTA,
            Builder = (s, ve) => s.HTML.pickingMode = PickingMode.Ignore
        };
        var rItemInstances = new UIRenderExplicit(ScreenInventory, ve => ve.Q("ItemInstanceList"))
            { VisibleWhenSourcesVisible = true }.WithPopupAnim();
        gItemInstances = new UIColumn(new UIRenderColumn(rItemInstances, 0)).WithLeaveHideVisibility();
        
        var rItemTypes = new UIRenderExplicit(ScreenInventory, ve => ve.Q("ItemTypeList"))
            { VisibleWhenSourcesVisible = true }.WithPopupAnim();
        var gItemTypes = new UIColumn(new UIRenderColumn(rItemTypes, 0), 
            Item.Items.Select(item => new TransferNode(null, gItemInstances) {
                Prefab = nodeItemVTA, 
                VisibleIf = () => Data.NumHeld(item) > 0,
            }.Bind(new ItemTypeNodeView(new(this, item))))
        ) { OverrideRenderVisibilityOnGoToChild = rItemTypes };
        gItemInstances.Parent = gItemTypes;
        ScreenInventory.SetFirst(gItemTypes);
        
        foreach (var item in Data.Inventory.Values.SelectMany(x => x))
            ItemAdded(item);
        Listen(Data.ItemAdded, ItemAdded);
        
        MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenMainVTA,
            Builder = (s, ve) => {
                s.HTML.pickingMode = PickingMode.Ignore;
                s.HTML.Q("MainMenuOptions").Q("Content").RemoveFromHierarchy();
            }
        };
        var mainCol = new UIColumn(new UIRenderColumn(MainScreen, 0),
            new TransferNode("Synthesize", ScreenSynth1) { Prefab = nodeMainMenuOptionVTA }.WithCSS("large"),
            new UINode("Requests") { Prefab = nodeMainMenuOptionVTA }.WithCSS("large"),
            new TransferNode("Inventory", ScreenInventory) { Prefab = nodeMainMenuOptionVTA }.WithCSS("large"),
            new FuncNode("Do Nothing", n => {
                var p = PopupUIGroup.CreatePopup(n, "It's 11 AM.\nGo back to sleep?",
                    r => new UIColumn(r, new UINode(
                                $"Today is {Data.Date.AsMDDate}. If you do nothing for the entire day, " +
                                $"it will become {(Data.Date+1).AsMDDate}.")
                            { Prefab = nodeTextOnlyVTA },
                        Data.Phase.IsComplete ? null : 
                            new UINode($"You still haven't completed all the requests for this period " +
                                       $"(deadline {Data.Phase.Deadline.AsMDDate}).")
                                { Prefab = nodeTextOnlyVTA },
                        new UINode("Are you sure you want to waste 24 hours?\n" +
                                   "Remember- work has a time limit, and so does life.")
                            { Prefab = nodeTextOnlyVTA }
                        ) { Interactable = false },
                    new PopupButtonOpts.Centered(new[] {
                        new UIButton("No", UIButton.ButtonType.Cancel, UIButton.GoBackCommand(n))
                                {Prefab = popupButtonVTA}
                            .WithRootView(r => r.DisableAnimations()),
                        new UIButton("Yes", UIButton.ButtonType.Confirm, _ => {
                            exec.UpdateDataV(d => d.Date += 1);
                            return n.ReturnToGroup;
                        }) {Prefab = popupButtonVTA}
                            .WithRootView(r => r.DisableAnimations())
                    }), popupVTA);
                return p;
            }) { Prefab = nodeMainMenuOptionVTA }.WithCSS("large"),
            new FuncNode("Pause Menu", () => {
                    ServiceLocator.Find<IPauseMenu>().QueueOpen();
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.Silent);
                })
                { Prefab = nodeMainMenuOptionVTA }.WithCSS("large")
        );
        
        base.FirstFrame();
        
        var calendars = Screens.SelectNotNull(s => s?.HTML.Q("Calendar")).ToList();

        void UpdateSynthesisHTML_1() {
            foreach (var c in calendars) {
                c.Q<Label>("Phase").text = Data.Phase.Title;
                c.Q<Label>("Today").text = $"今日  {Data.Date}";
                c.Q<Label>("Deadline").text = $"〆切  {Data.Phase.Deadline}";
            }
            if (ScreenSynth1.ScreenIsActive) {
                var ingrs = nextRecipe.Recipe?.Components ?? Array.Empty<RecipeComponent>();
                var xml = ScreenSynth1.HTML.Q("RecipeDetails");
                xml.Q<Label>("Time").text = $"{nextRecipe.Recipe?.Time ?? 0:F2}日間";
                var xmlIngrs = xml.Query(className: "ingredient").ToList();
                for (int ii = 0; ii < xmlIngrs.Count; ++ii) {
                    var ut = xmlIngrs[ii].Q(className: "underlinetext");
                    ut.EnableInClassList("empty", ii >= ingrs.Length);
                    ut.EnableInClassList("uncraftable", ii < ingrs.Length && !Data.Satisfied(ingrs[ii]));
                    if (ii < ingrs.Length) {
                        xmlIngrs[ii].Q<Label>("Text").text = ingrs[ii].Describe();
                        xmlIngrs[ii].Q<Label>("Count").text = ingrs[ii].Count.ToString();
                    }
                }
                ShowCategories(nextRecipe.Item, xml);
                ShowEffects(nextRecipe.Item, xml);
            }
        }

        Listen(exec.DataChanged, d => {
            UpdateSynthesisHTML_1();
            nextRecipe.ModelUpdated();
        });
        Listen(((IVersionedUIViewModel)nextRecipe).EvModelUpdated, _ => UpdateSynthesisHTML_1());
        Listen(exec.Manager.ADVState, st => {
            if (st == ADVManager.State.Investigation) {
                OpenWithAnimationV();
            } else if (st == ADVManager.State.Dialogue) {
                CloseWithAnimationV();
            }
        });
    }
    
    void ShowMeter(VisualElement container, int? low, int? high, bool applyClass = true) {
        container.EnableInClassList("metered", true);
        var selMeter = container.Q("SelectedMeter");
        selMeter.style.left = (low ?? 0f).Percent();
        if (low is null && high is null)
            selMeter.style.right = 100f.Percent();
        else
            selMeter.style.right = (100 - (high ?? 100f)).Percent();
    }

    void ShowCategories(Item? item, VisualElement xml) {
        var cats = item?.Categories ?? Array.Empty<(Category, int)>();
        var xmlCats = xml.Query(className: "category").ToList();
        for (int ii = 0; ii < xmlCats.Count; ++ii) {
            xmlCats[ii].Q(className: "underlinetext").EnableInClassList("empty", ii >= cats.Length);
            if (ii < cats.Length) {
                xmlCats[ii].Q<Label>("Text").text = cats[ii].category.Print();
                ShowMeter(xmlCats[ii], 0, cats[ii].score);
            }
        }
    }

    void ShowEffects(Item? item, VisualElement xml) {
        var ingrs = item?.Recipe?.Components ?? Array.Empty<RecipeComponent>();
        var xmlEffs = xml.Query(className: "effect").ToList();
        var ei = 0;
        var allEffs = item?.Recipe?.Effects ?? Array.Empty<EffectRequirement[]?>();
        for (int ii = 0; ii < allEffs.Length && ei < xmlEffs.Count; ++ii) {
            if (allEffs[ii] is not { } ingrEffs) continue;
            xmlEffs[ei].Q(className: "underlinetext").EnableInClassList("empty", false);
            xmlEffs[ei].EnableInClassList("metered", false);
            xmlEffs[ei++].Q<Label>("Text").text = ingrs[ii].Describe();
            foreach (var eff in ingrEffs) {
                xmlEffs[ei].Q(className: "underlinetext").EnableInClassList("empty", false);
                ShowMeter(xmlEffs[ei], eff.MinScore, eff.MaxScore);
                xmlEffs[ei++].Q<Label>("Text").text = "  " + eff.Result.Name;
                if (ei >= xmlEffs.Count) break;
            }
        }
        for (; ei < xmlEffs.Count; ++ei)
            xmlEffs[ei].Q(className: "underlinetext").EnableInClassList("empty", true);
    }

    public void ShowTraits(VisualElement xml, bool showContainer, List<TraitInstance> traits) {
        xml = xml.Q("TraitContainer");
        xml.style.display = showContainer.ToStyle();
        var xmlTraits = xml.Query(className: "trait").ToList();
        for (int ii = 0; ii < xmlTraits.Count; ++ii) {
            xmlTraits[ii].EnableInClassList("metered", false);
            xmlTraits[ii].Q(className: "underlinetext").EnableInClassList("empty", ii >= traits.Count);
            if (ii < traits.Count)
                xmlTraits[ii].Q<Label>("Text").text = traits[ii].Type.Name;
        } 
    }

    public void UpdateInventoryHTML(VisualElement xml) {
        var item = viewingItemInst?.Type ?? (viewingItem as Viewing.ItemType)?.item;
        ShowCategories(item, xml);
        ShowTraits(xml, viewingItemInst is not null || item is null, 
            viewingItemInst?.Traits ?? new());
        if (viewingItemInst is { } inst) {
            var xmlEffs = xml.Query(className: "effect").ToList();
            for (int ii = 0; ii < xmlEffs.Count; ++ii) {
                xmlEffs[ii].EnableInClassList("metered", false);
                xmlEffs[ii].Q(className: "underlinetext").EnableInClassList("empty", ii >= inst.Effects.Count);
                if (ii < inst.Effects.Count)
                    xmlEffs[ii].Q<Label>("Text").text = inst.Effects[ii].Type.Name;
            }
        } else ShowEffects(item, xml);
    }
    public void UpdateInventoryHTML(bool updateSynth3 = false) {
        if (ScreenSynth3.ScreenIsActive || updateSynth3)
            UpdateInventoryHTML(ScreenSynth3.HTML.Q("ItemDetails"));
        if (ScreenSynth2.ScreenIsActive)
            UpdateInventoryHTML(ScreenSynth2.HTML.Q("ItemDetails"));
        if (ScreenInventory.ScreenIsActive)
            UpdateInventoryHTML(ScreenInventory.HTML.Q("ItemDetails"));
    }
    
    public void ItemAdded(ItemInstance item) {
        gSynthItemInstances.AddNodeDynamic(new UINode(new ItemInstSelNodeView(new(this, item))) {
            Prefab = nodeItemVTA, 
            VisibleIf = () => (viewingItem as Viewing.Requirement)?.Matches(item) is true &&
                              Synth?.IsSelectedForOther(item) is not true,
        });
        gItemInstances.AddNodeDynamic(new UINode(new ItemInstNodeView(new(this, item))) {
            Prefab = nodeItemVTA, 
            VisibleIf = () => (viewingItem as Viewing.ItemType)?.Matches(item) is true
        });
    }

    [ContextMenu("bump date")]
    public void BumpDate() {
        exec.UpdateDataV(d => d.Date += 1);
    }
    [ContextMenu("add nuts")]
    public void AddNuts() {
        exec.UpdateDataV(d => d.AddItem(new(Item.BagOfNuts.S, new() { new(Effect.ThreeColor.S), new(Effect.Fragile.S)})));
    }

    private class SynthComponentView : UIView<SynthComponentView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24MainUXML S { get; }
            public int Index { get; }
            public RecipeComponent? Cmp => S.Synth?.Recipe.Components.Try(Index);
            
            public Model(PJ24MainUXML s, int index) {
                S = s;
                Index = index;
            }

            UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                if (S.Synth is null) throw new Exception("Confirm on synth component with no synth");
                S.viewingItem = new Viewing.Requirement(Cmp!);
                S.Synth.StartSelecting(Index);
                if (S.gSynthItemInstances.HasEntryNode) {
                    return new UIResult.GoToNode(S.gSynthItemInstances);
                } else {
                    S.viewingItem = null;
                    S.Synth.CancelSelection();
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                }
            }

            public override long GetViewHash() => (S.Synth?.Version, S.Synth?.Recipe.Result, S.Synth?.Count).GetHashCode();
        }

        public SynthComponentView(Model viewModel) : base(viewModel) { }
        
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.Cmp is { } cmp) {
                var syn = VM.S.Synth!;
                var (sel, req) = syn.ComponentReq(VM.Index);
                Node.HTML.Q<Label>("Requirement").text = $"{cmp.Describe()} {sel}/{req}";
                var hasEffect = syn.Recipe.HasEffects(VM.Index);
                if (sel > 0) {
                    var effects = syn.Recipe.DetermineEffects(syn.Selected[VM.Index], VM.Index, out var score);
                    VM.S.ShowMeter(Node.HTML, null, score, false);
                    Node.HTML.Q<Label>("Effect").text = effects?.Count > 0 ?
                        string.Join(", ", effects.Select(x => x.Type.Name)) :
                        "No effects";
                } else {
                    VM.S.ShowMeter(Node.HTML, null, null, false);
                    Node.HTML.Q<Label>("Effect").text = hasEffect ? "[Possible effects]" : "[No effects]";
                }
            }
            return base.Update(in context);
        }
    }

    public class ObjViewModel<T> : VersionedUIViewModel, IUIViewModel {
        public PJ24MainUXML S { get; }
        public T Val { get; }

        public ObjViewModel(PJ24MainUXML s, T val) {
            S = s;
            Val = val;
        }
    }

    private class ItemInstNodeView : UIView<ObjViewModel<ItemInstance>>, IUIView {
        public ItemInstNodeView(ObjViewModel<ItemInstance> viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>("Name").text = VM.Val.Type.Name;
            Node.HTML.Q<Label>("Count").style.display = DisplayStyle.None;
            return base.Update(in context);
        }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Tokens.Add(VM.Val.WhenDestroyed(() => Node.Remove()));
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingItemInst = VM.Val;
            VM.S.UpdateInventoryHTML();
        }
    }

    private class ItemInstSelNodeView : UIView<ItemInstSelNodeView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24MainUXML S { get; }
            public ItemInstance Val { get; }
            private int ver = 0;

            public Model(PJ24MainUXML s, ItemInstance val) {
                S = s;
                Val = val;
            }

            public override long GetViewHash() => (S.Synth?.Version, ver).GetHashCode();

            UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                if (S.Synth!.ChangeSelectionForCurrent(Val) is null)
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                ++ver;
                if (S.Synth!.CurrentComponentSatisfied)
                    return new UIResult.GoToNode(S.synthItemSelOKNode);
                return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
            }
        }

        public ItemInstSelNodeView(Model viewModel) : base(viewModel) {}
        
        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>("Name").text = VM.Val.Type.Name;
            var ct = Node.HTML.Q<Label>("Count");
            if (VM.S.Synth?.IsSelectedForCurrent(VM.Val) is true) {
                ct.text = "X";
                ct.style.display = DisplayStyle.Flex;
            } else
                ct.style.display = DisplayStyle.None;
            return base.Update(in context);
        }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            Tokens.Add(VM.Val.WhenDestroyed(() => Node.Remove()));
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingItemInst = VM.Val;
            VM.S.UpdateInventoryHTML();
        }
        void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool _) {
            VM.S.viewingItemInst = null;
            VM.S.UpdateInventoryHTML();
        }
    }
    
    private class ItemTypeNodeView : UIView<ObjViewModel<Item>>, IUIView {
        public ItemTypeNodeView(ObjViewModel<Item> viewModel) : base(viewModel) {
            DirtyOn(VM.S.exec.DataChanged);
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>("Name").text = VM.Val.Name;
            Node.HTML.Q<Label>("Count").text = VM.S.Data.NumHeld(VM.Val).ToString();
            return base.Update(in context);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingItem = new Viewing.ItemType(VM.Val);
            VM.S.viewingItemInst = null;
            VM.S.UpdateInventoryHTML();
        }
    }

    private class SynthItemSelConfirmView : UIView<SynthItemSelConfirmView.Model> {
        public class Model : UIViewModel {
            public PJ24MainUXML S { get; }
            public Model(PJ24MainUXML s) {
                S = s;
            }

            public override long GetViewHash() => (S.Synth?.Version, S.Synth?.CurrentSelection).GetHashCode();
        }

        public SynthItemSelConfirmView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.S.Synth is { CurrentSelection: { } sel } syn) {
                var (has, req) = syn.ComponentReq(sel);
                Node.HTML.Q<Label>().text = has >= req ? "OK" : $"{has}/{req}";
            }
            return base.Update(in context);
        }
    }
    
    private class RecipeNodeView : ItemTypeNodeView, IUIView {
        public RecipeNodeView(ObjViewModel<Item> viewModel) : base(viewModel) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.nextRecipe.Recipe = VM.Val.Recipe;
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.EnableInClassList("uncraftable", VM.S.Data.NumCanCraft(VM.Val.Recipe!) == 0);
            return base.Update(in context);
        }
    }
    
}


public class ProposedSynth : VersionedUIViewModel {
    private Recipe? _recipe;
    [JsonIgnore]
    public Recipe? Recipe {
        get => _recipe;
        set {
            if ((_recipe = value) != null) {
                MaxCount = Data.NumCanCraft(_recipe);
                Count = Math.Min(Count, MaxCount);
                ModelUpdated();
            }
        }
    }
    public Item? Item => Recipe?.Result;
    public int Count { get; set; } = 1;
    public int MaxCount { get; private set; } = 1;
    private PJ24GameDef.PJ24ADVData Data { get; }

    public ProposedSynth(PJ24GameDef.PJ24ADVData data) {
        Data = data;
    }

    public CurrentSynth StartSynth() => new(Recipe ?? throw new Exception("No recipe selected"), Count);
}
}
