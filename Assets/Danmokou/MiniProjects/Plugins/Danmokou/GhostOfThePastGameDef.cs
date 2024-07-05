using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
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
using Danmokou.UI.XML;
using Danmokou.VN;
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
using static MiniProjects.VN.ScriptLocalization.GhostOfThePast;

namespace MiniProjects.VN.PurpleHeart {

[CreateAssetMenu(menuName = "Data/ADV/GOTP Game")]
public class GhostOfThePastGameDef : ADVGameDef {
    public Sprite evidenceReviewBg = null!;
    
    private class Executing : DMKExecutingADV<Executing.GOTPIdealizedState, GOTPADVData> {
        private readonly EvidenceRequest<Evidence> evidenceRequest;
        //--- Lerpers
        private readonly PushLerper<float> evSize = new(0.5f, (a, b, t) => BMath.LerpU(a, b, Easers.EOutBack(t)));

        
        public Executing(GhostOfThePastGameDef gdef, ADVInstance inst) : base(inst) {
            evidenceRequest = new(VN);
            evSize.Push(1f);
            
            tokens.Add(MapWillUpdate.Subscribe(_ => {
                Logs.Log($"Setting delayed state from {Data.DelayedState} to {Data.State}");
                Data.DelayedState = Data.State;
            }));
            
            var evidenceScreen = new UIScreen(menu, "EVIDENCE", UIScreen.Display.Default) { 
                Builder = (s, ve) => {
                    //don't let events fall-through
                    s.HTML.pickingMode = PickingMode.Position;
                    s.Margin.SetLRMargin(720, 720);
                    //s.HTML.Q("ControlsHelper").RemoveFromHierarchy();
                    var c0 = ve.AddColumn();
                    c0.style.maxWidth = 40f.Percent();
                    var c1 = ve.AddColumn();
                    c1.style.maxWidth = 60f.Percent();
                    c1.style.alignItems = Align.Center;
                },
                MenuBackgroundOpacity = UIScreen.DefaultMenuBGOpacity
            };
            var evInfo = evidenceScreen.ColumnRender(1);
            var evs = new UIColumn(evidenceScreen, null, 
                Data.Evidences.Select(ev => new UINode {
                    ShowHideGroup = new UIColumn(evInfo, 
                        new UINode { Prefab = XMLUtils.Prefabs.PureTextNode, Passthrough = true, OnBuilt = n => n.Style.minHeight = 500 }
                            .WithCSS(XMLUtils.fontBiolinumClass, XMLUtils.small1Class)
                            .Bind(new FlagView(new(() => ev.Enabled, ev.Description, 
                                "I don't have any evidence to put here."))),
                        new UIButton("Present Evidence", UIButton.ButtonType.Confirm, _ => {
                            var __ = evidenceRequest.Present(ev).ContinueWithSync();
                            return new UIResult.ReturnToScreenCaller();
                        }) { VisibleIf = () => ev.Enabled && evidenceRequest.CanPresent })
                }.Bind(new FlagView(new(() => ev.Enabled, ev.Title, "---")))
            ));
            evidenceScreen.SetFirst(evs);
            menu.AddScreen(evidenceScreen);
            //Since this button is always present, we don't need to bother
            // going through Assertions for it
            // (Though we don't yet have an Assertion for generic UITK objects, only one for icon-based interactables)
            var toEvidenceButton = new UIButton(null, 
                UIButton.ButtonType.Confirm, _ => new UIResult.GoToScreen(evidenceScreen, menu.Unselect)) {
                OnBuilt = n => {
                    var l = n.HTML.Q<Label>();
                    l.style.backgroundColor = Color.clear;
                    l.style.backgroundImage = new StyleBackground(gdef.evidenceReviewBg);
                    l.SetPadding(54, 64, 54, 64);
                    tokens.Add(evSize.Subscribe(s => n.HTML.transform.scale = new UnityEngine.Vector3(s, s, 1)));
                },
                VisibleIf = () => Data.EvidenceButtonVisible
            }.Bind(new FlagView(new(() => evidenceRequest.CanPresent, "Show Evidence", "Evidence")))
            .Bind(new FixedXMLView(new(new FixedXMLObject(120, 90, null, null) {
                Pivot = XMLUtils.Pivot.TopLeft
            })));
            menu.AddNodeDynamic(toEvidenceButton);
            tokens.Add(evidenceRequest.RequestsChanged.Subscribe(_ => {
                evSize.PushIfNotSame(evidenceRequest.CanPresent ? 1.4f : 1f);
            }));
        }

        public override void RegularUpdate() {
            base.RegularUpdate();
            evSize.Update(ETime.FRAME_TIME);
        }

        record MapData(string key, Func<GOTPADVData, string> desc, float mapLinkOffset);

        public const string MarisaHouse = "marisaHouse";
        private const string SDM = "scarletDevilMansion";
        private const string HG = "hakugyokurou";
        private const string Myouren = "myouren";
        
        private MapData[] maps = {
            new(MarisaHouse, _ => "my house", 2),
            new(SDM, _ => "the Scarlet Devil Mansion", 1),
            new(HG, _ => "Hakugyokurou", 0),
            new(Myouren, _ => "Myouren Temple", -1)
        };

        private Marisa Marisa => VN.Find<Marisa>();
        
        protected override MapStateManager<GOTPIdealizedState, GOTPADVData> ConfigureMapStates() {
            var m = Manager;
            var ms = new MapStateManager<GOTPIdealizedState, GOTPADVData>(this, () => new(this));
            //These must be separated out of ConfigureMap since they should not be recreated
            // when the idealized state is recreated.
            var s0main = Context("s0main", async () => {
                var m98 = VN.Find<Marisa98>();
                var m = Marisa;
                m.LocalLocation.Value -= V3(0, 3);
                using var sc = VN.Add(new ScreenColor());
                sc.Tint.Value = Color.black._();
                sc.Alpha = 0.95f;
                await VN.Sequential(
                    //m.ESayC("happy", l0),
                    //m98.Say(l1, flags: SpeakFlags.Anonymous).And(
                    //    sc.FadeTo(0.8f, 0.5f),
                    //    m98.MoveBy(V3(0.8f, 0), 0.7f)).C,
                    m.MoveBy(V3(0, 0.5f), 0.4f).And(m.EmoteSay("happy", l2)).C,
                    m98.Say(l3, flags: SpeakFlags.Anonymous).And(
                        sc.FadeTo(0.8f, 0.5f),
                        m98.MoveBy(V3(0.7f, 0), 0.7f)).C,
                    m.MoveBy(V3(0, 0.6f), 0.4f).And(m.EmoteSay("happy", l4)).C,
                    m98.Say(l5, flags: SpeakFlags.Anonymous).And(
                        sc.FadeTo(0.65f, 0.5f),
                        m98.MoveBy(V3(0.7f, 0), 0.7f)).C,
                    m.MoveBy(V3(0, 0.6f), 0.4f).And(m.EmoteSay("worry", l6)).C,
                    m98.Say(l7, flags: SpeakFlags.Anonymous).And(
                            sc.FadeTo(0.5f, 0.5f),
                            m98.MoveBy(V3(0.7f, 0), 0.7f)).C,
                    m.MoveBy(V3(0.3f, 1.2f), 0.6f).And(
                        m.Disturb(m.ComputedLocation, JumpY(2f), 0.6f),
                        m.EmoteSay("surprise", l8)).C,
                    m98.Say(l9, flags: SpeakFlags.Anonymous).And(
                            sc.FadeTo(0f, 0.1f),
                            m98.MoveBy(V3(0.7f, 0), 0.7f)).C
                );
                var footstep = VN.Source("vn-footstep-1");
                await m.EmoteSay("cry", l10).And(
                    m.MoveBy(V3(8, 0), 1.5f, Easers.EInBack));
                HideMD();
                await GoToMap(SDM);
                //assertion-created objects are disposed between maps, so requery
                m = Marisa;
                using var u = VN.Add(new Marisa98());
                u.Alpha = 0;
                var r = VN.Find<Remilia>();
                var f = VN.Find<Flandre>();
                var s = VN.Find<Sakuya>();
                await m.SetEmote("cry");
                await m.MoveBy(V3(8, 0), 1.5f, Easers.EOutSine);
                footstep.Stop();
                ShowMD();
                await VN.Sequential(
                    m.ESayC("angry", l12),
                    m.SayC(l11),
                    r.ESayC("happy", l13),
                    m.ESayC("worry", l14),
                    r.ESayC("surprise", l15),
                    r.ESayC("happy", l16),
                    f.EmoteSay("happy", l17).And(f.Disturb(f.ComputedLocation, JumpY(0.6f), 0.4f)).C,
                    f.EmoteSay("", l18).C,
                    m.ESayC("worry", l19),
                    f.ESayC("happy", l20),
                    f.ESayC("smug", l21),
                    m.SayC(l22),
                    s.ESayC("worry", l23),
                    m.SayC(l24),
                    s.ESayC("worry", l25),
                    r.ESayC("angry", l26),
                    r.SayC(l27),
                    r.ESayC("surprise", l28),
                    r.ESayC("angry", l29),
                    f.ESayC("cry", l30),
                    r.ESayC("", l31),
                    r.ESayC("smug", l32),
                    r.ESayC("angry", l33),
                    r.ESayC("", l34),
                    s.ESayC("worry", l35),
                    r.ESayC("surprise", l36),
                    r.ESayC("smug", l37),
                    r.ESayC("happy", l38),
                    r.ESayC("", l39),
                    r.ESayC("surprise", l40),
                    m.ESayC("worry", l41),
                    r.ESayC("angry", l42),
                    r.ESayC("surprise", l43),
                    r.ESayC("", l44),
                    r.ESayC("smug", l45),
                    r.ESayC("surprise", l46),
                    r.SayC(l47),
                    r.ESayC("smug", l48),
                    r.ESayC("surprise", l49),
                    //r.SayC(l50),
                    f.ESayC("surprise", l51),
                    r.ESayC("angry", l52),
                    r.ESayC("smug", l53),
                    r.ESayC("worry", l54),
                    r.SayC(l55),
                    r.ESayC("angry", l56),
                    r.ESayC("smug", l56_1),
                    r.SayC(l57),
                    r.SayC(l58),
                    r.SayC(l59),
                    m.EmoteSay("surprise", l60).And(m.Disturb(m.ComputedLocation, JumpY(0.5f), 0.4f)).C,
                    m.SayC(l61),
                    m.ESayC("angry", l62),
                    m.ESayC("happy", l63),
                    s.ESayC("happy", l64),
                    f.ESayC("worry", l65),
                    r.ESayC("happy", l66),
                    m.ESayC("worry", l67),
                    s.ESayC("smug", l68),
                    f.ESayC("happy", l69),
                    m.ESayC("cry", l70),
                    m.ESayC("worry", l71),
                    f.ESayC("surprise", l72),
                    m.ESayC("", l73),
                    m.SayC(l74),
                    m.ESayC("surprise", l75),
                    m.ESayC("worry", l76),
                    r.ESayC("smug", l77),
                    m.SayC(l78),
                    m.ESayC("surprise", l79),
                    m.SayC(l80),
                    m.SayC(l81),
                    m.ESayC("worry", l82),
                    m.ESayC("angry", l83),
                    r.ESayC("worry", l84),
                    s.ESayC("worry", l85),
                    r.ESayC("angry", l86),
                    m.ESayC("cry", l87),
                    f.ESayC("cry", l88),
                    m.ESayC("worry", l89),
                    r.ESayC("happy", l90),
                    r.ESayC("smug", l91),
                    r.SayC(l92),
                    r.SayC(l93),
                    r.ESayC("surprise", l94),
                    r.SayC(l95),
                    sc.FadeTo(0.3f, 0.8f).And(
                        u.Say(l96, flags:SpeakFlags.Anonymous)).C
                );
                footstep = VN.Source("vn-footstep-1");
                await m.EmoteSay("surprise", l97).And(
                    m.MoveBy(V3(-8, 0), 1.5f, Easers.EInBack),
                    sc.FadeTo(0, 1));
                footstep.Stop();
                await VN.SpinUntilConfirm();
                await vn.Sequential(
                    s.ESayC("surprise", l99),
                    r.ESayC("worry", l100),
                    f.ESayC("worry", l101)
                );
                HideMD();
                await GoToMap(HG);
                //assertion-created objects are disposed between maps, so requery
                m = Marisa;
                var ym = VN.Find<Youmu>();
                ShowMD();
                await vn.Sequential(
                    m.ESayC("cry", l103),
                    ym.ESayC("worry", l104), 
                    m.ESayC("surprise", l105),
                    ym.ESayC("happy", l106),
                    m.ESayC("worry", l107),
                    ym.ESayC("", l108),
                    ym.SayC(l109)
                );
                UpdateDataV(d => d.State = d.State with { IsStarting = false }, new() {
                    Options = ActualizeOptions.Simultaneous
                });
                Yuyuko yu = null!;
                await VN.Wait(() => (yu = VN.FindEntity<Yuyuko>()!) != null);
                await vn.Sequential(
                    yu.ESayC("", l111),
                    m.ESayC("cry", l112),
                    yu.SayC(l113),
                    m.ESayC("surprise", l114),
                    yu.SayC(l115),
                    yu.SayC(l116),
                    yu.ESayC("happy", l117),
                    ym.ESayC("worry", l118),
                    ym.SayC(l119),
                    m.ESayC("angry", l120),
                    ym.ESayC("worry", l121),
                    ym.SayC(l122),
                    ym.ESayC("", l123),
                    m.ESayC("surprise", l124),
                    ym.RotateTo(V3(0, -180), 1f, Easers.EOutSine).And(ym.MoveBy(V3(-1, 0), 1f)).Then(ym.EmoteSay("worry", l125)).C,
                    yu.ESayC("worry", l126),
                    ym.RotateTo(V3(0, 0), 0.4f, Easers.EOutSine).And(
                        ym.MoveBy(V3(1, 0), 0.4f), 
                        ym.EmoteSay("happy", l127)).C,
                    m.ESayC("angry", l128),
                    ym.ESayC("worry", l129),
                    m.ESayC("surprise", l130),
                    yu.ESayC("", l131),
                    m.ESayC("angry", l132),
                    yu.ESayC("happy", l133),
                    m.ESayC("surprise", l134),
                    ym.ESayC("surprise", l135),
                    yu.ESayC("", l136),
                    ym.ESayC("happy", l137),
                    m.ESayC("cry", l138),
                    yu.ESayC("", l139),
                    m.SayC(l140),
                    ym.ESayC("", l141),
                    yu.ESayC("", l142),
                    ym.ESayC("happy", l143),
                    ym.ESayC("worry", l144),
                    m.ESayC("worry", l145),
                    m.ESayC("angry", l146),
                    ym.ESayC("happy", l147),
                    yu.ESayC("", l148),
                    yu.ESayC("happy", l149),
                    yu.ESayC("", l150),
                    ym.ESayC("worry", l151),
                    m.ESayC("smug", l152),
                    ym.ESayC("cry", l153),
                    yu.ESayC("", l154),
                    yu.SayC(l155),
                    m.ESayC("worry", l156),
                    yu.ESayC("happy", l157),
                    m.ESayC("surprise", l158),
                    yu.SayC(l159),
                    m.ESayC("worry", l160),
                    yu.ESayC("", l161),
                    ym.ESayC("worry", l162),
                    new LazyAction(() => UpdateDataV(d => d.EvidenceButtonVisible = true)),
                    yu.SayC(l163),
                    ym.SayC(l164),
                    yu.ESayC("worry", l165),
                    ym.ESayC("worry", l166),
                    yu.ESayC("happy", l167),
                    m.ESayC("worry", l168)
                );
            });

            StrongBoundedContext<InterruptionStatus> yuyu1Interrupt(Evidence ev) => new(VN, "interrupt_yuyu1", async () => {
                var m = Marisa;
                var ym = vn.Find<Youmu>();
                var yu = vn.Find<Yuyuko>();
                if (ev is Evidence.Sakuya { Status: >= Evidence.Sakuya.Level.L1}) {
                    await vn.Sequential(
                        m.ESayC("worry", l175),
                        yu.ESayC("surprise", l176),
                        ym.ESayC("worry", l177),
                        yu.ESayC("worry", l178),
                        yu.SayC(l179),
                        ym.SayC(l180),
                        m.ESayC("surprise", l181),
                        ym.ESayC("cry", l182),
                        m.ESayC("cry", l183),
                        yu.ESayC("happy", l184),
                        yu.ESayC("", l185),
                        yu.ESayC("angry", l186),
                        yu.ESayC("happy", l187),
                        m.ESayC("worry", l188),
                        yu.EmoteSay("", l189).And(yu.MoveBy(V3(6.5f, 0), 2f)).Then(yu.Say(l190)).C,
                        m.ESayC("worry", l191),
                        ym.ESayC("angry", l192),
                        m.ESayC("cry", l193),
                        yu.Say(l194).And(yu.MoveBy(V3(-6.5f, 0), 2f)).Then(yu.Say(l195)).C,
                        yu.SayC(l196),
                        yu.SayC(l197),
                        m.ESayC("worry", l198)
                    );
                    UpdateDataV(d => {
                        d.EvYuyuko.Status = Evidence.Yuyuko.Level.L1;
                        d.State = d.State with { Yuyuko = YuyukoState.L1 } ;
                    });
                } else {
                    await yu.ESayC("worry", l173);
                }
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            var yuyu1 = Context("yuyu1", async () => {
                using var _ = evidenceRequest.Request(yuyu1Interrupt);
                await vn.Find<Youmu>().ESayC("", l170);
                await Marisa.ESayC("worry", l171);
            });
            var yuyu2 = Context("yuyu2", async () => {
                var yu = vn.Find<Yuyuko>();
                await vn.Sequential(
                    yu.SayC(l195),
                    yu.SayC(l196),
                    yu.SayC(l197),
                    Marisa.ESayC("worry", l198)
                );
            });
            
            BoundedContext<InterruptionStatus> remi1Interrupt(Evidence ev) => new(VN, "", async () => {
                var r = VN.Find<Remilia>();
                var s = VN.Find<Sakuya>();
                var m = Marisa;
                if (ev is Evidence.Murasa) {
                    await vn.Sequential(
                        m.ESayC("happy", l214),
                        rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1.5f)),
                        rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1.5f)).And(r.EmoteSay("happy", l216)).C,
                        s.ESayC("happy", l217),
                        s.ESayC("", l218),
                        r.ESayC("", l219),
                        s.MoveBy(V3(-7, 0), 1.4f).And(s.Say(l221)),
                        m.ESayC("worry", l222),
                        s.ESayC("worry", l223),
                        s.Say(l224).And(s.MoveBy(V3(7, 0), 1.4f)).C,
                        m.ESayC("angry", l225),
                        r.ESayC("worry", l226),
                        s.ESayC("happy", l227),
                        m.ESayC("surprise", l228),
                        s.ESayC("", l229),
                        m.ESayC("worry", l230)
                    );
                    UpdateDataV(d => {
                        d.EvSakuya.Status = Evidence.Sakuya.Level.L1;
                        d.State = d.State with { Remilia = RemiliaState.L1 };
                    });
                } else {
                    await r.ESayC("angry", l211);
                    await m.ESayC("cry", l212);
                }
                return InterruptionStatus.Abort;
            });
            var remi1 = Context("", async () => {
                var r = VN.Find<Remilia>();
                var s = VN.Find<Sakuya>();
                var m = Marisa;
                await vn.Sequential(
                    r.ESayC("happy", l200),
                    m.ESayC("worry", l201),
                    r.ESayC("", l202),
                    s.ESayC("happy", l203),
                    m.ESayC("surprise", l204),
                    s.ESayC("", l205),
                    m.ESayC("angry", l206),
                    r.ESayC("smug", l207)
                );
                using var _ = evidenceRequest.Request(remi1Interrupt);
                await vn.Sequential(
                    r.SayC(l208),
                    m.ESayC("worry", l209)
                );
            });
            var remi2 = Context("remi2", async () => {
                await VN.Find<Sakuya>().ESayC("", l232);
                await Marisa.ESayC("worry", l233);
            });
            var byaku0 = Context("byakuren0", async () => {
                var b = VN.Find<Byakuren>();
                var m = Marisa;
                await vn.Sequential(
                    m.ESayC("surprise", l341),
                    b.ESayC("", l342),
                    m.ESayC("cry", l343),
                    b.ESayC("worry", l344),
                    b.SayC(l345),
                    m.ESayC("surprise", l346),
                    b.SayC(l347),
                    b.ESayC("angry", l348),
                    b.SayC(l349),
                    b.SayC(l350),
                    b.ESayC("", l351),
                    m.ESayC("angry", l352),
                    b.ESayC("happy", l353),
                    m.SayC(l354),
                    b.ESayC("angry", l355),
                    m.ESayC("cry", l356),
                    b.ESayC("", l358),
                    m.ESayC("angry", l360)
                );
                UpdateDataV(d => {
                    d.EvByakuren.Enabled = true;
                    d.State = d.State with { Byakuren = ByakurenState.L1_REQ };
                });
            });
            
            BoundedContext<InterruptionStatus> byaku1Interrupt(Evidence ev) => new(VN, "", async () => {
                var b = VN.Find<Byakuren>();
                var m = Marisa;
                if (ev is Evidence.Yuyuko or Evidence.Youmu) {
                    //We could use state change to spawn these via assertions, but since there are two it's a bit annoying to create an extra intermediary state. Instead, we'll create them manually and assert on DelayedState
                    //Instead of `using` (which disposes on BCTX finish), use `DisposeWithMap` so it aligns with DelayedState
                    var s = DisposeWithMap(VN.Add(new Murasa()));
                    var n = DisposeWithMap(VN.Add(new Nue()));
                    s.SortingID.Value = -100;
                    n.SortingID.Value = -50;
                    s.Alpha = n.Alpha = 0;
                    s.LocalLocation.Value = V3(6, 4);
                    n.LocalLocation.Value = V3(0, 2);
                    n.Emote.Value = "happy";
                    await vn.Sequential(
                        m.ESayC("angry", l371),
                        b.ESayC("worry", l372),
                        b.SayC(l373),
                        m.SayC(l374),
                        n.SayC(l375, flags: SpeakFlags.Anonymous),
                        VN.SFX("vn-yukari-power"),
                        n.MoveBy(V3(0, -2), 0.8f).And(n.FadeTo(1, 0.7f)).Then(n.Say(l377)).C,
                        n.SayC(l378),
                        n.ESayC("", l379),
                        n.ESayC("happy", l380),
                        m.ESayC("worry", l381),
                        n.ESayC("", l382),
                        m.ESayC("angry", l383),
                        n.ESayC("smug", l384),
                        m.SayC(l385),
                        b.ESayC("worry", l386),
                        s.MoveBy(V3(0, -4), 1.7f, Easers.EOutBack).And(
                            s.FadeTo(1, 1f),
                            vn.Wait(0.4f).Then(
                                s.Say(l388).And(VN.SFX("vn-suzunaan-bell").AsVnOp(VN)))).C,
                        b.ESayC("", l389),
                        s.SayC(l390),
                        s.ESayC("happy", l391),
                        m.ESayC("worry", l392),
                        s.ESayC("", l393),
                        s.ESayC("worry", l394),
                        s.ESayC("", l395),
                        b.ESayC("", l396),
                        s.SayC(l397),
                        s.SayC(l398),
                        s.SayC(l399),
                        s.ESayC("worry", l400),
                        n.ESayC("happy", l401),
                        n.ESayC("smug", l402),
                        n.ESayC("", l403),
                        s.ESayC("happy", l404),
                        s.ESayC("", l405),
                        s.SayC(l406),
                        s.ESayC("worry", l407),
                        s.ESayC("", l408),
                        s.ESayC("happy", l409),
                        n.ESayC("happy", l410),
                        b.ESayC("happy", l411),
                        b.ESayC("", l412),
                        b.SayC(l413),
                        b.ESayC("angry", l414),
                        s.ESayC("happy", l415),
                        m.ESayC("angry", l416),
                        b.ESayC("", l418)
                    );
                    UpdateDataV(d => {
                        d.EvMurasa.Enabled = true;
                        d.EvNue.Enabled = true;
                        d.State = d.State with { Byakuren = ByakurenState.L2 };
                    });
                } else if (ev is Evidence.Mima) {
                    await b.ESayC("worry", l367);
                } else {
                    await b.ESayC("angry", l368);
                    await m.ESayC("cry", l369);
                }
                return InterruptionStatus.Abort;
            });
            var byaku1 = Context("", async () => {
                var b = VN.Find<Byakuren>();
                var m = Marisa;
                await vn.Sequential(
                    b.ESayC("angry", l362),
                    m.ESayC("cry", l363));
                using var _ = evidenceRequest.Request(byaku1Interrupt);
                await vn.Sequential(
                    b.ESayC("", l364),
                    b.ESayC("", l365),
                    m.ESayC("angry", l366)
                );
            });
            var byaku2 = Context("byaku2", async () => {
                var b = VN.Find<Byakuren>();
                var s = VN.Find<Murasa>();
                await vn.Sequential(
                    b.ESayC("", l412),
                    b.SayC(l413),
                    b.ESayC("angry", l414),
                    s.ESayC("happy", l415),
                    Marisa.ESayC("angry", l416),
                    b.ESayC("", l418)
                );
            });
            
            var ghost1 = Context("ghost1", async () => {
                var g = VN.Find<Marisa98>();
                await vn.Sequential(
                    g.SayC(l235, flags: SpeakFlags.Anonymous),
                    Marisa.ESayC("surprise", l236),
                    g.SayC(l237, flags: SpeakFlags.Anonymous),
                    Marisa.ESayC("cry", l238),
                    g.SayC(l239, flags: SpeakFlags.Anonymous)
                );
            });
            var ghost2 = Context("ghost2", async () => {
                var g = VN.Find<Marisa98>();
                var m = Marisa;
                using var mi = VN.Add(new Mima());
                mi.Alpha = 0;

                await vn.Sequential(
                    g.SayC(l241, flags: SpeakFlags.Anonymous),
                    Marisa.ESayC("angry", l242),
                    g.SayC(l243, flags: SpeakFlags.Anonymous),
                    g.Say(l244, flags: SpeakFlags.Anonymous)
                );
                var ev = await evidenceRequest.WaitForEvidence("gev1");
                if (ev is not Evidence.Yuyuko { Status: >= Evidence.Yuyuko.Level.L1 })
                    goto fail;
                await vn.Sequential(
                    m.ESayC("angry", l246),
                    g.SayC(l247, flags: SpeakFlags.Anonymous),
                    g.Say(l248, flags: SpeakFlags.Anonymous)
                );
                ev = await evidenceRequest.WaitForEvidence("gev2");
                if (ev is not (Evidence.Nue or Evidence.Byakuren))
                    goto fail;
                await vn.Sequential(
                    m.ESayC("angry", l250),
                    g.SayC(l251, flags: SpeakFlags.Anonymous),
                    g.Say(l252, flags: SpeakFlags.Anonymous)
                );
                ev = await evidenceRequest.WaitForEvidence("gev3");
                if (ev is Evidence.Mima) {
                    mi.LocalLocation.Value = V3(-3, -3);
                    await vn.Sequential(
                        m.EmoteSay("surprise", l254).And(g.FadeTo(0, 1f)).C,
                        mi.SayC(l255, flags: SpeakFlags.Anonymous),
                        m.EmoteSay("worry", l256).And(vn.Wait(0.4f).Then(m.MoveBy(V3(-1, -2), 1.4f))).C,
                        m.EmoteSay("", l257).And(m.MoveBy(V3(1, 2), 1.6f)).C,
                        mi.EmoteSay("cry", l258).And(mi.FadeTo(1, 1), mi.MoveBy(V3(0, 3), 1)).C,
                        mi.ESayC("worry", l259),
                        mi.RotateTo(V3(0, 180), 0.7f).Then(vn.Wait(0.2f)).Then(mi.RotateTo(V3(0, 0), 0.8f).And(mi.Say(l260))).C,
                        m.ESayC("surprise", l261),
                        mi.ESayC("surprise", l262),
                        m.ESayC("angry", l263),
                        mi.ESayC("surprise", l264),
                        m.ESayC("worry", l265),
                        mi.ESayC("worry", l266),
                        m.ESayC("surprise", l267),
                        mi.SayC(l268),
                        m.ESayC("cry", l269),
                        mi.ESayC("worry", l270),
                        m.SayC(l271),
                        mi.ESayC("", l272),
                        mi.ESayC("happy", l273),
                        m.ESayC("surprise", l274),
                        mi.ESayC("", l275),
                        m.ESayC("worry", l276),
                        m.ESayC("angry", l277),
                        mi.ESayC("happy", l278),
                        mi.ESayC("worry", l279),
                        mi.EmoteSay("happy", l280).And(mi.MoveBy(V3(-2, 0), 0.8f), mi.FadeTo(0, 0.8f)).C,
                        new LazyAction(() => {
                            mi!.LocalLocation.Value = V3(-3, 6);
                            mi!.EulerAnglesD.Value = V3(0, 0, 180);
                        }),
                        mi.MoveBy(V3(0, -2), 1f).And(mi.FadeTo(1, 1), mi.EmoteSay("happy", l281)).C,
                        m.ESayC("angry", l282),
                        mi.ESayC("cry", l283),
                        mi.ESayC("", l284),
                        mi.Say(l285).And(mi.MoveBy(V3(-0.8, 0.4), 0.5f)).C,
                        mi.Say(l286).And(mi.MoveBy(V3(-0.8, 0.4), 0.5f)).C,
                        mi.Say(l287).And(mi.MoveBy(V3(-0.8, 0.4), 0.5f)).C,
                        mi.Say(l288).And(mi.MoveBy(V3(0, 2), 0.5f), mi.FadeTo(0, 0.5f)).C
                    );
                } else if (ev is Evidence.Marisa) {
                    mi.LocalLocation.Value = V3(-6, 0);
                    mi.Emote.Value = "angry";
                    await vn.Sequential(
                        g.SetEmote("smug"),
                        g.TintTo(FColor.White, 1f).And(m.EmoteSay("surprise", l290)).C,
                        g.SayC(l291),
                        g.SayC(l292),
                        g.SayC(l293),
                        m.ESayC("cry", l294),
                        m.EmoteSay("angry", l295).And(m.Disturb(m.ComputedLocation, JumpY(1f), 0.6f)).C,
                        g.ESayC("angry", l296),
                        g.ESayC("smug", l297),
                        g.SayC(l298),
                        g.SayC(l299),
                        g.ESayC("happy", l300),
                        g.ESayC("angry", l301),
                        g.ESayC("smug", l302),
                        m.ESayC("cry", l303),
                        m.SayC(l304),
                        g.SayC(l305),
                        g.SayC(l306),
                        g.ESayC("happy", l307),
                        g.SayC(l308),
                        g.ESayC("smug", l309),
                        g.ESayC("", l310),
                        g.EmoteSay("smug", l311).And(g.MoveBy(V3(1f, 0), 0.8f)).C,
                        m.EmoteSay("surprise", l312).And(m.MoveBy(V3(0.4f, 0), 0.5f)).C,
                        g.Say(l313).And(g.MoveBy(V3(1.2f, 0), 1f)).C,
                        m.EmoteSay("cry", l314).And(m.MoveBy(V3(0.2f, 0), 0.5f)).C,
                        mi.SayC(l315, flags: SpeakFlags.Anonymous),
                        g.EmoteSay("surprise", l316).And(
                            g.RotateTo(V3(0, 180), 0.64f),
                            mi.FadeTo(1, 1f),
                            mi.MoveBy(V3(2, 0), 1.1f)
                        ).C,
                        mi.Say(l317).And(
                            mi.Disturb(mi.ComputedLocation, JumpX(2), 0.7f),
                            mi.Disturb(mi.ComputedLocation, JumpY(0.4f), 0.64f),
                            vn.Wait(0.3f).Then(g.MoveBy(V3(24, 16), 2.2f, Easers.EOutSine).And(
                                vn.SFX("vn-impact-1").AsVnOp(VN),
                                g.RotateTo(V3(0, 0, 900), 2.1f),
                                g.ScaleTo(V3(0.2, 0.2), 1f)))
                            ).C,
                        mi.SayC(l318),
                        m.ESayC("surprise", l319),
                        mi.ESayC("worry", l320),
                        mi.ESayC("happy", l321),
                        m.ESayC("worry", l322),
                        mi.ESayC("", l323),
                        m.ESayC("angry", l324),
                        mi.ESayC("surprise", l325),
                        mi.ESayC("happy", l326),
                        mi.SayC(l327),
                        mi.ESayC("", l328),
                        mi.ESayC("happy", l329),
                        mi.SayC(l330),
                        mi.ESayC("", l331),
                        mi.EmoteSay("happy", l332).And(mi.MoveBy(V3(-3, 0), 1).And(mi.FadeTo(0, 1))).C,
                        m.ESayC("cry", l333)
                    );
                } else
                    goto fail;
                

                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 2f));
                completion.SetResult(new UnitADVCompletion());
                return;
                
                fail: ;
                await vn.Sequential(
                    g.SayC(l335, flags: SpeakFlags.Anonymous),
                    g.SayC(l336, flags: SpeakFlags.Anonymous),
                    g.SayC(l337, flags: SpeakFlags.Anonymous),
                    new LazyAction(HideMD),
                    rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1.5f)),
                    rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1.5f))
                );
            });
            
            void ConfigureUniversal(string mapName, GOTPIdealizedState i, GOTPADVData d) {
                foreach (var map in maps) {
                    var isCurr = map.key == mapName;
                    i.Assert(new InteractableAssertion(m, _ => GoToMapUI(map.key), $"go to {map.key}") {
                        Location = V3(7.3f, 1.8f + map.mapLinkOffset),
                        Info = new InteractableInfo.Map(isCurr),
                        Hover = new Interactable.HoverAction.VNOp(() => 
                            VN.Find<Marisa>().Say(isCurr ? 
                                $"I'm currently at {map.desc(d)}." :
                                $"Should I head over to {map.desc(d)}?"))
                    });
                }
            }
            
            ms.ConfigureMap(MarisaHouse, (i, d) => {
                ConfigureUniversal(MarisaHouse, i, d);
                i.Assert(new EntityAssertion<NormalBedroomBG>(VN));
                i.Assert(new BGMAssertion(VN, "gotp.house"));
                i.Assert(new CharacterAssertion<Marisa98>(VN) {
                    Location = V3(-3, 0),
                    Tint = Color.black._()
                }.WithChildren(new InteractableBCtxAssertion(m, 
                    d.State.Byakuren >= ByakurenState.L2 && d.State.Remilia >= RemiliaState.L1 && d.State.Yuyuko >= YuyukoState.L1 ? ghost2 : ghost1
                ) {Location = Marisa98.SpeakIconOffset }));
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = V3(3, 0)
                });
                if (d.State.IsStarting)
                    i.SetEntryVN(s0main);
            });
            ms.ConfigureMap(SDM, (i, d) => {
                ConfigureUniversal(SDM, i, d);
                if (d.State.IsStarting)
                    i.Assert(new CharacterAssertion<Flandre>(VN) {
                        Location = V3(-1.5, 0)
                    });
                i.Assert(new EntityAssertion<LibraryBG>(VN));
                i.Assert(new BGMAssertion(VN, "gotp.sdm"));
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = d.State.IsStarting ? V3(-13, 0) : V3(-5, 0)
                }, new CharacterAssertion<Sakuya>(VN) {
                    Location = V3(5, 0)
                }, new CharacterAssertion<Remilia>(VN) {
                    Location = V3(2, 0),
                }.WithChildren(new InteractableBCtxAssertion(m, d.State.Remilia switch {
                        RemiliaState.L0_REQ => remi1,
                        _ => remi2
                    }
                ) { Location = Remilia.SpeakIconOffset }));
            });
            ms.ConfigureMap(HG, (i, d) => {
                ConfigureUniversal(HG, i, d);
                i.Assert(new EntityAssertion<HakugyokurouBG>(VN));
                i.Assert(new BGMAssertion(VN, "gotp.hg"));
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = V3(5, 0),
                    Emote = d.State.IsStarting ? "cry" : null
                }, new CharacterAssertion<Youmu>(VN) {
                    Location = V3(-0.5f, 0)
                }.WithChildren(d.State.Yuyuko >= YuyukoState.L1 ? null : 
                    new InteractableBCtxAssertion(m, yuyu1) { Location = Youmu.SpeakIconOffset }));
                if (!d.State.IsStarting)
                    i.Assert(new CharacterAssertion<Yuyuko>(VN) {
                        Location = V3(-4, 0)
                    }.WithChildren(d.State.Yuyuko < YuyukoState.L1 ? null : 
                        new InteractableBCtxAssertion(m, yuyu2) { Location = Yuyuko.SpeakIconOffset }));
            });
            ms.ConfigureMap(Myouren, (i, d) => {
                ConfigureUniversal(Myouren, i, d);
                i.Assert(new EntityAssertion<Shrine2BG>(VN));
                i.Assert(new BGMAssertion(VN, "gotp.myouren"));
                if (d.DelayedState.Byakuren >= ByakurenState.L2) {
                    i.Assert(new CharacterAssertion<Murasa>(VN) {
                        Location = V3(6, 0)
                    }, new CharacterAssertion<Nue>(VN) {
                        Location = V3(0, 0)
                    });
                }
                i.Assert(new CharacterAssertion<Marisa>(VN) {
                    Location = V3(-5, 0)
                }, new CharacterAssertion<Byakuren>(VN) {
                    Location = V3(3, 0)
                }.WithChildren(new InteractableBCtxAssertion(m, d.State.Byakuren switch {
                    ByakurenState.L2 => byaku2,
                    ByakurenState.L1_REQ => byaku1,
                    _ => byaku0
                }) { Location = Byakuren.SpeakIconOffset }));
            });
            return ms;
        }
        
        public record GOTPIdealizedState(Executing e) : ADVIdealizedState(e) {
            protected override Task FadeIn(ActualizeOptions options) {
                return e.rgb.DoTransition(new RenderGroupTransition.Fade(e.rg, 0.7f)).Task;
            }
            protected override Task FadeOut(ActualizeOptions options) {
                return e.rg.DoTransition(new RenderGroupTransition.Fade(e.rgb, 0.7f)).Task;
            }
        }
    }

    [Serializable]
    public record GOTPADVData: ADVData {
        public State State = new();
        public State DelayedState = new();
        public bool EvidenceButtonVisible;
        public Evidence.Marisa EvMarisa { get; init; } = new() { Enabled = true };
        public Evidence.Mima EvMima { get; init; } = new() { Enabled = true };
        public Evidence.Remilia EvRemilia { get; init; } = new() { Enabled = true };
        public Evidence.Flandre EvFlandre { get; init; } = new() { Enabled = true };
        public Evidence.Sakuya EvSakuya { get; init; } = new() { Enabled = true };
        public Evidence.Yuyuko EvYuyuko { get; init; } = new() { Enabled = true };
        public Evidence.Youmu EvYoumu { get; init; } = new() { Enabled = true };
        public Evidence.Byakuren EvByakuren { get; init; } = new();
        public Evidence.Nue EvNue { get; init; } = new();
        public Evidence.Murasa EvMurasa { get; init; } = new();

        [JsonIgnore] public Evidence[] Evidences = null!;

        public GOTPADVData(InstanceData VNData) : base(VNData) {
            SnapshotReferences();
        }

        [OnDeserialized]
        internal void _OnDeserialized(StreamingContext _) => SnapshotReferences();

        private void SnapshotReferences() {
            Evidences = new Evidence[]
                { EvMarisa, EvMima, EvRemilia, EvFlandre, EvSakuya, EvYuyuko, EvYoumu, EvByakuren, EvNue, EvMurasa };
        }
    }

    [Serializable]
    public abstract record Evidence {
        public bool Enabled { get; set; } = false;
        [JsonIgnore]
        public abstract LString Title { get; }
        [JsonIgnore]
        public abstract LString Description { get; }

        public record Mima : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.mima");
            public override LString Description =>
                "Why is Mima even here in my evidence list?";
        }

        public record Marisa : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.marisa");
            public override LString Description =>
                "Hi, I'm Marisa. I'm being haunted by a ghost. Normally this doesn't happen to me, so I'm a bit freaked out about it.";
        }

        public record Youmu : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.youmu");
            public override LString Description =>
                "Youmu is the half-human half-ghost gardener of Hakugyokurou.\nSupposedly, her sword can cut through ghosts, so if I can provide her more information on what type of ghost is haunting me, she might be able to help me.";
        }

        public record Yuyuko(Yuyuko.Level Status = Yuyuko.Level.L0) : Evidence {
            public enum Level {
                L0 = 0,
                L1 = 1
            }
            public Level Status { get; set; } = Status;
            public override LString Title => LocalizedStrings.FindReference("dialogue.yuyuko");
            private const string baseDescr =
                "Yuyuko is the master of Hakugyokurou. She is a so-called \"true ghost\".";
            public override LString Description => Status switch {
                Level.L1 => baseDescr +
                            "\nYuyuko told me that the ghost haunting me is a ghost from my past. That really narrows it down— I should have enough evidence to confront the ghost now.",
                _ => baseDescr
            };
        }
        public record Sakuya(Sakuya.Level Status = Sakuya.Level.L0) : Evidence {
            public enum Level {
                L0 = 0,
                L1 = 1
            }
            public Level Status { get; set; } = Status;
            public override LString Title => LocalizedStrings.FindReference("dialogue.sakuya");
            private const string baseDescr = "Sakuya is the maid at the Scarlet Devil Mansion. She is human, supposedly.";
            public override LString Description => Status switch {
                Level.L1 => baseDescr +
                                     "\nSakuya told me that the ghost afflicting me is something internal." +
                                     " I suppose an \"internal ghost\" counts as a type of ghost?",
                _ => baseDescr
            };
        }

        public record Remilia : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.remilia");
            public override LString Description =>
                "Remilia is the master of the Scarlet Devil Mansion. She is a vampire.";
        }

        public record Flandre : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.flandre");
            public override LString Description =>
                "Flandre is Remilia's younger sister. She is a vampire, but she has no taste for ghost stories at all.";
        }
        
        
        public record Byakuren : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.byakuren");
            public override LString Description =>
                "Byakuren is the head priest of the Myouren Temple. She insists that the ghost haunting me isn't real and that I'm just imagining it. Easy for her to say!";
        }
        
        public record Nue : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.nue");
            public override LString Description =>
                "Nue is a nue who hangs out at the Myouren Temple. She says that we see ghosts when we can't understand what we see. Though I can't help but feel like that's a bit excessively metaphorical of a description.";
        }
        
        public record Murasa : Evidence {
            public override LString Title => LocalizedStrings.FindReference("dialogue.murasa");
            public override LString Description =>
                "Murasa is a ship-sinking youkai from the Myouren Temple. When she was younger, she went around and sunk a lot of ships, so many sailors considered her to be a ghost of the seas.";
        }


    }

    //must be record-typed for DelayedState to work
    public record State(bool IsStarting = true, YuyukoState Yuyuko = YuyukoState.L0_REQ,
        RemiliaState Remilia = RemiliaState.L0_REQ, ByakurenState Byakuren = ByakurenState.L0);

    public enum YuyukoState {
        L0_REQ, L1
    }

    public enum RemiliaState {
        L0_REQ, L1
    }
    public enum ByakurenState {
        L0, L1_REQ, L2
    }
    
    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.ADVData.CurrentMap == "")
            throw new Exception("GOTP was loaded with no current map.");
        Logs.Log("Starting GOTP execution...");
        return new Executing(this, inst);
    }

    public override ADVData NewGameData() => new GOTPADVData(new(SaveData.r.GlobalVNData)) {
        CurrentMap = Executing.MarisaHouse
    };
}
}