using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Events;
using BagoumLib.Tasks;
using Danmokou.ADV;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.VN;
using MiniProjects.VN;
using Newtonsoft.Json;
using Suzunoya;
using Suzunoya.Assertions;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using UnityEngine.XR;
using static SuzunoyaUnity.Helpers;
using Vector3 = System.Numerics.Vector3;
using static MiniProjects.VN.ScriptLocalization.PurpleHeartParadox;

namespace MiniProjects.VN.PurpleHeart {

[CreateAssetMenu(menuName = "Data/ADV/Purple Heart Game")]
public class PurpleHeartGameDef : ADVGameDef {
    private class Executing : ExecutingADVGame<Executing.PHIdealizedState, PHADVData> {
        //--- Entities
        private readonly ADVDialogueBox md;
        private readonly Narrator ec;
        private readonly UnityRenderGroup rg;
        private readonly UnityRenderGroup rgb;
        //--- Lerpers
        private readonly PushLerper<Vector3> dialogueShowOffset = new((p, n) => (n.Y > p.Y) ? 0.3f : 0.5f);
        private readonly PushLerper<FColor> dialogueShowAlpha = new((p, n) => (n.a > p.a) ? 0.3f : 0.5f);

        private void HideMD() {
            dialogueShowOffset.Push(new(0f, -0.5f, 0));
            dialogueShowAlpha.Push(new FColor(1, 1, 1, 0));
        }
        private void ShowMD() {
            dialogueShowOffset.Push(new(0,0,0));
            dialogueShowAlpha.Push(new FColor(1, 1, 1, 1));
        }
        
        public Executing(ADVInstance inst) : base(inst) {
            //probably don't need to add these to tokens as they'll be destroyed with VN destruction.
            md = VN.Add(new ADVDialogueBox());
            md.ComputedLocation.AddDisturbance(dialogueShowOffset);
            md.ComputedTint.AddDisturbance(dialogueShowAlpha);
            HideMD();
            
            VN.ContextStarted.Subscribe(c => {
                if (VN.Contexts.Count == 1) {
                    md.Clear();
                    ShowMD();
                }
                //_ = md.MoveTo(Vector3.Zero, 0.5f, M.EOutSine).Task;
            });
            VN.ContextFinished.Subscribe(c => {
                if (VN.Contexts.Count == 0 && inst.eVN.Active) {
                    HideMD();
                }
            });
            ec = VN.Add(new Narrator());
            rg = (UnityRenderGroup)VN.DefaultRenderGroup;
            rgb = new UnityRenderGroup(VN, "black", 1, true);
            rg.Visible.Value = false;
            tokens.Add(MapWillUpdate.Subscribe(_ => {
                Logs.Log($"Setting delayed state from {Data.DelayedState} to {Data.State}");
                Data.DelayedState = Data.State;
            }));
        }

        public override void RegularUpdate() {
            dialogueShowOffset.Update(ETime.FRAME_TIME);
            dialogueShowAlpha.Update(ETime.FRAME_TIME);
        }
        
        record MapData(string key, Func<PHADVData, string> desc, float mapLinkOffset) { }

        private MapData[] maps = {
            new("YukariHouse", _ => "my house", 2),
            new("MoriyaShrine", d => d.State == State.S7_BackToMoriya ? "the Forest of Magic" : "the Moriya Shrine", 1),
            new("Ravine", _ => "Genbu Ravine", 0),
            new("Lake", _ => "the Misty Lake", -1),
            new("HakureiShrine", _ => "Reimu's place", -2)
        };

        private Yukari Yukari => VN.Find<Yukari>();
        
        /// <summary>
        /// Core function that handles the entire game's logical configuration.
        /// </summary>
        /// <returns></returns>
        protected override MapStateManager<PHIdealizedState, PHADVData> ConfigureMapStates() {
            var m = Manager;
            var ms = new MapStateManager<PHIdealizedState, PHADVData>(() => new(this));
            //Use this proxy function to register BCTXs so they can be inspected and run on load.
            // Top-level contexts should always be <Unit>.
            BoundedContext<Unit> Context(string id, Func<Task> innerTask) {
                var ctx = new BoundedContext<Unit>(VN, id, async () => {
                    await innerTask();
                    return default;
                });
                if (bctxes.ContainsKey(ctx.ID))
                    throw new Exception($"Multiple BCTXes are defined for key {ctx.ID}");
                bctxes[ctx.ID] = ctx;
                return ctx;
            }
            //These must be separated out of ConfigureMap since they should not be recreated
            // when the idealized state is recreated.
            var s0main = Context("s0main", async () => {
                var y = VN.Find<Yukari>();
                await VN.Sequential(
                    y.ESayC("happy", l0),
                    y.ESayC("", l1),
                    y.EmoteSayC("smug", l2),
                    y.ESayC("", l3)
                );
                UpdateDataV(p => p.State = State.S1_ToReimu);
            });
            var s0nitori = Context("s0nitori", async () => {
                var n = VN.Find<Nitori>();
                await VN.Sequential(
                    n.ESayC("happy", l4),
                    Yukari.SayC(l5),
                    n.ESayC("", l6));
            });
            var s0cirno = Context("s0cirno", async () => {
                var c = VN.Find<Cirno>();
                await VN.Sequential(
                    c.ESayC("happy", l7),
                    Yukari.SayC(l8),
                    c.ESayC("happy", l9));
            });
            var s0kanako = Context("s0kanako", async () => {
                await VN.Sequential(
                    VN.Find<Kanako>().ESayC("happy", l11),
                    Yukari.SayC(l12)
                );
            });
            var s1main = Context("s1main", async () => {
                var r = VN.Find<Reimu>();
                var y = VN.Find<Yukari>();
                await VN.Sequential(
                    y.ESayC("happy", l386),
                    r.SayC(l13),
                    y.ESayC("", l14),
                    r.SayC(l15),
                    y.ESayC("worry", l16),
                    r.ESayC("happy", l17),
                    y.SayC(l18),
                    r.ESayC("", l19),
                    y.SayC(l20),
                    y.ESayC("angry", l21),
                    y.SayC(l22),
                    y.ESayC("happy", l23),
                    r.SayC(l24),
                    y.ESayC("angry", l25)
                );
                UpdateDataV(p => p.State = State.S2_ToHouse);
            });
            var s2main = Context("s2main", async () => {
                var k = VN.Find<Kasen>();
                var y = VN.Find<Yukari>();
                await VN.Sequential(
                    k.SayC(l26),
                    y.ESayC("worry", l27),
                    k.ESayC("worry", l28),
                    y.SayC(l29),
                    k.ESayC("surprise", l30),
                    y.SayC(l31),
                    k.ESayC("worry", l32),
                    y.ESayC("surprise", l33),
                    k.SayC(l34),
                    y.SayC(l35),
                    k.SayC(l36),
                    k.ESayC("", l37),
                    y.ESayC("worry", l38),
                    k.SayC(l39),
                    y.ESayC("surprise", l40),
                    k.ESayC("worry", l41),
                    y.ESayC("worry", l42),
                    k.ESayC("", l43),
                    y.ESayC("", l44),
                    k.SayC(l45),
                    k.SayC(l46),
                    y.SayC(l47),
                    k.ESayC("worry", l48),
                    k.ESayC("happy", l49),
                    y.ESayC("happy", l50)
                );
                UpdateDataV(p => p.State = State.S2_1_DoremyEntry, new() {
                    SimultaneousActualization = true
                });
                Doremy d = null!;
                await VN.Wait(() => (d = VN.FindEntity<Doremy>()) != null);
                await VN.SFX("vn-yukari-power");
                await d.ESayC("happy", l51);
                await MapTransitionTask;
                await VN.Sequential(
                    d.ESayC("surprise", l52),
                    y.ESayC("", l53),
                    d.ESayC("happy", l54),
                    y.ESayC("worry", l55),
                    d.ESayC("", l56),
                    y.ESayC("happy", l57),
                    d.ESayC("smug", l58),
                    k.ESayC("worry", l59),
                    d.ESayC("", l60),
                    d.SayC(l60_1),
                    y.ESayC("cry", l61),
                    d.SayC(l62),
                    d.SayC(l63),
                    d.ESayC("worry", l64),
                    d.ESayC("happy", l65),
                    d.ESayC("", l66),
                    y.ESayC("", l67),
                    d.SayC(l68),
                    d.SayC(l68_1),
                    d.SayC(l69),
                    d.SayC(l69_1),
                    y.ESayC("surprise", l70),
                    k.ESayC("angry", l71),
                    y.SayC(l72),
                    d.ESayC("surprise", l73),
                    d.ESayC("worry", l74),
                    d.ESayC("happy", l75),
                    k.SayC(l76),
                    y.ESayC("happy", l77),
                    k.ESayC("", l78),
                    k.SayC(l79),
                    y.ESayC("", l80)
                );
                d.Emote.RevokeOverride();
                UpdateDataV(p => p.State = State.S3_ToMoriya);
            });

            var s3kasen = Context("s3kasen", async () => {
                await VN.Find<Kasen>().SayC(l83);
            });
            var s3doremy = Context("s3doremy", async () => {
                await VN.Find<Doremy>().SayC(l85);
            });
            var s3reimu = Context("s3reimu", async () => {
                var r = VN.Find<Reimu>();
                await VN.Sequential(
                    r.SayC(l87),
                    VN.Find<Sanae>().SayC(l88),
                    r.ESayC("worry", l89),
                    r.ESayC("", l90)
                );
            });
            var s3main = Context("s3main", async () => {
                var r = VN.Find<Reimu>();
                var s = VN.Find<Sanae>();
                var y = VN.Find<Yukari>();
                await VN.Sequential(
                    s.ESayC("", l91_1),
                    r.ESayC("angry", l92),
                    s.ESayC("worry", l93),
                    y.ESayC("worry", l94),
                    s.ESayC("happy", l95),
                    s.ESayC("", l96),
                    s.SayC(l97),
                    r.ESayC("angry", l98),
                    r.SayC(l99),
                    s.ESayC("worry", l100),
                    y.ESayC("worry", l101),
                    s.ESayC("surprise", l102),
                    r.ESayC("happy", l103),
                    s.ESayC("worry", l104),
                    y.ESayC("", l105),
                    r.ESayC("worry", l106),
                    s.ESayC("", l107),
                    y.ESayC("worry", l108),
                    s.ESayC("", l109),
                    y.ESayC("happy", l110)
                );
                UpdateDataV(p => p.State = State.S4_ToMistyLake, new() {
                    SimultaneousActualization = true
                });
            });
            var s4sanae = Context("s4sanae", async () => {
                await VN.Find<Sanae>().ESayC("", l112);
            });
            var s4kasen = Context("s4kasen", async () => {
                await VN.Sequential(
                    VN.Find<Yukari>().ESayC("worry", l115),
                    VN.Find<Kasen>().ESayC("", l116)
                );
            });
            var s4main = Context("s4main", async () => {
                var c = VN.Find<Cirno>();
                var y = VN.Find<Yukari>();
                await VN.Sequential(
                    c.ESayC("", l118),
                    y.ESayC("", l119),
                    c.ESayC("cry", l120),
                    y.ESayC("happy", l121),
                    c.ESayC("happy", l122),
                    y.ESayC("", l123),
                    c.ESayC("worry", l124),
                    y.SayC(l125),
                    y.SayC(l125_1),
                    ec.SayC(l126),
                    c.ESayC("happy", l127),
                    y.ESayC("worry", l128),
                    c.ESayC("", l129),
                    y.ESayC("surprise", l130),
                    c.ESayC("worry", l131),
                    y.ESayC("worry", l132),
                    c.ESayC("cry", l133),
                    c.SayC(l134),
                    c.SayC(l135),
                    y.ESayC("happy", l136),
                    y.SayC(l137),
                    c.ESayC("angry", l138),
                    c.SayC(l139),
                    y.ESayC("worry", l140),
                    c.SayC(l141),
                    y.ESayC("surprise", l142),
                    c.ESayC("cry", l143)
                );
                c.Emote.RevokeOverride();
                UpdateDataV(p => p.State = State.S5_ToRavine, new() {
                    SimultaneousActualization = true
                });
            });
            var s5cirno = Context("s5cirno", async () => {
                await VN.Find<Cirno>().ESayC("", l145);
            });
            var s5kasen = Context("s5kasen", async () => {
                var k = VN.Find<Kasen>();
                await VN.Sequential(
                    VN.Find<Yukari>().ESayC("worry", l148),
                    k.ESayC("worry", l149),
                    k.ESayC("", l150)
                );
            });
            var s5doremy = Context("s5doremy", () => VN.Find<Doremy>().ESayC("", l152).Task);
            var s5main = Context("s5main", async () => {
                var i = VN.Find<Iku>();
                var y = Yukari;
                await VN.Sequential(
                    y.ESayC("surprise", l154),
                    i.ESayC("", l155),
                    y.ESayC("worry", l156),
                    i.ESayC("worry", l157),
                    y.ESayC("happy", l158),
                    i.SayC(l159),
                    i.ESayC("smug", l160),
                    i.SayC(l161),
                    y.ESayC("worry", l162),
                    i.ESayC("", l163),
                    y.ESayC("surprise", l164),
                    i.SayC(l165),
                    i.ESayC("angry", l166),
                    y.ESayC("worry", l167),
                    i.ESayC("worry", l168),
                    y.SayC(l169),
                    i.SayC(l170),
                    y.ESayC("surprise", l171),
                    i.ESayC("happy", l172)
                );
                UpdateDataV(p => p.State = State.S6_BackToMistyLake, new() {
                    SimultaneousActualization = true
                });
            });
            var s6iku = Context("s6iku", async () => {
                var i = VN.Find<Iku>();
                await VN.Sequential(
                    i.ESayC("", l174),
                    i.ESayC("worry", l175)
                );
            });
            var s6kasen = Context("s6kasen",
                () => VN.Sequential(Yukari.ESayC("happy", l178), VN.Find<Kasen>().ESayC("", l179)).Task);
            var s6main = Context("s6main", async () => {
                var n = VN.Find<Nitori>();
                var y = Yukari;
                await VN.Sequential(
                    n.ESayC("", l181),
                    y.ESayC("worry", l182),
                    n.ESayC("happy", l183),
                    y.ESayC("happy", l184),
                    n.ESayC("", l185),
                    y.ESayC("", l186),
                    n.ESayC("worry", l187),
                    y.SayC(l188),
                    n.ESayC("happy", l189),
                    y.ESayC("worry", l190)
                );
                UpdateDataV(p => p.State = State.S6_1_SeigaTransform, new MapStateTransitionSettings<PHIdealizedState>() {
                    SimultaneousActualization = true
                });
                Seiga s = null!;
                await VN.Wait(() => (s = VN.FindEntity<Seiga>()) != null);
                await s.ESayC("happy", l192);
                await MapTransitionTask;
                await VN.Sequential(
                    y.ESayC("happy", l193),
                    y.ESayC("surprise", l194),
                    s.ESayC("smug", l195),
                    y.ESayC("happy", l196),
                    y.ESayC("worry", l197),
                    s.ESayC("worry", l198),
                    y.SayC(l199),
                    s.SayC(l200),
                    y.ESayC("happy", l201),
                    s.ESayC("surprise", l202),
                    y.ESayC("angry", l203),
                    s.ESayC("happy", l204),
                    s.SayC(l205),
                    y.SayC(l206),
                    s.ESayC("worry", l207),
                    y.ESayC("worry", l208),
                    s.ESayC("angry", l209),
                    s.ESayC("worry", l210),
                    y.ESayC("happy", l211),
                    s.ESayC("", l212),
                    y.ESayC("worry", l213),
                    s.ESayC("", l214),
                    y.ESayC("happy", l215),
                    s.ESayC("smug", l216),
                    y.ESayC("surprise", l217),
                    y.ESayC("happy", l218),
                    s.ESayC("angry", l219),
                    y.SayC(l220),
                    VN.SFX("vn-yukari-power"),
                    VN.Wait(1f),
                    VN.SFX("vn-yukari-power"),
                    VN.Wait(0.5f),
                    y.ESayC("", l221),
                    s.ESayC("smug", l222),
                    y.ESayC("happy", l223),
                    s.ESayC("angry", l224),
                    y.SayC(l225),
                    VN.SFX("vn-yukari-power"),
                    VN.Wait(1f),
                    VN.SFX("vn-yukari-power"),
                    VN.Wait(0.5f),
                    y.ESayC("", l226),
                    s.ESayC("smug", l227),
                    y.ESayC("happy", l228),
                    s.ESayC("angry", l229),
                    s.ESayC("worry", l230),
                    y.ESayC("worry", l231),
                    y.ESayC("", l232),
                    s.SayC(l233),
                    y.ESayC("happy", l234)
                );
                UpdateDataV(p => p.State = State.S7_BackToMoriya);
            });
            var s7seiga = Context("s7seiga", async () => {
                var s = VN.Find<Seiga>();
                await VN.Sequential(
                    s.ESayC("", l236),
                    s.SayC(l237),
                    s.ESayC("happy", l238)
                );
            });
            var s7reimu = Context("s7reimu", async () => {
                var r = VN.Find<Reimu>();
                await VN.Sequential(
                    r.ESayC("", l402),
                    VN.Find<Marisa>().ESayC("worry", l403),
                    r.ESayC("worry", l404)
                );
            });

            var s7kasen = Context("s7kasen", async () => {
                await VN.Sequential(
                    VN.Find<Kasen>().ESayC("", l241),
                    Yukari.ESayC("", l242)
                );
            });

            var s7main = Context("s7main", async () => {
                var r = VN.Find<Reimu>();
                var ma = VN.Find<Marisa>();
                var y = Yukari;
                await VN.Sequential(
                    ma.ESayC("happy", l244),
                    y.ESayC("happy", l245),
                    r.ESayC("surprise", l246),
                    y.ESayC("worry", l247),
                    r.ESayC("", l248),
                    ma.ESayC("worry", l249),
                    r.ESayC("angry", l250),
                    y.SayC(l251),
                    ma.ESayC("happy", l252),
                    r.ESayC("happy", l253)
                );
                await UpdateData(p => p.State = State.S7_1_SanaeTransform, new() {
                    SimultaneousActualization = true
                });
                var s = VN.Find<Sanae>();
                await VN.Sequential(
                    s.ESayC("happy", l255),
                    s.SayC(l256)
                );
                await UpdateData(p => p.State = State.S8_RavineReimu, new() {
                    SimultaneousActualization = true
                });
                await y.ESayC("worry", l257);
            });

            var s8kasen = Context("s8kasen", () => VN.Find<Kasen>().ESayC("", l260).Task);
            var s8doremy = Context("s8doremy", async () => {
                var d = VN.Find<Doremy>();
                var y = Yukari;
                await VN.Sequential(
                    d.ESayC("", l262),
                    y.ESayC("worry", l263),
                    d.ESayC("happy", l264),
                    y.ESayC("cry", l265),
                    d.ESayC("", l266)
                );
                y.Emote.RevokeOverride();
            });
            var s8main = Context("s8main", async () => {
                var y = Yukari;
                await VN.Sequential(
                    y.ESayC("worry", l268),
                    VN.Find<Reimu>().ESayC("surprise", l269),
                    y.SayC(l270),
                    y.ESayC("surprise", l271),
                    y.ESayC("angry", l272)
                );
                UpdateDataV(p => p.State = State.S9_BackToHouse, new() {
                    SimultaneousActualization = true
                });
            });
            var s9reimu = Context("s9reimu", () =>
                VN.Find<Reimu>().ESayC("", "Something smells off around this place...").Task);
            
            var s9main = Context("s9main", async () => {
                var y = Yukari;
                var s = VN.Find<Seiga>();
                using var sc = VN.Add(new ScreenColor());
                sc.Alpha = 0;
                using var y2 = VN.Add(new Yukari());
                y2.Alpha = 0;
                y2.Location.Value += V3(0, 1);
                y2.Name = "Other Yukari";
                await VN.Sequential(
                    y.ESayC("surprise", l275),
                    s.ESayC("worry", l276),
                    y.ESayC("worry", l277),
                    s.SayC(l278),
                    y.ESayC("angry", l279),
                    s.ESayC("smug", l280),
                    s.ESayC("", l281),
                    s.SayC(l282),
                    s.ESayC("surprise", l283),
                    s.ESayC("smug", l284),
                    s.SayC(l285),
                    s.ESayC("", l286),
                    s.ESayC("happy", l287),
                    y.ESayC("worry", l288),
                    y.SayC(l289),
                    s.ESayC("", l290),
                    s.ESayC("happy", l291),
                    s.ESayC("surprise", l292)
                );
                await s.MoveBy(V3(0, -1), 0.8f).And(s.FadeTo(0, 0.8f)).And(
                    VN.Wait(0.4f).Then(y2.MoveBy(V3(0, -1), 0.8f)
                        .And(y2.FadeTo(1, 0.8f))
                        .And(VN.SFX("vn-yukari-power").AsVnOp(VN))));
                s.ComputedTint.AddConst(new(0, 0, 0, 0));
                await VN.Sequential(
                    y2.ESayC("smug", l293),
                    y.ESayC("worry", l294),
                    y.ESayC("surprise", l295),
                    y2.ESayC("", l296),
                    y2.SayC(l297),
                    y2.SayC(l298),
                    y.ESayC("worry", l299),
                    y2.ESayC("smug", l300),
                    y.ESayC("surprise", l301),
                    y2.SayC(l302),
                    y.ESayC("angry", l303),
                    y2.ESayC("", l304),
                    y2.SayC(l305),
                    y2.ESayC("smug", l306),
                    VN.SFX("vn-yukari-power"),
                    sc.FadeTo(0.6f, 1f).Then(sc.FadeTo(0, 1f)).And(y2.EmoteSay("cry", l307)).C,
                    y2.ESayC("angry", l308),
                    y2.ESayC("worry", l309),
                    y2.SayC(l310),
                    VN.SFX("vn-yukari-power"),
                    sc.FadeTo(1f, 2f).Then(y2.FadeTo(0, 1.5f).And(sc.FadeTo(0, 2f)))
                        .And(y2.EmoteSay("cry", l311)).C,
                    y.ESayC("worry", l312),
                    y.SayC(l313)
                );
                UpdateDataV(p => p.State = State.S10_RavineMarisa, new() {
                    SimultaneousActualization = true
                });
            });

            var s10doremy = Context("s10doremy", () => VN.Find<Doremy>().ESayC("", l318).Task);

            var s10main = Context("s10main", async () => {
                var r = VN.Find<Reimu>();
                using var rgp = new UnityRenderGroup(VN, "pool", 2, false);
                using var p = VN.Add(new PoolBG());
                using var m = VN.Add(new Marisa());
                m.Alpha = 0.6f;
                m.RenderGroup.Value = rgp;
                await m.SetEmote("surprise");
                using var rr = VN.Add(new Reimu());
                rr.Location.Value = V3(4, 0);
                rr.Alpha = 0.6f;
                rr.RenderGroup.Value = rgp;
                p.RenderGroup.Value = rgp;
                using var s = VN.Add(new Sanae());
                s.Alpha = 0;
                var y = Yukari;
                s.Name = m.Name = y.Name;
                await VN.Sequential(
                    y.ESayC("happy", l320),
                    r.ESayC("surprise", l321),
                    s.ESayC("worry", l322),
                    r.ESayC("", l323),
                    m.ESayC("worry", l324),
                    r.ESayC("worry", l325)
                );
                HideMD();
                await rg.DoTransition(new RenderGroupTransition.Fade(rgp, 2f));
                //vn.RunBGM("php.seiga");
                md.RenderGroup.Value = rgp;
                ShowMD();
                await VN.Sequential(
                    m.ESayC("surprise", l326),
                    r.ESayC("", l327),
                    m.SayC(l328),
                    r.ESayC("angry", l329),
                    m.ESayC("angry", l330)
                );
                HideMD();
                await rgp.DoTransition(new RenderGroupTransition.Fade(rg, 2f));
                md.RenderGroup.Value = rg;
                await r.SetEmote("");
                ShowMD();
                await VN.Sequential(
                    r.SayC(l331),
                    r.SayC(l332),
                    r.SayC(l333),
                    m.ESayC("angry", l334),
                    r.SayC(l335),
                    r.SayC(l336),
                    r.SayC(l337),
                    m.ESayC("cry", l338),
                    VN.SFX("vn-yukari-power")
                );
                GoToMap("Lake", p => p.State = State.S11_SelfAtLake);
            });

            var s11main = Context("s11main", async () => {
                using var rgp = new UnityRenderGroup(VN, "pool", 2, false);
                using var p = VN.Add(new PoolBG());
                using var m = VN.Add(new Marisa());
                m.Visible.Value = false;
                await m.SetEmote("surprise");
                using var y = VN.Add(new Yukari());
                await y.SetEmote("cry");
                y.Alpha = 0.6f;
                p.RenderGroup.Value = rgp;
                y.RenderGroup.Value = rgp;
                m.Name = y.Name;
                await VN.Sequential(
                    m.SayC("I... I'm... I'm...")
                );
                HideMD();
                await rg.DoTransition(new RenderGroupTransition.Fade(rgp, 2f));
                md.RenderGroup.Value = rgp;
                ShowMD();
                await VN.Sequential(
                    y.ESayC("cry", l342),
                    y.ESayC("worry", l343),
                    y.SayC(l344),
                    y.ESayC("cry", l345),
                    y.SayC(l346),
                    y.SayC(l347),
                    y.ESayC("worry", l348),
                    y.SayC(l349),
                    y.ESayC("angry", l350)
                );
                HideMD();
                await rgp.DoTransition(new RenderGroupTransition.Fade(rg, 2f));
                md.RenderGroup.Value = rg;
                ShowMD();
                await VN.Sequential(
                    y.SayC(l351),
                    y.ESayC("worry", l352),
                    y.SayC(l353),
                    y.ESayC("angry", l354)
                );
                UpdateDataV(p => p.State = State.S12_SelfAtRavine, new() {
                    SimultaneousActualization = true
                });
            });

            var s12main = Context("s12main", async () => {
                var y = Yukari;
                await VN.Sequential(
                    y.ESayC("happy", l356_1),
                    y.ESayC("worry", l357),
                    y.SayC(l358),
                    y.ESayC("angry", l359)
                );
                y.Emote.RevokeOverride();
                UpdateDataV(p => p.State = State.S13_HouseReimu, new() {
                    SimultaneousActualization = true
                });
            });

            var s13main = Context("s13main", async () => {
                var y = Yukari;
                var r = VN.Find<Reimu>();
                using var s = VN.Add(new Seiga());
                using var u = VN.Add(new Unknown());
                s.Alpha = 0;
                s.Location.Value = V3(3, 1);
                DMKVNState.RunningAudioTrackProxy? sbgm = null;
                await VN.Sequential(
                    y.ESayC("worry", l362),
                    r.ESayC("angry", l363),
                    y.ESayC("happy", l364),
                    r.SayC(l365),
                    y.ESayC("worry", l366),
                    new LazyAction(() => sbgm = VN.RunBGM("php.seiga")),
                    u.SayC(l367),
                    y.ESayC("surprise", l368),
                    VN.SFX("vn-yukari-power"),
                    s.FadeTo(1f, 0.8f).And(s.MoveBy(V3(0, -1), 0.8f)),
                    s.ESayC("smug", l369),
                    s.SayC(l370),
                    y.ESayC("surprise", l370_1),
                    s.ESayC("surprise", l371),
                    s.ESayC("worry", l372),
                    s.ESayC("happy", l373),
                    s.ESayC("smug", l374),
                    r.ESayC("", l375),
                    s.ESayC("", l376),
                    r.SayC(l377),
                    s.SayC(l378),
                    y.ESayC("", l379)
                );
                HideMD();
                sbgm?.FadeOut(1);
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)).And(
                    VN.SFX("vn-yukari-power").AsVnOp(VN).Then(VN.Wait(0.8f))
                        .Then(VN.SFX("vn-yukari-power").AsVnOp(VN)));
                s.Alpha = 0;
                md.Clear();
                await UpdateData(p => p.State = State.S14_HouseSelf, new() {
                    SimultaneousActualization = true
                });
                VN.RunBGM("php.house");
                await rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f));
                ShowMD();
                await VN.Sequential(
                    y.ESayC("surprise", l382),
                    y.ESayC("worry", l382_1),
                    y.SayC(l383),
                    y.SayC(l384)
                );
                UpdateDataV(p => p.State = State.S15_Final, new() {
                    SimultaneousActualization = true
                });
            });

            var s15main = Context("s15main", async () => {
                var y = Yukari;
                using var r = VN.Add(new Reimu());
                using var m = VN.Add(new Seiga());
                m.Name += " (Memory of a Dream)";
                r.Location.Value = V3(1, 0);
                r.Alpha = 0;
                m.Alpha = 0;
                await VN.Sequential(
                    y.ESayC("happy", l386),
                    r.ESayC("cry", l386_1),
                    r.MoveBy(V3(-1, 0), 1).And(r.FadeTo(1, 1)),
                    r.ESayC("worry", l387),
                    y.SayC(l388),
                    r.ESayC("happy", l389),
                    r.ESayC("", l390),
                    y.ESayC("worry", l391),
                    m.ESayC("", l392),
                    y.SayC(l393),
                    y.ESayC("angry", l394),
                    y.ESayC("happy", l395),
                    r.ESayC("surprise", l396),
                    y.SayC(l397),
                    r.SayC(l398),
                    y.ESayC("", l399),
                    r.ESayC("angry", l400),
                    y.ESayC("happy", l401),
                    r.SayC(l401_1)
                );
                ServiceLocator.Find<IAudioTrackService>().ClearRunningBGM();
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 2f));
                completion.SetResult(default);
            });

            var mimaTalk = Context("mimaTalk", async () => {
                var y = Yukari;
                var m = VN.Find<Mima>();
                await VN.Sequential(
                    m.ESayC("worry", l405),
                    y.ESayC("surprise", l406),
                    m.SayC(l407),
                    y.ESayC("happy", l408),
                    m.ESayC("worry", l409)
                );
            });
            var suwakoTalk = Context("suwakoTalk", async () => {
                var y = Yukari;
                var s = VN.Find<Suwako>();
                await VN.Sequential(
                    s.ESayC("happy", l410),
                    y.ESayC("worry", l411),
                    s.ESayC("", l412),
                    y.SayC(l413),
                    s.ESayC("worry", l414),
                    y.SayC(l415),
                    s.ESayC("", l416),
                    y.ESayC("angry", l417),
                    s.ESayC("worry", l418),
                    y.ESayC("", l419),
                    s.SayC(l420),
                    s.SayC(l421),
                    y.ESayC("worry", l422),
                    s.ESayC("surprise", l423),
                    y.ESayC("happy", l424),
                    y.ESayC("", l425),
                    s.SayC(l426),
                    s.ESayC("happy", l427)
                );
            });



            var dialogueMap = new Dictionary<Type, Vector3>() {
                {typeof(Reimu), V3(1, 2f)},
                {typeof(Kasen), V3(1.2f, 2.4f)},
                {typeof(Doremy), V3(0.8f, 2.3f)},
                {typeof(Nitori), V3(0.9f, 1.9f)},
                {typeof(Cirno), V3(1f, 2f)},
                {typeof(Seiga), V3(0.8f, 2.5f)}
            };
            Vector3 SpeakForType<T>() => dialogueMap[typeof(T)];
            
            void ConfigureUniversal(string mapName, PHIdealizedState i, PHADVData d) {
                i.Assert(new CharacterAssertion<Yukari>(VN) {
                    ExtraBind = y => y.Visible.BaseValue = false
                });
                foreach (var map in maps) {
                    var isCurr = map.key == mapName;
                    i.Assert(new InteractableAssertion(m, () => GoToMap(map.key), $"go to {map.key}") {
                        Location = V3(7.3f, 1.8f + map.mapLinkOffset),
                        Type = new InteractableType.Map(isCurr),
                        Hover = new Interactable.HoverAction.VNOp(() => 
                            VN.Find<Yukari>().Say(isCurr ? 
                                $"I'm currently at {map.desc(d)}." :
                                $"Should I head over to {map.desc(d)}?"))
                    });
                }
            }
            
            ms.ConfigureMap("YukariHouse", (i, d) => {
                ConfigureUniversal("YukariHouse", i, d);
                i.Assert(new EntityAssertion<NormalBedroomBG>(VN));
                var houseTheme = "php.house";
                if (d.DelayedState == State.S9_BackToHouse)
                    houseTheme = "php.seiga";
                i.Assert(new BGMAssertion(VN, houseTheme));
                if (d.State == State.S0_Initial)
                    i.SetEntryVN(s0main);
                if (d.State < State.S13_HouseReimu && d.DelayedState != State.S9_BackToHouse) {
                    if (d.State >= State.S2_ToHouse)
                        i.Assert(new CharacterAssertion<Kasen>(VN) {
                            Location = V3(d.State < State.S8_RavineReimu ? 3 : -3, 0)
                        }.WithChildren(new InteractableBCtxAssertion(m, d.State switch {
                            <= State.S2_ToHouse => s2main,
                            <= State.S3_ToMoriya => s3kasen,
                            <= State.S4_ToMistyLake => s4kasen,
                            <= State.S5_ToRavine => s5kasen,
                            <= State.S6_BackToMistyLake => s6kasen,
                            <= State.S7_BackToMoriya => s7kasen,
                            _ => s8kasen
                        }) { Location = Kasen.SpeakIconOffset }));
                    if (d.State >= State.S2_1_DoremyEntry)
                        i.Assert(new CharacterAssertion<Doremy>(VN) {
                            Location = V3(d.State < State.S8_RavineReimu ? -3 : 3, 0)
                        }.WithChildren(new InteractableBCtxAssertion(m, d.State switch {
                            < State.S5_ToRavine => s3doremy,
                            < State.S8_RavineReimu => s5doremy,
                            < State.S10_RavineMarisa => s8doremy,
                            _ => s10doremy
                        }) { Location = Doremy.SpeakIconOffset }));
                }
                if (d.State == State.S9_BackToHouse)
                    i.Assert(new CharacterAssertion<Seiga>(VN) {
                        Tint = new(0.4f, 0.4f, 0.4f)
                    }.WithChildren(new InteractableBCtxAssertion(m, s9main)
                        {Location = Seiga.SpeakIconOffset }));
                if (d.State == State.S13_HouseReimu) {
                    i.Assert(new CharacterAssertion<Reimu>(VN) {
                        Location = V3(-3, 0)
                    }.WithChildren(new InteractableBCtxAssertion(m, s13main)
                        {Location = Reimu.SpeakIconOffset }));
                }

            });
            
            ms.ConfigureMap("MoriyaShrine", (i, d) => {
                ConfigureUniversal("MoriyaShrine", i, d);
                if (d.State is < State.S3_ToMoriya or >= State.S15_Final)
                    i.Assert(new CharacterAssertion<Kanako>(VN) {
                    }.WithChildren(new InteractableBCtxAssertion(m, s0kanako) {
                        Location = V3(1f, 2.8f)
                    }));
                else if (d.State < State.S8_RavineReimu) {
                    i.Assert(new CharacterAssertion<Reimu>(VN) {
                        Location = V3(-3, 0)
                    }.WithChildren(new InteractableBCtxAssertion(m, 
                        d.State == State.S7_BackToMoriya ? s7reimu : s3reimu) {
                        Location = SpeakForType<Reimu>()
                    }));
                    if (d.State == State.S7_BackToMoriya) {
                        i.Assert(new CharacterAssertion<Marisa>(VN) {
                            Location = V3(3, 0)
                        }.WithChildren(new InteractableBCtxAssertion(m, s7main) {
                            Location = V3(1, 1.7f)
                        }));
                    } else {
                        i.Assert(new CharacterAssertion<Sanae>(VN) {
                            Location = V3(3, 0)
                        }.WithChildren(new InteractableBCtxAssertion(m, d.State switch {
                            State.S3_ToMoriya => s3main,
                            _ => s4sanae
                        }) {
                            Location = V3(1.2, 2.3f)
                        }));
                    }
                } else if (d.State < State.S11_SelfAtLake && d.DelayedState > State.S7_BackToMoriya)
                    i.Assert(new CharacterAssertion<Suwako>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, suwakoTalk)
                            { Location = V3(1, 1.3f) }));
                i.Assert(d.State == State.S7_BackToMoriya ? 
                    new EntityAssertion<ForestBG>(VN) : new EntityAssertion<Shrine2BG>(VN));
                i.Assert(new BGMAssertion(VN, d.State == State.S7_BackToMoriya ? "php.forest" : "php.moriya"));
            });
            
            ms.ConfigureMap("Ravine", (i, d) => {
                ConfigureUniversal("Ravine", i, d);
                i.Assert(new BGMAssertion(VN, "php.ravine"));
                i.Assert(new EntityAssertion<WaterfallBG>(VN));
                if (d.State is < State.S5_ToRavine or >= State.S15_Final)
                    i.Assert(new CharacterAssertion<Nitori>(VN) {}
                        .WithChildren(new InteractableBCtxAssertion(m, s0nitori) 
                            { Location = SpeakForType<Nitori>() }));
                else if (d.State < State.S8_RavineReimu)
                    i.Assert(new CharacterAssertion<Iku>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, 
                            d.State == State.S5_ToRavine ? s5main : s6iku)
                            { Location = V3(0.7f, 2.2f)}));
                else if (d.State is >= State.S8_RavineReimu and <= State.S10_RavineMarisa)
                    i.Assert(new CharacterAssertion<Reimu>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m,
                            d.State switch {
                                State.S8_RavineReimu => s8main,
                                State.S9_BackToHouse => s9reimu,
                                _ => s10main
                            }) { Location = SpeakForType<Reimu>()}));
                else if (d.State is State.S12_SelfAtRavine)
                    i.SetEntryVN(s12main);
                
            });
            ms.ConfigureMap("Lake", (i, d) => {
                ConfigureUniversal("Lake", i, d);
                i.Assert(new EntityAssertion<LakeBG>(VN));
                if (d.State == State.S11_SelfAtLake)
                    i.SetEntryVN(s11main);
                //use delayed state to not assert cirno when seiga transform finishes (6 -> 6_1 -> 7)
                if (d.DelayedState is (< State.S11_SelfAtLake and not State.S6_BackToMistyLake) or >= State.S15_Final)
                    i.Assert(new CharacterAssertion<Cirno>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, 
                            d.State switch {
                                State.S4_ToMistyLake => s4main,
                                State.S5_ToRavine => s5cirno,
                                _ => s0cirno,
                            }) {
                            Location = SpeakForType<Cirno>()
                        }));
                if (d.State is State.S6_BackToMistyLake)
                    i.Assert(new CharacterAssertion<Nitori>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, s6main)
                            { Location = SpeakForType<Nitori>() }));
                var audio = "php.lake";
                if (d.State == State.S6_1_SeigaTransform ||
                    (d.DelayedState == State.S6_BackToMistyLake && d.State == State.S7_BackToMoriya)) {
                    i.Assert(new CharacterAssertion<Seiga>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, s7seiga)
                            { Location = SpeakForType<Seiga>() }));
                    audio = "php.seiga";
                }
                i.Assert(new BGMAssertion(VN, audio));
            });
            ms.ConfigureMap("HakureiShrine", (i, d) => {
                ConfigureUniversal("HakureiShrine", i, d);
                i.Assert(new EntityAssertion<ShrineCourtyardBG>(VN));
                i.Assert(new BGMAssertion(VN, "php.hakurei"));
                if (d.State == State.S1_ToReimu) {
                    i.Assert(new CharacterAssertion<Reimu>(VN));
                    i.SetEntryVN(s1main);
                } else if (d.State == State.S15_Final)
                    i.SetEntryVN(s15main);
                else if (d.State <= State.S10_RavineMarisa && d.DelayedState > State.S1_ToReimu)
                    i.Assert(new CharacterAssertion<Mima>(VN)
                        .WithChildren(new InteractableBCtxAssertion(m, mimaTalk)
                            { Location = V3(1, 2.3f) }));
                
            });
            return ms;
        }
        
        public record PHIdealizedState(Executing e) : ADVIdealizedState(e) {
            protected override Task FadeIn() {
                return e.rgb.DoTransition(new RenderGroupTransition.Fade(e.rg, 0.7f)).Task;
            }
            protected override Task FadeOut() {
                return e.rg.DoTransition(new RenderGroupTransition.Fade(e.rgb, 0.7f)).Task;
            }
        }
    }
        
    [Serializable]
    public record PHADVData(Suzunoya.Data.InstanceData VNData) : ADVData(VNData) {
        public State State;
        public State DelayedState;
    }

    public enum State {
        S0_Initial,
        S1_ToReimu,
        S2_ToHouse,
        S2_1_DoremyEntry,
        S3_ToMoriya,
        S4_ToMistyLake,
        S5_ToRavine,
        S6_BackToMistyLake,
        S6_1_SeigaTransform,
        S7_BackToMoriya,
        S7_1_SanaeTransform,
        S8_RavineReimu,
        S9_BackToHouse,
        S10_RavineMarisa,
        S11_SelfAtLake,
        S12_SelfAtRavine,
        S13_HouseReimu,
        S14_HouseSelf,
        S15_Final,
    }
    
    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.ADVData.CurrentMap == "")
            throw new Exception("Purple Heart was loaded with no current map.");
        Logs.Log("Starting Purple Heart execution...");
        return new Executing(inst);
    }

    public override ADVData NewGameData() => new PHADVData(new(SaveData.r.GlobalVNData)) {
        CurrentMap = "YukariHouse"
    };
}
}