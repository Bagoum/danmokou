using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Assertions;
using BagoumLib.Culture;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Mathematics;
using BagoumLib.Tasks;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Services;
using Danmokou.UI;
using Danmokou.UI.XML;
using Danmokou.VN;
using MiniProjects.PJ24;
using MiniProjects.VN;
using Newtonsoft.Json;
using Suzunoya;
using Suzunoya.ADV;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;
using static SuzunoyaUnity.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace MiniProjects.PJ24 {

[CreateAssetMenu(menuName = "Data/ADV/PJ24 Game")]
public class PJ24GameDef : ADVGameDef {
    public class Executing : DMKExecutingADV<Executing.PJ24IdealizedState, PJ24ADVData> {
        private readonly PJ24GameDef gdef;

        public Executing(PJ24GameDef gdef, ADVInstance inst) : base(inst) {
            this.gdef = gdef;
            tokens.Add(ServiceLocator.Register<Executing>(this));
        }

        /// <inheritdoc/>
        public override void ADVDataFinalized() {
        }

        public const string House = "myhouse";
        private SZYUCharacter M => VN.Find<Marisa>();

        public BoundedContext<Unit> CraftedItem(ItemInstance item) => new(VN, "", async () => {
            md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
            md.MinimalState.AddConst(true);
            await M.SayC($"Poggers! I crafted a {item.Type.Name}!");
            return default;
        });
        
        protected override MapStateManager<PJ24IdealizedState, PJ24ADVData> ConfigureMapStates() {
            var m = Manager;
            var ms = new MapStateManager<PJ24IdealizedState, PJ24ADVData>(this, () => new(this));
            
            var s0 = Context("", async () => {
                using var _links = LinkCallback.RegisterClicker(("link0", "example text"), ("link1", "this is a tooltip"));
                md.LocalLocation.Value = new Vector3(2.5f, 0, 0);
                md.MinimalState.AddConst(true);
                await M.Say(
                    "Kaguya asked me to craft an <color=red><link=\"link0\">Oil Lamp</link></color>." +
                    "\nMaybe I should work on that first.");
                await vn.Wait(() => false);
                var x = 45;
            }, true);
            
            ms.ConfigureMap(House, (i, d) => {
                //i.Assert(new EntityAssertion<ShrineRoomBG>(VN));
                i.Assert(new BGMAssertion(VN, "s01-1"));
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = V3(-1.5f, 0),
                    Tint = FColor.Clear
                });
                i.Assert(new RunOnEntryAssertion(() => Manager.TryOrDelayExecuteVN(s0, true)) 
                    { Priority = (int.MaxValue, 0)});
            });
            return ms;
        }
        
        public record PJ24IdealizedState(Executing e) : ADVIdealizedState(e) {
            protected override Task FadeIn() {
                return e.rgb.DoTransition(new RenderGroupTransition.Fade(e.rg, 0.7f)).Task;
            }
            protected override Task FadeOut() {
                return e.rg.DoTransition(new RenderGroupTransition.Fade(e.rgb, 0.7f)).Task;
            }
        }
    }

    public class Request {
        public string Description { get; set; } = "Example request description";
        public bool Visible { get; set; } = false;
        public bool Complete { get; set; } = false;
    }

    public abstract class GamePhase {
        public abstract string Title { get; }
        public abstract Date Deadline { get; }
        public abstract Request[] Requests { get; }

        public bool IsComplete {
            get {
                var req = Requests;
                for (int ii = 0; ii < req.Length; ++ii)
                    if (!req[ii].Complete)
                        return false;
                return true;
            }
        }

        public class May : GamePhase {
            public override string Title => "5月下旬";
            public override Date Deadline => new(5, 31);
            public override Request[] Requests { get; } = new[] {
                new Request() {} //TODO
            };
        }
        public class June1 : GamePhase {
            public override string Title => "6月上旬";
            public override Date Deadline => new(6, 14);
            public override Request[] Requests { get; } = new[] {
                new Request() {} //TODO
            };
        }
        public class June2 : GamePhase {
            public override string Title => "6月中旬";
            public override Date Deadline => new(6, 30);
            public override Request[] Requests { get; } = new[] {
                new Request() {} //TODO
            };
        }
        public class June3 : GamePhase {
            public override string Title => "6月下旬";
            public override Date Deadline => new(6, 42);
            public override Request[] Requests { get; } = new[] {
                new Request() {} //TODO
            };
        }
    }

    [Serializable]
    public record PJ24ADVData: ADVData {
        public Date Date { get; set; } = new(5, 29);
        public GamePhase Phase { get; set; } = new GamePhase.May();
        public Dictionary<Item, List<ItemInstance>> Inventory { get; init; } = new() {
            {Item.FlaxFiber.S, new() { new(Item.FlaxFiber.S, null, new(){new(Trait.Glowing1.S), new(Trait.Sour.S)}), 
                new(Item.FlaxFiber.S, new() {new(Effect.Antifragile.S)}, new(){new(Trait.Glowing2.S)}) }},
            {Item.Water.S, new() { new(Item.Water.S) }},
            {Item.TritricolorBanner.S, new() { new(Item.TritricolorBanner.S, new(){new(Effect.TraditionallyWoven.S)})}},
            {Item.LinenCloth.S, new() {
                new(Item.LinenCloth.S, new(){new(Effect.Fragile.S)}),
                new(Item.LinenCloth.S),new(Item.LinenCloth.S),new(Item.LinenCloth.S),
            }},
            {Item.DragonscaleCloth.S, new() {
                new(Item.DragonscaleCloth.S, null, new(){new(Trait.Sour.S)}),
                new(Item.DragonscaleCloth.S),new(Item.DragonscaleCloth.S),new(Item.DragonscaleCloth.S),
            }},
            {Item.Rope.S, new() {
                new(Item.Rope.S, null, new(){new(Trait.Sweet.S)}),
                new(Item.Rope.S),
            }}
        };
        [JsonIgnore] public Event<ItemInstance> ItemAdded { get; } = new();

        public PJ24ADVData(InstanceData VNData) : base(VNData) {
        }

        public ItemInstance ExecuteSynthesis(CurrentSynth synth) {
            Date += synth.Recipe.DaysTaken(synth.Count);
            foreach (var item in synth.Selected.SelectMany(x => x))
                RemoveItem(item);
            var result = synth.Recipe.Synthesize(synth.Selected);
            AddItem(result);
            return result;
        }

        public void AddItem(ItemInstance item) {
            Inventory.AddToList(item.Type, item);
            ItemAdded.OnNext(item);
        }

        public void RemoveItem(ItemInstance item) {
            Inventory[item.Type].Remove(item);
            item.Destroy();
        }
        
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

        public bool Satisfied(RecipeComponent comp) => NumHeld(comp) >= comp.Count;

        public int NumCanCraft(Recipe recipe) {
            var ct = 99;
            foreach (var cmp in recipe.Components)
                ct = Math.Min(ct, NumHeld(cmp) / cmp.Count);
            return ct;
        }
    }

    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.ADVData.CurrentMap == "")
            throw new Exception("PJ24 was loaded with no current map.");
        Logs.Log("Starting PJ24 execution...");
        return new Executing(this, inst);
    }

    public override ADVData NewGameData() => new PJ24ADVData(new(SaveData.r.GlobalVNData)) {
        CurrentMap = Executing.House
    };
}
}