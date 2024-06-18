using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Cancellation;
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
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN;
using MiniProjects.PJ24;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using SuzunoyaUnity;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using static Danmokou.UI.XML.XMLUtils;
using InstanceData = Suzunoya.Data.InstanceData;

namespace MiniProjects.PJ24 {
public record Date(int Month, int Day) {
    public Date Add(int days) {
        var d = Day + days;
        if (Month == 5 && d > 31)
            return new Date(6, 1).Add(d - 32);
        if (Month == 6 && d > 42)
            return new(7, 1);
        return this with { Day = d };
    }

    /// <summary>
    /// Return -1 if this date is before `other`, 0 if they are the same, or 1 if this date is after `other`.
    /// </summary>
    public int Cmp(Date other) {
        if (this == other) 
            return 0;
        if (this.Month < other.Month || this.Month == other.Month && this.Day < other.Day)
            return -1;
        return 1;
    }

    public override string ToString() => $"{Month}月{Day:00}日";
    
    public static Date operator+(Date d, int days) => d.Add(days);
}
public class DataModel {
    public Date Date { get; set; } = new(5, 29);
    public Date Deadline { get; set; } = new(5, 31);
    public string Phase { get; set; } = "5月下旬";

    public Dictionary<Item, List<ItemInstance>> Inventory { get; init; } = new() {
        
    };

    public Event<Unit> DataChanged { get; } = new();

    public int NumHeld(Item item) => Held(item)?.Count ?? 0;
    public List<ItemInstance>? Held(Item item) => Inventory.TryGetValue(item, out var lis) ? lis : null;

    public int NumHeld(RecipeComponent comp) {
        var total = 0;
        for (int ii = 0; ii < Item.Items.Length; ++ii) {
            var item = Item.Items[ii];
            var match = comp.MatchesType(item);
            if (match is true) {
                total += NumHeld(item);
            } else if (match is null) {
                if (Held(item) is {} held)
                    for (int jj = 0; jj < held.Count; ++jj)
                        if (comp.Matches(held[jj]))
                            ++total;
            }
        }
        return total;
    }

    public bool CanCraft(Recipe recipe) {
        foreach (var cmp in recipe.Components)
            if (NumHeld(cmp) < cmp.Count)
                return false;
        return true;
    }
}

public record WIPSynth(Item Item, int Count) {
}

public class LocalXMLPJ24Fragments : UIController {
    public DataModel Data = new();
    public readonly Evented<WIPSynth?> CurrSynth = new(null);

    public VisualTreeAsset screenSynth1 = null!;
    public VisualTreeAsset nodeRecipe = null!;

    protected override bool CaptureFallthroughInteraction => false;

    public override void FirstFrame() {
        var s = MainScreen = new UIScreen(this, null, UIScreen.Display.Unlined) {
            Prefab = screenSynth1,
            Builder = (s, ve) => s.HTML.pickingMode = PickingMode.Ignore
        };
        var rRecipeList = new UIRenderExplicit(s, ve => ve.Q("RecipeList"));
        var gRecipeList = new UIColumn(new UIRenderColumn(rRecipeList, 0), Item.Items.Select(item => {
            if (item.Recipe is null)
                return null;
            return new UINode(new RecipeNodeView(new(this, Data, item)))
                { Prefab = nodeRecipe };
        }));
        base.FirstFrame();
        List<VisualElement> calendars = new();
        calendars.Add(s.HTML.Q("Calendar"));

        Data.Inventory[Item.FlaxFiber.S] = new() { new(Item.FlaxFiber.S), new(Item.FlaxFiber.S) };

        void UpdateHTML(Unit _) {
            foreach (var c in calendars) {
                c.Q<Label>("Phase").text = Data.Phase;
                c.Q<Label>("Today").text = $"今日  {Data.Date}";
                c.Q<Label>("Deadline").text = $"〆切  {Data.Deadline}";
            }
        }

        Listen(Data.DataChanged, UpdateHTML);
        UpdateHTML(default);

        void ShowMeter(VisualElement container, int? low, int? high) {
            container.EnableInClassList("metered", true);
            var selMeter = container.Q("SelectedMeter");
            selMeter.style.left = (low ?? 0f).Percent();
            selMeter.style.right = (100 - (high ?? 100f)).Percent();
        }

        Listen(CurrSynth, syn => {
            var ingrs = syn?.Item.Recipe?.Components ?? Array.Empty<RecipeComponent>();
            var xml = s.HTML.Q("RecipeDetails");
            xml.Q<Label>("Time").text = $"{syn?.Item.Recipe?.Time ?? 0:F2}日間";
            var xmlIngrs = xml.Query(className: "ingredient").ToList();
            for (int ii = 0; ii < xmlIngrs.Count; ++ii) {
                xmlIngrs[ii].Q(className:"underlinetext").EnableInClassList("empty", ii >= ingrs.Length);
                if (ii < ingrs.Length) {
                    xmlIngrs[ii].Q<Label>("Text").text = ingrs[ii].Describe();
                    xmlIngrs[ii].Q<Label>("Count").text = ingrs[ii].Count.ToString();
                }
            }
            var cats = syn?.Item.Categories ?? Array.Empty<(Category, int)>();
            var xmlCats = xml.Query(className: "category").ToList();
            for (int ii = 0; ii < xmlCats.Count; ++ii) {
                xmlCats[ii].Q(className:"underlinetext").EnableInClassList("empty", ii >= cats.Length);
                if (ii < cats.Length) {
                    xmlCats[ii].Q<Label>("Text").text = cats[ii].category.Print();
                    ShowMeter(xmlCats[ii], 0, cats[ii].score);
                }
            }
            var xmlEffs = xml.Query(className: "effect").ToList();
            var ei = 0;
            var allEffs = syn?.Item.Recipe?.Effects ?? Array.Empty<EffectRequirement[]?>();
            for (int ii = 0; ii < allEffs.Length && ei < xmlEffs.Count; ++ii) {
                if (allEffs[ii] is not {} ingrEffs) continue;
                xmlEffs[ei].Q(className:"underlinetext").EnableInClassList("empty", false);
                xmlEffs[ei].EnableInClassList("metered", false);
                xmlEffs[ei++].Q<Label>("Text").text = ingrs[ii].Describe();
                foreach (var eff in ingrEffs) {
                    xmlEffs[ei].Q(className:"underlinetext").EnableInClassList("empty", false);
                    ShowMeter(xmlEffs[ei], eff.MinScore, eff.MaxScore);
                    xmlEffs[ei++].Q<Label>("Text").text = "  " + eff.Result.Name;
                    if (ei >= xmlEffs.Count) break;
                }
            }
            for (; ei < xmlEffs.Count; ++ei)
                xmlEffs[ei].Q(className:"underlinetext").EnableInClassList("empty", true);
        });
        
        /*
        var r1 = new UIRenderConstructed(s, new(sc => {
            var ve = sc.AddScrollColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 1000, 1080 - 550).SetWidthHeight(new(900, 900));
            ve.style.backgroundColor = new Color(0.3f, 0.223f, 0.3f, 0.8f);
            return ve;
        })).WithTooltipAnim();
        var r2 = new UIRenderConstructed(s, new(sc => {
            var c2 = s.Container.AddColumn().ConfigureAbsolute()
                .WithAbsolutePosition(3840 - 2000, 1080 - 550).SetWidthHeight(new(900, 900));
            c2.style.backgroundColor = new Color(0.6f, 0.223f, 0.3f, 0.8f);
            return c2;
        })).WithPopupAnim();

        Task? EnterColumn2(UIGroup g) {
            r1.OverrideVisibility(false);
            return null;
        }
        Task? LeaveColumn2(UIGroup g) {
            r1.OverrideVisibility(true);
            return null;
        }

        var nRecipes = Item.Craftables.Length;
        var nodes = new UINode[nRecipes];
        var group2 = new UIColumn(new UIRenderColumn(r2, 0),
            new UINode($"hello 0"), new UINode($"hello 1"));
        group2.Visibility = new GroupVisibility.UpdateOnLeaveHide(group2);
        for (int ii = 0; ii < nRecipes; ++ii) {
            nodes[ii] = new TransferNode($"{Item.Craftables[ii].Name}", group2);
        }

        var g1 = new UIColumn(new UIRenderColumn(r1, 0), nodes) {
            OnGoToChild = EnterColumn2,
            OnReturnFromChild = LeaveColumn2
        };
        group2.Parent = g1;
        Menu.FreeformGroup.AddGroupDynamic(g1);*/

        var ct = new Cancellable();
        var vn = new DMKVNState(ct, "script-name-here", new InstanceData(new GlobalData()));
        var exec = ServiceLocator.Find<IVNWrapper>().TrackVN(vn);
        //ServiceLocator.Find<IVNBacklog>().TryRegister(exec);
        var bctx = new BoundedContext<Unit>(vn, "", async () => {
            using var _links = LinkCallback.RegisterClicker(("link0", "example text"), ("link1", "this is a tooltip"));
            using var md = vn.Add(new ADVDialogueBox());
            md.LocalLocation.Value = new Vector3(2.5f, 0)._();
            md.MinimalState.AddConst(true);
            using var s = vn.Add(new Reimu());
            s.Alpha = 0;
            await vn.Sequential(s.Say(
                    "Kaguya asked me to craft an <color=red><link=\"link0\">Oil Lamp</link></color>." +
                    "\nMaybe I should work on that first.")
            );
            vn.Run(WaitingUtils.Spin(WaitingUtils.GetCompletionAwaiter(out var t), stopdialogue = new()));
            await t;
            return default;
        });
        _ = bctx.Execute();
    }

    private Cancellable? stopdialogue = null;

    [ContextMenu("cancel dialogue")]
    public void CnacelDialogue() => stopdialogue?.Cancel();

    [ContextMenu("bump date")]
    public void BumpDate() {
        Data.Date += 1;
        Data.DataChanged.OnNext(default);
    }
    [ContextMenu("add nuts")]
    public void AddNuts() {
        Data.Inventory[Item.BagOfNuts.S] = new() { new(Item.BagOfNuts.S) };
        Data.DataChanged.OnNext(default);
    }

    private class RecipeNodeView : UIView<RecipeNodeView.Model>, IUIView {
        public class Model : UIViewModel {
            public LocalXMLPJ24Fragments S { get; }
            public DataModel Source { get; }
            public Item Item { get; }

            public Model(LocalXMLPJ24Fragments s, DataModel source, Item item) {
                S = s;
                Source = source;
                Item = item;
            }

            public override long GetViewHash() => (Item, Source.NumHeld(Item)).GetHashCode();
        }

        public RecipeNodeView(Model viewModel) : base(viewModel) {
            //don't use DirtyOn since we still want hash code lgoic
            Tokens.Add(VM.Source.DataChanged.Subscribe(_ => MarkDirty()));
        }

        void IUIView.OnEnter(UINode node, ICursorState cs, bool animate) {
            VM.S.CurrSynth.Value = new(VM.Item, 1);
        }

        protected override BindingResult Update(in BindingContext context) {
            Node.HTML.Q<Label>("Name").text = VM.Item.Name;
            Node.HTML.Q<Label>("Count").text = VM.Source.NumHeld(VM.Item).ToString();
            Node.HTML.EnableInClassList("uncraftable", !VM.Source.CanCraft(VM.Item.Recipe!));
            return base.Update(in context);
        }
    }
    
}
}
