using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Tasks;
using BagoumLib.Reflection;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scriptables;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Newtonsoft.Json;
using Suzunoya.ADV;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.UIElements;

namespace MiniProjects.PJ24 {

public class PJ24CraftingUXML : UIController {
    public abstract record Viewing {
        public abstract bool Matches(ItemInstance inst);
        public record Inventory(Item? Item) : Viewing {
            public override bool Matches(ItemInstance inst) => Item is null || Item == inst.Type;
        }

        public record Synth(Recipe Recipe, int Count) : Viewing {
            public Synth(ProposedSynth ps) : this(ps.Recipe ?? throw new Exception("No recipe selected"), ps.Count) { }
            
            public Evented<int> Version { get; } = new(0);

            public List<ItemInstance>[] Selected { get; } = 
                Recipe.Components
                    .Select(x => new List<ItemInstance>())
                    .ToArray();
            public int? CurrentSelection { get; private set; }
            public int CurrentSelectionOrThrow =>
                CurrentSelection ?? throw new Exception("Not currently selecting ingredients for a recipe component");

            public bool CurrentComponentSatisfied =>
                CurrentSelection is {} sel && ComponentSatisfied(sel);
            public (int selected, int required) ComponentReq(int index) => 
                (Selected[index].Count, Recipe.Components[index].Count * Count);
            public bool ComponentSatisfied(int index) {
                var (sel, req) = ComponentReq(index);
                return sel == req;
            }
            public bool AllComponentsSatisfied() {
                for (int ii = 0; ii < Selected.Length; ++ii)
                    if (!ComponentSatisfied(ii))
                        return false;
                return true;
            }

            public int? FirstUnsatisfiedIndex {
                get {
                    for (int ii = 0; ii < Selected.Length; ++ii)
                        if (!ComponentSatisfied(ii))
                            return ii;
                    return null;
                }
            }
            
            public override bool Matches(ItemInstance inst) => 
                CurrentSelection is not {} ind || (Recipe.Components[ind].Matches(inst) && !IsSelectedForOther(inst));

            /// <summary>
            /// Returns true if the item is selected for a component index other than the one currently being edited.
            /// </summary>
            public bool IsSelectedForOther(ItemInstance inst) {
                for (int ii = 0; ii < Selected.Length; ++ii) {
                    if (ii != CurrentSelection && Selected[ii] is { } lis) {
                        for (int jj = 0; jj < lis.Count; ++jj)
                            if (lis[jj] == inst)
                                return true;
                    }
                }
                return false;
            }

            public bool IsSelectedForCurrent(ItemInstance inst) =>
                CurrentSelection is { } sel && Selected[sel].Contains(inst);
            
            public void StartSelecting(int index) {
                Selected[index] = new();
                CurrentSelection = index;
                ++Version.Value;
            }
            
            /// <summary>
            /// Select the item if it is currently unselected,
            /// or unselect it if it is currently selected.
            /// </summary>
            /// <returns>True if the item was selected, false if it was unselected,
            /// or null if the item could not be selected because enough items have already been selected.</returns>
            public bool? ChangeSelectionForCurrent(ItemInstance inst) {
                var lis = Selected[CurrentSelectionOrThrow];
                if (lis.Remove(inst)) {
                    ++Version.Value;
                    return false;
                }
                if (CurrentComponentSatisfied)
                    return null;
                lis.Add(inst);
                ++Version.Value;
                return true;
            }
            
            
            public void CancelSelection() {
                if (CurrentSelection is not { } sel) return;
                Selected[sel] = new();
                CurrentSelection = null;
                ++Version.Value;
            }

            public void CommitSelection() {
                CurrentSelection = null;
                ++Version.Value;
            }
        }
        
        public record Request(PJ24.Request Req) : Viewing {
            public override bool Matches(ItemInstance inst) => Req.Matches(inst);
            public int Version { get; private set; } = 0;

            public List<ItemInstance> Selected { get; } = new();

            public bool Satisfied => Selected.Count == Req.ReqCount;
    
            public bool IsSelected(ItemInstance inst) => Selected.Contains(inst);
    
            /// <summary>
            /// Select the item if it is currently unselected,
            /// or unselect it if it is currently selected.
            /// </summary>
            /// <returns>True if the item was selected, false if it was unselected,
            /// or null if the item could not be selected because enough items have already been selected.</returns>
            public bool? ChangeSelection(ItemInstance inst) {
                if (Selected.Remove(inst)) {
                    ++Version;
                    return false;
                }
                if (Satisfied)
                    return null;
                Selected.Add(inst);
                ++Version;
                return true;
            }
        }
    }

    public VisualTreeAsset screenPersistentUIVTA = null!;
    public VisualTreeAsset screenMainVTA = null!;
    public VisualTreeAsset screenInventoryVTA = null!;
    public VisualTreeAsset screenRequestVTA = null!;
    public VisualTreeAsset screenSynth1VTA = null!;
    public VisualTreeAsset screenSynth2VTA = null!;
    public VisualTreeAsset screenSynth3VTA = null!;
    public VisualTreeAsset nodeMainMenuOptionVTA = null!;
    public VisualTreeAsset nodeItemVTA = null!;
    public VisualTreeAsset nodeTextOnlyVTA = null!;
    public VisualTreeAsset nodeSynthComponentVTA = null!;
    public VisualTreeAsset popupVTA = null!;
    public VisualTreeAsset popupButtonVTA = null!;
    public NamedSprite[] sprites = null!;
    public Dictionary<Item, Sprite> ItemConfig { get; private set; } = null!;
    private PJ24GameDef.Executing exec = null!;
    private ProposedSynth nextRecipe { get; set; } = null!;
    private Viewing.Synth? Synth => viewing as Viewing.Synth;
    private Viewing.Request? Submission => viewing as Viewing.Request;
    private Viewing? viewing = null;
    private ItemInstance? viewingInst = null;
    private UINode requestItemSelOKNode = null!;
    private UINode synthItemSelOKNode = null!;
    private UINode synthFinalizeNode = null!;
    public SFXConfig craftSFX = null!;
    public SFXConfig reqSubmitSFX = null!;
    
    public PJ24GameDef.PJ24ADVData Data => exec.Data;
    private LookupHelper<ItemInstance> InventoryView { get; set; } = null!;
    private Lookup<ItemInstance>? filter;
    private Lookup<ItemInstance>? sort;
    private void ClearFilterSort() {
        filter?.Destroy();
        sort?.Destroy();
        filter = null;
        sort = null;
    }
    private UIScreen PersistentScreen = null!;
    private UIScreen ScreenSynth1 = null!;
    private UIScreen ScreenSynth2 = null!;
    private UIScreen ScreenSynth3 = null!;
    private UIScreen ScreenInventory = null!;
    private UIScreen ScreenRequest = null!;
    protected override UIScreen?[] Screens => new[] { MainScreen, PersistentScreen, ScreenSynth1, ScreenSynth2, ScreenSynth3, ScreenInventory, ScreenRequest };
    protected override bool CaptureFallthroughInteraction => false;
    protected override bool OpenOnInit => false;
    
    //need to wait for adv data finalization before creating menu,
    // but if there is no loading process, then adv data finalization occurs immediately.
    //use this delayed structure to call Setup only when FirstFrame AND ADVDataFinalized are called.
    public JointCallback<PJ24CraftingUXML, PJ24GameDef.Executing, Unit> SetupCB { get; } = new((a, b) => a.Setup(b));

    private void Awake() {
        ItemConfig = sprites.ToDictionary(kv => Item.FindByName(kv.name), kv => kv.sprite);
        RegisterService(this);
    }

    public override void FirstFrame() {
        base.FirstFrame();
        SetupCB.SetFirst(this);
    }

    private bool ItemInstVisible(ItemInstance? v) {
        if (v is null) return false;
        if (viewing?.Matches(v) is false) return false;
        return true;
    }

    private readonly Evented<bool> exclude = new(false);
    private readonly Evented<bool> reverse = new(false);
    private Selector<Continuation<Selector, Lookup<ItemInstance>.Filter>>? filterSel;
    private Selector<Lookup<ItemInstance>.Sort>? sortSel;
    private PopupUIGroup ShowSortFilterMenu(UINode source) {
        //filters
        if (filterSel is null) {
            var filters = new[] {
                //TODO add some support for hiding elements of selectors based on adding a view to the node in the dropdown
                new Selector<Trait>(Trait.Traits, c => c.Name, Multiselect: true)
                    .AsFilterContinuation<ItemInstance>((vals, x) => x.HasAnyTrait(vals), exclude, "Trait"),
                new Selector<Item>(Item.Items, c => c.Name, Multiselect: true)
                    .AsFilterContinuation<ItemInstance>((vals, x) => vals.IndexOf(x.Type) > -1, exclude, "Item"),
                new Selector<Category>(ReflectionUtils.GetEnumVals<Category>(), Multiselect: true)
                    .AsFilterContinuation<ItemInstance>(
                        (vals, x) => vals.Any(cat => x.Type.CategoryScore(cat) is not null), exclude, "Category")
            };
            filterSel = new Selector<Continuation<Selector, Lookup<ItemInstance>.Filter>>(filters, x => x.Obj.Description!);
        }
        var nodes = new List<UINode>() {
            filterSel.SelectorDropdown("Filter by:"),
            exclude.MakeSelector("Mode:", "Include", "Exclude"),
            //empty passthrough node to keep size of box consistent even when no filter is selected
            new PassthroughNode("\t") { VisibleIf = () => !filterSel.FirstSelected.Valid }
        };
        foreach (var f in filterSel.Values) {
            nodes.Add(f.Obj.SelectorDropdown());
            nodes[^1].VisibleIf = () => filterSel.FirstSelected.Try(out var sel) && sel == f;
        }
        
        //sorts
        if (sortSel is null) {
            var sorts = new Lookup<ItemInstance>.Sort[] {
                new Lookup<ItemInstance>.Sort<int>(x => x.Traits.Count, "Trait Count"),
                new Lookup<ItemInstance>.Sort<string>(x => x.Type.Name, "Alphabetical"),
            };
            sortSel = new Selector<Lookup<ItemInstance>.Sort>(sorts, s => s.Feature);
        }
        nodes.AddRange(new[] {
            new PassthroughNode("\t"),
            sortSel.SelectorDropdown("Sort by:"),
            reverse.MakeSelector("Order:", "Asc", "Desc")
        });
        
        return PopupUIGroup.CreatePopup(source, "Filter/Sort", rs => new UIColumn(rs, nodes), 
            new PopupButtonOpts.LeftRightFlush(null, new UINode[] {
                new UIButton(LocalizedStrings.Controls.confirm, UIButton.ButtonType.Confirm, b => {
                    ClearFilterSort();
                    if (sortSel.FirstSelected.Try(out var sels))
                        AddToken(InventoryView.AddLayer(sort = sels with { Reverse = reverse.Value }));
                    if (filterSel.FirstSelected.Try(out var self) && self.Obj.IsAnySelected)
                        AddToken(InventoryView.AddLayer(filter = self.Realize()));
                    return source.ReturnToGroup;
                }) { EnabledIf = () => !filterSel.FirstSelected.Try(out var sel) || sel.Obj.IsAnySelected }
            }), builder: (_, ve) => ve.SetWidth(1200));
    }

    private void Setup(PJ24GameDef.Executing _exec) {
        exec = _exec;
        InventoryView = new(Data.Inventory, 
            new Lookup<ItemInstance>.Sort<int>(x => x.Type.SortIndex, "") { Hidden = true });
        nextRecipe = new ProposedSynth(exec.Data);
        
        //Completion screen showing result of synthesis
        ScreenSynth3 = new UIScreen(this) { Prefab = screenSynth3VTA };
        _ = new UIRenderExplicit(ScreenSynth3, "ItemDetails")
            .WithView(new ItemDetailsRenderView(new(this, ScreenSynth3)));
        ScreenSynth3.AddFreeformGroup((n, cs, req) => {
            if (req is UICommand.Confirm or UICommand.Back) {
                ((UnityVNState)exec.Md.Container).ClickConfirmOrSkip();
                return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
            }
            return null;
        }).SetFirst();
        
        //Synthesis screen showing synth board / in-progress synthesis
        ScreenSynth2 = new UIScreen(this) { Prefab = screenSynth2VTA };
        //Item selection for synth board component
        var rSynthItemCont = ScreenSynth2.Q("ItemListAndDetails").UseSourceVisible();
        _ = rSynthItemCont.Q("ItemDetails").UseTreeVisible().WithPopupAnim()
            .WithView(new ItemDetailsRenderView(new(this, ScreenSynth2)));
        var rSynthItemInstances = rSynthItemCont.Q("ItemInstanceList").UseTreeVisible().WithPopupAnim();
        var gSynthItemInstances = new UIColumn(rSynthItemInstances.Col(0));
        var gSynthItemSelect = new VGroup(gSynthItemInstances, new UIColumn(rSynthItemInstances.Q("ConfirmButton"), 
            synthItemSelOKNode = new SynthItemSelConfirmView(new(this)).MakeNode())) {
            OnLeave = _ => {
                Synth!.CancelSelection();
                return null;
            },
        }.WithLeaveHideVisibility();
        //Synth board components
        var rSynthComponents = ScreenSynth2.Q("SynthBoard");
        var gSynthComponents = new UIColumn(rSynthComponents.Col(0), 
            4.Range().Select(i => new SynthComponentView(new(this, i, gSynthItemInstances)).MakeNode()))
            .WithChildren(gSynthItemSelect);
        var gSynthConfirm = new UIColumn(rSynthComponents.Q("ConfirmButton"),
            synthFinalizeNode = new UIButton("Synthesize!", UIButton.ButtonType.Confirm, n => {
                    var result = viewingInst = Data.ExecuteSynthesis(Synth!);
                    ISFXService.SFXService.Request(craftSFX);
                    return new UIResult.GoToScreen(ScreenSynth3) {
                        Options = new() { DelayScreenFadeInRatio = 2.5f },
                        OnPostTransition = () => {
                            _ = exec.RunCraftedItemIntuition(result)
                                .ContinueWithSync(() => OperateOnResult(
                                    new UIResult.ReturnToScreenCaller(3), UITransitionOptions.Default));
                            //let the crafted intuition to start first, then trigger data-based dialogues
                            exec.UpdateDataV(_ => { });
                        }
                    };
                }) {
                EnabledIf = () => Synth?.CurrentSelection is null && Synth?.AllComponentsSatisfied() is true,
                Prefab = popupButtonVTA
            });
        ScreenSynth2.SetFirst(new VGroup(gSynthComponents, gSynthConfirm)
            .WithOnEnter(() => exec.RunSynthIngredientSelIntuition(Synth!.Recipe)));
        
        //Selection screen showing recipes and crafting options
        ScreenSynth1 = new UIScreen(this) { Prefab = screenSynth1VTA };
        _ = ScreenSynth1.ContainerRender.WithView(new SynthScreen1RenderView(new(this)));
        var rCraftCount = new UIRenderExplicit(ScreenSynth1, ve => ve.Q("CraftingCount")) 
            .UseSourceVisible().WithPopupAnim();
        var gCraftCount = new UIColumn(rCraftCount, new LROptionNode<int>("", 
            new PropTwoWayBinder<int>(nextRecipe, "Count"),
            () => nextRecipe.MaxCount is var mc and > 0 ?
                mc.Range()
                .Select(i => ((LString)($"{i + 1} / {mc}"), i + 1)).ToArray() :
                new[] { ((LString)"1 / 0", 1) }) {
            Builder = ve => ve.Q("CountSelector"),
            OnConfirm = (n, cs) => {
                var syn = new Viewing.Synth(nextRecipe);
                viewing = syn;
                Listen(syn.Version, _ => {
                    if (syn.CurrentSelection is {} ind)
                        ShowSynthEffect(ind, ScreenSynth2.HTML.Q("ConsolidatedEffectInfo"));
                    ShowTraits(ScreenSynth2.HTML.Q("Traits"), true, Alchemy.CombineTraits(syn.Selected));
                });
                return new UIResult.GoToScreen(ScreenSynth2);
            },
        })
            .OnEnterOrReturnFromChild(_ => exec.RunSynthMenuCraftCtIntuition(nextRecipe.Recipe!, nextRecipe.MaxCount))
            .WithLeaveHideVisibility();
        new UIColumn(ScreenSynth1.Q("RecipeList").UseSourceVisible().WithPopupAnim().Col(0), 
                Item.Items.SelectNotNull(item => item.Recipe.OptFMap(r => 
                    new UINode(new RecipeNodeView(new(this, item))) {
                        OnConfirm = (n, cs) => Data.NumCanCraft(r) > 0 ? 
                            new UIResult.GoToNode(gCraftCount) : new UIResult.StayOnNode(true)
            })))
            .WithLocalLeaveHideVisibility()
            .WithChildren(gCraftCount)
            .SetFirst();
        
        //Requests screen showing list of requests and item selector for submitting requests
        ScreenRequest = new UIScreen(this) { Prefab = screenRequestVTA };
        //Item selector
        var rReqItemCont = ScreenRequest.Q("ItemListAndDetails").UseSourceVisible();
        _ = rReqItemCont.Q("ItemDetails").UseTreeVisible().WithPopupAnim()
            .WithView(new ItemDetailsRenderView(new(this, ScreenRequest)));
        var rReqItemInstances = rReqItemCont.Q("ItemInstanceList").UseTreeVisible().WithPopupAnim();
        var gReqItemInstances = new UIColumn(rReqItemInstances.Col(0));
        var gReqItemSelect = new VGroup(gReqItemInstances, new UIColumn(rReqItemInstances.Q("ConfirmButton"),
            requestItemSelOKNode = new ReqItemSelConfirmView(new(this)).MakeNode())).WithLeaveHideVisibility();
        //Request list
        var rRequestCont = ScreenRequest.Q("RequestListAndDetails").UseSourceVisible().WithPopupAnim();
        var gRequests = new UIColumn(rRequestCont.Q("RequestList").Col(0),
            Data.Phases.SelectMany(p => p.Requests.Select(r => new UINode(r.ShortDescr) {
                VisibleIf = () => Data.Phase == p && r.Visible && !r.Complete,
            }.Bind(new RequestView(new(this, r, gReqItemSelect)))))
            .Append(new UINode("All requests completed!") {
                VisibleIf = () => Data.Phase.PhaseComplete,
            }.Bind(new RequestView(new(this, null, null))))
            .Append(new UINode("Waiting for requests...") {
                VisibleIf = () => Data.Phase.RemainingRequestsUncollected,
            }.Bind(new RequestView(new(this, null, null))))
        ).WithLocalLeaveHideVisibility()
        .WithChildren(gReqItemSelect)
        .SetFirst();
        
        //Inventory screen showing item types and item instances
        ScreenInventory = new UIScreen(this) { Prefab = screenInventoryVTA };
        _ = ScreenInventory.Q("ItemDetails")
            .WithView(new ItemDetailsRenderView(new(this, ScreenInventory)));
        var rItemInstances = ScreenInventory.Q("ItemInstanceList").UseSourceVisible().WithPopupAnim();
        var gInvItemInstances = new UIColumn(rItemInstances.Col(0))
            .WithOnLeave(ClearFilterSort)
            .WithLeaveHideVisibility();
        var rItemTypes = ScreenInventory.Q("ItemTypeList").UseSourceVisible().WithPopupAnim();
        new UIColumn(new UIRenderColumn(rItemTypes, 0), 
            Item.Items.Select(item => new TransferNode(null, gInvItemInstances) {
                VisibleIf = () => Data.NumHeld(item) > 0,
            }.Bind(new ItemTypeNodeView(new(this, item))))
            .Prepend(new TransferNode(null, gInvItemInstances).Bind(new ItemTypeNodeView(new(this, null))))
        ).WithLocalLeaveHideVisibility()
        .WithChildren(gInvItemInstances)
        .SetFirst();
        
        //Request screen, synth screen, and inventory screen all have item instance lists with different view logics.
        // ItemInstance has a "Destroyed" event; when this fires, the nodes will be removed (see the views' OnBuilt).
        var numNodes = 0;
        void ItemAdded() {
            //only keep as many nodes as are required to handle the entire inventory
            if (Data.Inventory.Count <= numNodes) return; 
            gReqItemInstances.AddNodeDynamic(new ItemInstReqSelNodeView(new(this, numNodes)));
            gSynthItemInstances.AddNodeDynamic(new ItemInstSynSelNodeView(new(this, numNodes)));
            gInvItemInstances.AddNodeDynamic(new ItemInstNodeView(new(this, numNodes++)));
        }
        UINode NoOtherHelper(string? filterMsg, string noFilterMsg, bool showFilterCtxMenu = false) {
            var n = new UINode(
                new FlagView(new(() => filter != null, filterMsg ?? "No items match the filters...", noFilterMsg)),
                new NoOtherNodeView()) {
                Prefab = nodeItemVTA,
                OnBuilt = n => {
                    n.HTML.Q("Name").style.maxWidth = 100f.Percent();
                    n.HTML.Q("Count").style.display = false.ToStyle();
                },
            };
            if (showFilterCtxMenu)
                n.Bind(new ContextMenuView(new((n, _) => ItemInstNodeView.Model.CtxMenuOpts(this, n))));
            return n;
        }
        gReqItemInstances.AddNodeDynamic(NoOtherHelper(null, "No items satisfy this request..."));
        gSynthItemInstances.AddNodeDynamic(NoOtherHelper(null,  "No items remain for synthesis..."));
        gInvItemInstances.AddNodeDynamic(NoOtherHelper(null, "You have no items here...", true));
        foreach (var _ in Data.Inventory)
            ItemAdded();
        Listen(Data.InventoryChanged, _ => {
            InventoryView.Recompile();
            ItemAdded();
        });
        
        MainScreen = new UIScreen(this) {
            Prefab = screenMainVTA,
            Builder = (s, ve) => {
                //remove the scroll view
                s.HTML.Q("MainMenuOptions").Q("Content").RemoveFromHierarchy();
            },
        };
        var mainCol = new UIColumn(new UIRenderColumn(MainScreen, 0),
            new TransferNode("Synthesize", ScreenSynth1),
            new TransferNode("Requests", ScreenRequest),
            new TransferNode("Inventory", ScreenInventory),
            new FuncNode("Do Nothing", n => {
                var p = PopupUIGroup.CreatePopup(n, "It's 10 AM.\nGo back to sleep?",
                    r => new UIColumn(r, new UINode(
                                $"Today is {Data.Date.AsMDDate}. If you do nothing for the entire day, " +
                                $"it will become {(Data.Date+1).AsMDDate}.")
                            { Prefab = nodeTextOnlyVTA },
                        Data.Phase.PhaseComplete ? null : 
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
                        new UIButton("Yes", UIButton.ButtonType.Confirm, 
                                n.ReturnToGroup.AsFunc((UIButton _) => exec.UpdateDataV(d => d.Date += 1))) 
                                {Prefab = popupButtonVTA}
                            .WithRootView(r => r.DisableAnimations())
                    }), popupVTA);
                return p;
            }),
            new FuncNode("Pause Menu", IPauseMenu.FindAndQueueOpen)
        )   .WithNodeMod(n => n.WithCSS("large").Prefab = nodeMainMenuOptionVTA)
            .OnEnterOrReturnFromChild(_ => exec.TryRunBaseMenuIntuition());

        PersistentScreen = new UIScreen(this) { Prefab = screenPersistentUIVTA, Persistent = true };
        PersistentScreen.ScreenRender.WithView(new PersistentUIRenderView(new(this)));
        
        Build();

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

    void ShowSynthEffect(int index, VisualElement xml) {
        if (Synth?.Recipe.Components.Try(index) is { } cmp) {
            if (Synth.ComponentReq(index).selected > 0) {
                var effects = Synth.Recipe.DetermineEffects(Synth.Selected[index], index, out var score);
                ShowMeter(xml, null, score, false);
                xml.Q<Label>("Effect").text = effects?.Count > 0 ?
                    string.Join(", ", effects.Select(x => x.Type.Name)) :
                    "No effects";
            } else {
                ShowMeter(xml, null, null, false);
                xml.Q<Label>("Effect").text = Synth.Recipe.HasEffects(index) ? 
                    "[Possible effects]" : "[No effects]";
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

    public void ShowItemImage(Item? typ, VisualElement xml, bool isDirect = false) {
        var bgs = typ is null ? null : ItemConfig.GetValueOrDefault(typ);
        if (!isDirect)
            xml = xml.Q("Image");
        xml.style.backgroundImage = bgs is null ? StyleKeyword.Null : new StyleBackground(bgs);
    }

    public void ShowItemDetailsAt(VisualElement xml) {
        var item = viewingInst;
        var typ = item?.Type ?? (viewing as Viewing.Inventory)?.Item;
        ShowCategories(typ, xml);
        ShowTraits(xml, item is not null || typ is null, 
            item?.Traits ?? new());
        ShowItemImage(typ, xml);
        if (item is { } inst) {
            var xmlEffs = xml.Query(className: "effect").ToList();
            for (int ii = 0; ii < xmlEffs.Count; ++ii) {
                xmlEffs[ii].EnableInClassList("metered", false);
                xmlEffs[ii].Q(className: "underlinetext").EnableInClassList("empty", ii >= inst.Effects.Count);
                if (ii < inst.Effects.Count)
                    xmlEffs[ii].Q<Label>("Text").text = inst.Effects[ii].Type.Name;
            }
        } else ShowEffects(typ, xml);
    }

    public void ShowRequestDetails(Request? req) {
        var xml = ScreenRequest.HTML.Q("RequestDetails");
        xml.Q<Label>("Description").text = req?.Descr ?? "";
        xml.Q("Requirement").Q<Label>().text = req is null ? "" : $"{req.ReqCount}x {req.Required.Describe()}";
        ShowItemImage(req?.Required.Type, xml);
        foreach (var (i, x) in xml.Query(className: "reward").ToList().Enumerate()) {
            if (req?.Reward.TryN(i) is {} rew)
                x.Q<Label>().text = $"{rew.ct}x {rew.item.Describe()}";
            else
                x.Q<Label>().text = "";
        }
    }

    private class RequestView : UIView<RequestView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public Request? Req { get; }
            private UIGroup? Selector { get; }
            
            public Model(PJ24CraftingUXML s, Request? r, UIGroup? selector) {
                S = s;
                Req = r;
                Selector = selector;
            }

            UIResult IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                if (Req is null) return new UIResult.ReturnToScreenCaller();
                S.viewing = new Viewing.Request(Req);
                if (Selector?.MaybeEntryNode is {} en) {
                    return new UIResult.GoToNode(en);
                } else {
                    S.viewing = null;
                    return new UIResult.StayOnNode(true);
                }
            }

            public override long GetViewHash() => (S.Submission?.Req, S.Submission?.Version ?? -1).GetHashCode();
        }
        
        public override VisualTreeAsset Prefab => VM.S.nodeItemVTA;

        public RequestView(Model viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            HTML.Q("Count").style.display = DisplayStyle.None;
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            //this could be implemented with RenderOnlyGroup, but request details are only
            // shown in one place with one data source (this),
            // and there's no other use for a viewingRequest field
            VM.S.ShowRequestDetails(VM.Req);
        }
    }
    
    /// <summary>
    /// View for each component of a synthesis on the synthesis board.
    /// </summary>
    private class SynthComponentView : UIView<SynthComponentView.Model> {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public int Index { get; }
            private UIGroup Selector { get; }
            public RecipeComponent? Cmp => S.Synth?.Recipe.Components.Try(Index);
            
            public Model(PJ24CraftingUXML s, int index, UIGroup selector) {
                S = s;
                Index = index;
                Selector = selector;
            }

            UIResult IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                if (S.Synth is null) throw new Exception("Confirm on synth component with no synth");
                S.Synth.StartSelecting(Index);
                if (Selector.MaybeEntryNode is {} en) {
                    return new UIResult.GoToNode(en);
                } else {
                    S.Synth.CancelSelection();
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                }
            }

            bool IUIViewModel.ShouldBeVisible(UINode node) => Cmp != null;

            public override long GetViewHash() => (S.Synth?.Version ?? -1, S.Synth?.Recipe.Result, S.Synth?.Count ?? -1).GetHashCode();
        }

        public override VisualTreeAsset Prefab => VM.S.nodeSynthComponentVTA;

        public SynthComponentView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            VM.S.ShowItemImage(null, HTML);
            if (VM.Cmp is { } cmp) {
                var syn = VM.S.Synth!;
                var (sel, req) = syn.ComponentReq(VM.Index);
                HTML.Q<Label>("Requirement").text = $"{cmp.Describe()} {sel}/{req}";
                VM.S.ShowSynthEffect(VM.Index, HTML);
                if (sel > 0)
                    VM.S.ShowItemImage(syn.Selected[VM.Index][0].Type, HTML);
            }
            return base.Update(in context);
        }
    }

    public class ObjViewModel<T> : VersionedUIViewModel {
        public PJ24CraftingUXML S { get; }
        public T Val { get; }

        public ObjViewModel(PJ24CraftingUXML s, T val) {
            S = s;
            Val = val;
        }

        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }

    private class ItemInstNodeView : UIView<ItemInstNodeView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public ItemInstance? Val => S.InventoryView[Index];
            public int Index { get; }

            public Model(PJ24CraftingUXML s, int index) {
                S = s;
                Index = index;
            }
            public override long GetViewHash() => Val?.GetHashCode() ?? -1;

            bool IUIViewModel.ShouldBeVisible(UINode node) => S.ItemInstVisible(Val);

            UIResult? IUIViewModel.OnContextMenu(UINode node, ICursorState cs) =>
                PopupUIGroup.CreateContextMenu(node, CtxMenuOpts(S, node));

            public static UINode[] CtxMenuOpts(PJ24CraftingUXML menu, UINode node) => new UINode[] {
                    //by passing node as the popup source, it actually closes the context menu before
                    // opening the popup, which is nice.
                    new FuncNode("Filter/Sort", _ => menu.ShowSortFilterMenu(node))
                };
        }
        public override VisualTreeAsset Prefab => VM.S.nodeItemVTA;
        public ItemInstNodeView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.Val is { } v) {
                HTML.Q<Label>("Name").text = v.ShortDescribe();
                HTML.Q<Label>("Count").style.display = DisplayStyle.None;
            }
            return base.Update(in context);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingInst = VM.Val;
        }
    }

    private class ItemInstSynSelNodeView : UIView<ItemInstSynSelNodeView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public ItemInstance? Val => S.InventoryView[Index];
            public int Index { get; }
            private int ver = 0;

            public Model(PJ24CraftingUXML s, int index) {
                S = s;
                Index = index;
            }

            public override long GetViewHash() => (Val, S.Synth?.Version ?? -1, ver).GetHashCode();

            UIResult IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                if (S.Synth!.ChangeSelectionForCurrent(Val!) is null)
                    return new UIResult.StayOnNode(UIResult.StayOnNodeType.NoOp);
                ++ver; //TODO can you remove this local ver?
                if (S.Synth!.CurrentComponentSatisfied)
                    return new UIResult.GoToNode(S.synthItemSelOKNode);
                return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
            }

            bool IUIViewModel.ShouldBeVisible(UINode node) => S.ItemInstVisible(Val);
        }
        
        public override VisualTreeAsset Prefab => VM.S.nodeItemVTA;

        public ItemInstSynSelNodeView(Model viewModel) : base(viewModel) {}
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.Val is {} v) {
                HTML.Q<Label>("Name").text = v.ShortDescribe();
                var ct = HTML.Q<Label>("Count");
                if (VM.S.Synth?.IsSelectedForCurrent(v) is true) {
                    ct.text = "X";
                    ct.style.display = DisplayStyle.Flex;
                } else
                    ct.style.display = DisplayStyle.None;
            }
            return base.Update(in context);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingInst = VM.Val;
        }
        void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) {
            if (!isEnteringPopup)
                VM.S.viewingInst = null;
        }
    }
    
    private class ItemInstReqSelNodeView : UIView<ItemInstReqSelNodeView.Model>, IUIView {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public ItemInstance? Val => S.InventoryView[Index];
            public int Index { get; }

            public Model(PJ24CraftingUXML s, int index) {
                S = s;
                Index = index;
            }

            public override long GetViewHash() => (Val, S.Submission?.Version ?? -1).GetHashCode();

            UIResult IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                var req = (Viewing.Request)S.viewing!;
                req.ChangeSelection(Val!);
                if (req.Satisfied)
                    return new UIResult.GoToNode(S.requestItemSelOKNode);
                return new UIResult.StayOnNode(UIResult.StayOnNodeType.DidSomething);
            }

            bool IUIViewModel.ShouldBeVisible(UINode node) => S.ItemInstVisible(Val);
        }
        
        public override VisualTreeAsset Prefab => VM.S.nodeItemVTA;

        public ItemInstReqSelNodeView(Model viewModel) : base(viewModel) {}
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.Val is { } v) {
                HTML.Q<Label>("Name").text = v.ShortDescribe();
                var ct = HTML.Q<Label>("Count");
                if (VM.S.Submission?.IsSelected(v) is true) {
                    ct.text = "X";
                    ct.style.display = DisplayStyle.Flex;
                } else
                    ct.style.display = DisplayStyle.None;
            }
            return base.Update(in context);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewingInst = VM.Val;
        }
        void IUIView.OnLeave(UINode node, ICursorState cs, bool animate, bool isEnteringPopup) {
            if (!isEnteringPopup)
                VM.S.viewingInst = null;
        }
    }
    
    /// <summary>
    /// View for nodes which display an item type and the count held in the container.
    /// </summary>
    private class ItemTypeNodeView : UIView<ObjViewModel<Item?>>, IUIView {
        public override VisualTreeAsset Prefab => VM.S.nodeItemVTA;

        public ItemTypeNodeView(ObjViewModel<Item?> viewModel) : base(viewModel) { }

        public override void OnBuilt(UINode node) {
            base.OnBuilt(node);
            node.AddToken(DirtyOn(VM.S.exec.DataChanged));
        }

        protected override BindingResult Update(in BindingContext context) {
            HTML.Q<Label>("Name").text = VM.Val?.Name ?? "All";
            HTML.Q<Label>("Count").text = 
                (VM.Val is {} it ? VM.S.Data.NumHeld(it) : VM.S.Data.Inventory.Count).ToString();
            return base.Update(in context);
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.viewing = new Viewing.Inventory(VM.Val);
            VM.S.viewingInst = null;
        }

        void IUIView.OnRemovedFromNavHierarchy(UINode node) {
            VM.S.viewing = null;
            VM.S.viewingInst = null;
        }
    }

    private class SynthItemSelConfirmView : UIView<SynthItemSelConfirmView.Model> {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public Model(PJ24CraftingUXML s) {
                S = s;
            }

            bool IUIViewModel.ShouldBeEnabled(UINode node) => 
                S.Synth?.CurrentComponentSatisfied is true;

            UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                S.Synth!.CommitSelection();
                if (S.Synth.FirstUnsatisfiedIndex is { } ind)
                    return new UIResult.ReturnToGroupCaller().Then(new UIResult.GoToSibling(ind));
                return new UIResult.GoToNode(S.synthFinalizeNode);
            }

            public override long GetViewHash() => (S.Synth?.Version ?? -1, S.Synth?.CurrentSelection ?? -1).GetHashCode();
        }
        public override VisualTreeAsset? Prefab => VM.S.popupButtonVTA;

        public SynthItemSelConfirmView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.S.Synth is { CurrentSelection: { } sel } syn) {
                var (has, req) = syn.ComponentReq(sel);
                HTML.Q<Label>().text = has >= req ? "OK" : $"{has}/{req}";
            }
            return base.Update(in context);
        }
    }
    private class ReqItemSelConfirmView : UIView<ReqItemSelConfirmView.Model> {
        public class Model : UIViewModel, IUIViewModel {
            public PJ24CraftingUXML S { get; }
            public Model(PJ24CraftingUXML s) {
                S = s;
            }

            bool IUIViewModel.ShouldBeInteractable(UINode node) => 
                ((IUIViewModel)this).ShouldBeEnabled(node);

            bool IUIViewModel.ShouldBeEnabled(UINode node) => 
                S.Submission?.Satisfied is true;

            UIResult? IUIViewModel.OnConfirm(UINode node, ICursorState cs) {
                ISFXService.SFXService.Request(S.reqSubmitSFX);
                S.exec.UpdateDataV(d => d.SubmitRequest(S.Submission!));
                return new UIResult.ReturnToGroupCaller();
            }

            public override long GetViewHash() => (S.Submission?.Version ?? -1, S.Submission?.Req).GetHashCode();
        }

        public override VisualTreeAsset? Prefab => VM.S.popupButtonVTA;
        public ReqItemSelConfirmView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            if (VM.S.Submission is {} sub) {
                var (has, req) = (sub.Selected.Count, sub.Req.ReqCount);
                HTML.Q<Label>().text = (sub.Req.Complete || has >= req) ? "OK" : $"{has}/{req}";
            }
            return base.Update(in context);
        }
    }
    
    /// <summary>
    /// View for each recipe in the recipes list.
    /// </summary>
    private class RecipeNodeView : ItemTypeNodeView, IUIView {
        public RecipeNodeView(ObjViewModel<Item> viewModel) : base(viewModel!) { }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.nextRecipe.Recipe = VM.Val!.Recipe;
        }

        protected override BindingResult Update(in BindingContext context) {
            HTML.EnableInClassList("uncraftable", VM.S.Data.NumCanCraft(VM.Val!.Recipe!) == 0);
            return base.Update(in context);
        }
    }

    private class PersistentUIRenderView : UIView<PersistentUIRenderView.Model>, IUIView {
        public class Model : UIViewModel {
            public PJ24CraftingUXML Menu { get; }
            public Model(PJ24CraftingUXML menu) {
                Menu = menu;
            }
            public override long GetViewHash() => Menu.exec.DataVersion;
        }

        public PersistentUIRenderView(Model viewModel) : base(viewModel) { }

        protected override BindingResult Update(in BindingContext context) {
            var data = VM.Menu.Data;
            var cal = HTML.Q("Calendar");
            cal.Q<Label>("Phase").text = data.Phase.Title;
            cal.Q<Label>("Today").text = $"今日  {data.Date}";
            cal.Q<Label>("Deadline").text = $"〆切  {data.Phase.Deadline}";
            return base.Update(in context);
        }
    }

    private class SynthScreen1RenderView : UIView<SynthScreen1RenderView.Model>, IUIView {
        public class Model : UIViewModel {
            public PJ24CraftingUXML Menu { get; }
            public Model(PJ24CraftingUXML menu) {
                Menu = menu;
            }

            public override long GetViewHash() => 
                Menu.ScreenSynth1.State.Value == UIScreenState.Inactive ? 0 :
                (Menu.exec.DataVersion, Menu.nextRecipe.ViewVersion).GetHashCode();
        }

        public SynthScreen1RenderView(Model viewModel) : base(viewModel) { }
        
        protected override BindingResult Update(in BindingContext context) {
            var s = VM.Menu.ScreenSynth1;
            if (s.State.Value > UIScreenState.Inactive) {
                var nextRecipe = VM.Menu.nextRecipe;
                var ingrs = nextRecipe.Recipe?.Components ?? Array.Empty<RecipeComponent>();
                var xml = s.HTML.Q("RecipeDetails");
                VM.Menu.ShowItemImage(nextRecipe.Item, xml);
                xml.Q<Label>("Time").text = $"{nextRecipe.Recipe?.Time ?? 0:F2}日間";
                var xmlIngrs = xml.Query(className: "ingredient").ToList();
                for (int ii = 0; ii < xmlIngrs.Count; ++ii) {
                    var ut = xmlIngrs[ii].Q(className: "underlinetext");
                    ut.EnableInClassList("empty", ii >= ingrs.Length);
                    ut.EnableInClassList("uncraftable", ii < ingrs.Length && !VM.Menu.Data.Satisfied(ingrs[ii]));
                    if (ii < ingrs.Length) {
                        xmlIngrs[ii].Q<Label>("Text").text = ingrs[ii].Describe();
                        xmlIngrs[ii].Q<Label>("Count").text = ingrs[ii].Count.ToString();
                    }
                }
                VM.Menu.ShowCategories(nextRecipe.Item, xml);
                VM.Menu.ShowEffects(nextRecipe.Item, xml);
                if (nextRecipe.Recipe is { } r) {
                    var cc = s.HTML.Q("CraftingCount");
                    cc.Q("TargetLv").Q<Label>("Right").text = r.Level.ToString();
                    cc.Q("SuccessRate").Q<Label>("Right").text =
                        $"{Math.Round(M.Lerp(12, 69, r.Level, 100, 90)):F0}%";
                    var time = r.DaysTaken(nextRecipe.Count);
                    cc.Q("DaysElapsed").Q<Label>("Right").text = time.ToString();
                    cc.Q("Finish").Q<Label>("Right").text = (VM.Menu.Data.Date + time).ToString();
                }
            }
            return base.Update(in context);
        }
    }
    
    private class ItemDetailsRenderView : UIView<ItemDetailsRenderView.Model> {
        public class Model : UIViewModel {
            public PJ24CraftingUXML Menu { get; }
            public UIScreen S { get; }

            public Model(PJ24CraftingUXML m, UIScreen s) {
                Menu = m;
                S = s;
            }

            public override long GetViewHash() => 
                //normally you don't need to include a check for ScreenIsActive,
                // but we need to ensure that redraws occur when entering screenSynth3,
                // so the cleanest way is to change the hash code when screenSynth3 is made active
                S.State.Value == UIScreenState.Inactive ? 0 :
                (Menu.viewing, Menu.viewingInst).GetHashCode();
        }

        public ItemDetailsRenderView(Model viewModel) : base(viewModel) { }

        protected override BindingResult Update(in BindingContext context) {
            if (VM.S.State.Value >= UIScreenState.InactiveWillGoActive) {
                VM.Menu.ShowItemDetailsAt(HTML);
                if (VM.S == VM.Menu.ScreenSynth3)
                    VM.Menu.ShowItemImage(VM.Menu.viewingInst?.Type, VM.S.HTML.Q("HandImage"), true);
            }
            return base.Update(in context);
        }
    }
    
    [ContextMenu("Make sprites array")]
    public void MakeSpritesArray() {
        sprites = Item.Items.Select(i => new NamedSprite() { name = i.Name }).ToArray();
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
                Count = 1;
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

}
}
