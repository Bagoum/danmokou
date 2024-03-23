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
using static MiniProjects.VN.ScriptLocalization.WP;

namespace MiniProjects.VN.WP {

[CreateAssetMenu(menuName = "Data/ADV/WP Game")]
public class WPGameDef : ADVGameDef {
    public Sprite evidenceReviewBg = null!;
    
    private class Executing : DMKExecutingADV<Executing.WPIdealizedState, WPADVData> {
        private readonly WPGameDef gdef;
        private readonly SelectionRequest<string> selector;
        private readonly EvidenceTargetProxy<Evidence, Target> targetEvReq;
        private readonly UIScreen targetScreen;
        private readonly IFixedXMLObjectContainer evTargets;
        private readonly EvidenceRequest<Evidence> evidenceRequest;
        private bool CanEvidence => targetEvReq.Request.CanPresentAny || evidenceRequest.CanPresent;
        private bool CanSpecificEvidence => targetEvReq.Request.CanPresent || evidenceRequest.CanPresent;


        //--- Lerpers
        private readonly PushLerper<float> evSize = new(0.5f, (a, b, t) => BMath.LerpU(a, b, Easers.EOutBack(t)));


        public Executing(WPGameDef gdef, ADVInstance inst) : base(inst) {
            this.gdef = gdef;
            selector = SetupSelector();
            targetEvReq = new(new(Manager, VN));
            evidenceRequest = new(VN, Manager);
            evSize.Push(1f);
            
            tokens.Add(MapWillUpdate.Subscribe(_ => {
                Data.DelayedState = Data.State;
            }));

            (targetScreen, evTargets) = menu.MakeScreen(_ => null);
            var targetInfo = new UINode("Select a target to present evidence to") {
                Prefab = XMLUtils.Prefabs.PureTextNode,
                Passthrough = true,
                OnBuilt = n => {
                    var l = n.Label!;
                    l.style.backgroundColor = new Color(0.42f, 0.08f, 0.47f, 0.7f);
                    l.SetPadding(44, 64, 44, 64);
                }
            };
            targetInfo.ConfigureAbsoluteLocation(new FixedXMLObject(1920, 200, null, null), XMLUtils.Pivot.Center,
                useVisiblityPassthrough:false);
            evTargets.AddNodeDynamic(targetInfo);
            
            //The actual evidence window needs to be set up in ADVDataFinalized since it depends on Data.Evidences
        }

        /// <inheritdoc/>
        public override void ADVDataFinalized() {
            var evidenceScreen = new UIScreen(menu, "EVIDENCE", UIScreen.Display.Basic) { 
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
                Data.Evidences.Select(ev => new UINode(() => 
                    ev.Enabled ? ev.Title : "---") {
                    ShowHideGroup = new UIColumn(evInfo, 
                        new UINode(() => ev.Enabled ? ev.Description : "I don't have any evidence to put here.") 
                            { Prefab = XMLUtils.Prefabs.PureTextNode, Passthrough = true, InlineStyle = (_, n) => n.Style.minHeight = 500 }
                            .With(XMLUtils.fontBiolinumClass, XMLUtils.small1Class),
                        new UIButton("Use Evidence", UIButton.ButtonType.Confirm, 
                            _ => {
                                if (targetEvReq.Request.CanPresentAny) {
                                    targetEvReq.NextEvidence = ev;
                                    return new UIResult.GoToScreen(targetScreen);
                                } else {
                                    var __ = evidenceRequest.Present(ev).ContinueWithSync();
                                    return new UIResult.ReturnToScreenCaller();
                                }
                            }) { 
                                VisibleIf = () => ev.Enabled && CanEvidence
                        })
            }));
            evidenceScreen.SetFirst(evs);
            menu.AddScreen(evidenceScreen);
            //Since this button is always present, we don't need to bother
            // going through Assertions for it
            // (Though we don't yet have an Assertion for generic UITK objects, only one for icon-based interactables)
            var toEvidenceButton = new UIButton(() => CanSpecificEvidence ? "Show Evidence" : "Evidence", 
                UIButton.ButtonType.Confirm, _ => new UIResult.GoToScreen(evidenceScreen, menu.Unselect)) {
                OnBuilt = n => {
                    var l = n.Label!;
                    l.style.backgroundColor = Color.clear;
                    l.style.backgroundImage = new StyleBackground(gdef.evidenceReviewBg);
                    l.SetPadding(54, 64, 54, 64);
                    tokens.Add(evSize.Subscribe(s => n.HTML.transform.scale = new UnityEngine.Vector3(s, s, 1)));
                },
            };
            toEvidenceButton.ConfigureAbsoluteLocation(new FixedXMLObject(120, 90, null, null), XMLUtils.Pivot.TopLeft);
            menu.AddNodeDynamic(toEvidenceButton);
            void UpdateEvidenceButton(Unit _) {
                evSize.PushIfNotSame(CanSpecificEvidence ? 1.2f : 1f);
                toEvidenceButton.RedrawIfBuilt();
            }
            tokens.Add(targetEvReq.Request.RequestsChanged.Subscribe(UpdateEvidenceButton));
            tokens.Add(evidenceRequest.RequestsChanged.Subscribe(UpdateEvidenceButton));
        }

        public override void RegularUpdate() {
            base.RegularUpdate();
            evSize.Update(ETime.FRAME_TIME);
        }

        record MapData(string key, Func<WPADVData, string> desc, float mapLinkOffset);

        public const string House = "myhouse";
        private const string SnowMF = "snow_fairies";
        private const string SnowCirno = "snow_cirno";
        private const string SnowHouse = "snowhouse";
        private const string SnowHill = "snowhill";
        
        private readonly MapData[] maps = {
            new(House, _ => "my house", 2),
            new(SnowMF, _ => "my backyard", 1),
            new(SnowCirno, _ => "my lawn", 0),
            new(SnowHouse, _ => "my rental property", -1),
            new(SnowHill, _ => "my golf course", -2)
        };
        private SZYUCharacter R => VN.Find<Reimu>();

        private async Task<IDisposable> MakeObjection(bool useBgm = true) {
            var bgm = useBgm ? vn.RunBGMFast("s01-2") : null;
            var sc = vn.Add(new ScreenColor());
            var obj = vn.Add(new Objection());
            sc.RenderLayer.Value = obj.RenderLayer.Value = SortingLayer.NameToID("UI");
            sc.Alpha = 0;
            obj.Alpha = 0;
            obj.LocalLocation.Value = V3(0, 1.4);
            obj.Scale.Value = V3(6f);
            
            await vn.SFX("vn-objection");
            await obj.ScaleTo(V3(1.5f), 0.25f, Easers.EOutSine).And(
                vn.Wait(0.05f).Then(obj.FadeTo(1, 0.2f, Easers.EOutSine)),
                sc.FadeTo(0.5f, 0.22f, Easers.EOutSine)
            );
            await sc.FadeTo(0, 0.5f, Easers.EInSine);
            await vn.Wait(0.5f);
            _ = vn.Wait(0.3f).Then(
                    obj.FadeTo(0, 1.4f).And(
                        obj.MoveBy(V3(0, -2), 1.4f),
                        obj.ScaleTo(V3(1), 1.4f))).Task.ContinueWithSync();
            return new JointDisposable(null, bgm, obj, sc);
        }
        
        protected override MapStateManager<WPIdealizedState, WPADVData> ConfigureMapStates() {
            var m = Manager;
            var ms = new MapStateManager<WPIdealizedState, WPADVData>(this, () => new(this));
            /*
            StrongBoundedContext<InterruptionStatus> interrupt(Evidence ev) => new(VN, "interrupt0", async () => {
                await vn.Sequential(
                    Mr.SayC($"You presented {ev} (internal)"),
                    R.SayC("Second line")
                );
                return InterruptionStatus.Continue;
            }) { LoadSafe = false };;*/
            
            var s0 = Context("s0", async () => {
                using var u = vn.Add(new Unknown());
                await vn.Sequential(
                    R.MoveTo(V3(6, 0), 1.2f, Easers.EOutSine).And(
                        R.EmoteSay("happy", l1)).C,
                    R.Disturb(R.ComputedLocation, JumpY(0.7f), 0.6f).And(R.Say(l2)).C,
                    R.SayC(l3),
                    R.ESayC("angry", l4),
                    u.SayC(l5),
                    R.ESayC("surprise", l6),
                    R.ESayC("worry", l7),
                    u.SayC(l8),
                    R.SayC(l9),
                    R.ESayC("angry", l10),
                    u.SayC(l11),
                    R.SayC(l12),
                    R.SayC(l13),
                    R.SayC(l14),
                    R.ESayC("worry", "...But first, I have to open the door and go outside.")
                    );
            });
            var chestFail = Context("chestFail", async () => {
                await R.ESayC("normal", l19);
            });
            var pickupKey = Context("pickupKey", async () => {
                await R.ESayC("happy", l18);
                UpdateDataV(d => d.EvKey.Enabled = true);
            });
            var doorFail = Context("doorFail", async () => {
                await vn.Sequential(
                    R.ESayC("worry", l15),
                    R.ESayC("angry", l16),
                    R.SayC(l17)
                );
            });
            StrongBoundedContext<InterruptionStatus> cirnoInterruptF = new(VN, "cirno_intf", async () => {
                await R.ESayC("angry", l93);
                await vn.Find<Cirno>().ESayC("angry", l94);
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            StrongBoundedContext<InterruptionStatus> cirnoInterrupt2(Evidence ev) => new(VN, "cirno_int2", async () => {
                var c = vn.Find<Cirno>();
                if (ev is Evidence.MunicipalLaw) {
                    using (var _ = await MakeObjection()) {
                        await vn.Sequential(
                            R.ESayC("angry", l104),
                            c.ESayC("worry", l105),
                            R.SayC(l106),
                            c.SayC(l107),
                            R.ESayC("satisfied", l108),
                            c.EmoteSay("surprise", l109).And(c.MoveBy(V3(-12, 0), 2, Easers.EInBack)).C);
                    }
                    await vn.Sequential(
                        R.ESayC("happy", l110),
                        R.ESayC("worry", l111),
                        R.ESayC("satisfied", l112),
                        vn.SFX("extend-any"),
                        R.SayC(l113, flags: SpeakFlags.Anonymous)
                    );
                    UpdateDataV(d => {
                        d.EvIceBlock.Enabled = true;
                        d.State = d.State with { RemovedCirno = true };
                    });
                } else await cirnoInterruptF;
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            StrongBoundedContext<InterruptionStatus> cirnoInterrupt(Evidence ev) => new(VN, "cirno_int", async () => {
                var c = vn.Find<Cirno>();
                if (ev is Evidence.Snowball) {
                    using var _ = evidenceRequest.Request(_ => cirnoInterruptF);
                    await vn.Sequential(
                        R.ESayC("angry", l95),
                        vn.SFX("vn-gun"),
                        R.SayC(l96, flags: SpeakFlags.Anonymous),
                        c.ESayC("happy", l97));
                    using var __ = evidenceRequest.Request(cirnoInterrupt2);
                    await vn.Sequential(
                        vn.SFX("vn-gun"),
                        c.SayC(l98, flags: SpeakFlags.Anonymous),
                        R.SayC(l99),
                        c.SayC(l100),
                        R.SayC(l99),
                        c.ESayC("worry", l100),
                        R.SayC(l99),
                        c.SayC(l101),
                        R.SayC(l102),
                        c.ESayC("cry", l103)
                    );
                } else await cirnoInterruptF;
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            StrongBoundedContext<InterruptionStatus> tokikoIntF(Evidence ev) => new(VN, "tokiko_intf", async () => {
                var c = vn.Find<Tokiko>();
                if (ev is Evidence.OSHA) {
                    await vn.Sequential(
                        R.ESayC("worry", "Hmm, this would work if she was actually violating regulations, but right" +
                                         " now she's holding the ladder correctly."),
                        R.SayC("If I can get her to let go of the ladder or turn around, then I should immediately" +
                               " use this evidence in the middle of our conversation.")
                    );
                } else {
                    await vn.Sequential(
                        c.ESayC("worry", "Okay, one more rung..."),
                        R.ESayC("angry", "She's not even listening!")
                    );
                }
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            StrongBoundedContext<InterruptionStatus> tokikoInt(Evidence ev) => new(VN, "tokiko_int", async () => {
                if (ev is Evidence.OSHA) {
                    var c = vn.Find<Tokiko>();
                    using (var _ = await MakeObjection()) {
                        await vn.Sequential(
                            R.ESayC("angry", l72),
                            c.ESayC("surprise", l73),
                            R.SayC(l74),
                            R.ESayC("satisfied", l75),
                            c.ESayC("cry", l76),
                            R.ESayC("angry", l77),
                            c.Say(l78).And(
                                c.MoveBy(V3(-12, 4), 3),
                                c.Disturb(c.ComputedLocation, JumpY(-1), 1.4f),
                                vn.SFX("vn-birdflap").AsVnOp(vn).Then(
                                    vn.Wait(0.8f),
                                    vn.SFX("vn-birdflap").AsVnOp(vn),
                                    vn.Wait(0.7f),
                                    vn.SFX("vn-birdflap").AsVnOp(vn),
                                    vn.Wait(0.6f),
                                    vn.SFX("vn-birdflap").AsVnOp(vn))
                            ).C);
                    }
                    await vn.Sequential(
                        R.ESayC("", l79),
                        R.ESayC("surprise", l80),
                        R.ESayC("angry", l81)
                    );
                    UpdateDataV(d => d.State = d.State with { RemovedTokiko = true });
                    return InterruptionStatus.Abort;
                } else return await tokikoIntF(ev);
            }) { LoadSafe = false };
            var cirno = Context("cirno", async () => {
                var c = vn.Find<Cirno>();
                var r = R;
                using var _ = evidenceRequest.Request(cirnoInterrupt);
                await vn.Sequential(
                    c.ESayC("cry", l83),
                    r.ESayC("worry", l84),
                    c.ESayC("happy", l85),
                    c.ESayC("worry", l86),
                    r.ESayC("surprise", l87),
                    r.ESayC("worry", l88),
                    c.SayC(l89),
                    r.SayC(l90),
                    c.SayC(l91),
                    r.ESayC("angry", l92)
                );
            });
            var tkbook1 = Context("tkbook1", async () => {
                await R.ESayC("worry", l28);
            });
            var tkbook2 = Context("tkbook2", async () => {
                await vn.Sequential(
                    R.ESayC("happy", l29),
                    R.ESayC("surprise", l30),
                    R.ESayC("", l31),
                    R.ESayC("worry", l32),
                    vn.SFX("extend-any"),
                    R.SayC(l33, flags:SpeakFlags.Anonymous)
                );
                UpdateDataV(d => d.EvLaw.Enabled = true);
            });
            var tokiko = Context("tokiko", async () => {
                var t = vn.Find<Tokiko>();
                await vn.Sequential(
                    R.ESayC("", l34),
                    t.ESayC("worry", l35),
                    R.ESayC("angry", l36),
                    t.SayC(l37),
                    R.Say(l38)
                );
                var (i, _) = await selector.WaitForSelection("s1", l40, l57);
                using var _ = evidenceRequest.Request(tokikoIntF);
                if (i == 0) {
                    await vn.Sequential(
                        R.SayC(l41),
                        t.ESayC("surprise", l42),
                        R.SayC(l43),
                        t.ESayC("happy", l44),
                        t.ESayC("worry", l45),
                        R.ESayC("worry", l46),
                        t.SayC(l47),
                        R.SayC(l48),
                        R.SayC(l49),
                        t.ESayC("happy", l50),
                        R.ESayC("angry", l51),
                        t.ESayC("cry", l52),
                        R.ESayC("worry", l53),
                        t.ESayC("angry", l54),
                        R.SayC(l55)
                    );
                } else {
                    await vn.Sequential(
                        R.ESayC("angry", l58),
                        t.EmoteSay("surprise", l59).And(t.RotateTo(V3(0, 720), 2, Easers.EOutSine)).C
                    );
                    using var __ = evidenceRequest.Request(tokikoInt);
                    await vn.Sequential(
                        t.SayC(l60, flags: SpeakFlags.Anonymous),
                        t.EmoteSayC("worry", l61),
                        R.SayC(l62),
                        t.ESayC("happy", l63),
                        t.ESayC("cry", l64),
                        t.ESayC("worry", l65),
                        R.ESayC("worry", l66),
                        t.ESayC("angry", l67),
                        R.SayC(l68)
                    );
                }
            });
            var lilybook = Context("lilybook", async () => {
                await R.ESayC("", l115);
            });
            var lilyflail = Context("lilyflail", async () => {
                var w = vn.Find<LilyWhite>();
                await w.EmoteSay("surprise", l166).And(w.RotateTo(V3(0, 720, 720), 1.5f)).C;
                await w.Say(l167).And(w.RotateTo(V3(0), 1.5f)).C;
            });
            StrongBoundedContext<InterruptionStatus> lilyInt(Evidence ev) => new(VN, "lily_int", async () => {
                var l = vn.Find<Letty>();
                if (ev is Evidence.IceBlock) {
                    var w = vn.Find<LilyWhite>();
                    await vn.Sequential(
                        R.ESayC("angry", l163),
                        R.SayC(l164, flags: SpeakFlags.Anonymous),
                        vn.SFX("vn-ice-break"),
                        vn.Wait(0.4),
                        w.ESayC("cry", l165)
                    );
                    await lilyflail;
                    await l.ESayC("worry", l168);
                    await lilyflail;
                    await vn.Sequential(
                        w.Say(l171, flags: SpeakFlags.Anonymous)
                            .And(vn.Find<Book2>().MoveBy(V3(-4, -3), 2, Easers.EOutSine)).C,
                        R.ESayC("worry", l172),
                        R.ESayC("surprise", l173),
                        R.ESayC("", l174),
                        R.ESayC("angry", l175)
                    );
                    await lilyflail;
                    await l.ESayC("worry", l178);
                    UpdateDataV(d => {
                        d.State = d.State with { RemovedLily = true };
                        d.EvFlower.Enabled = true;
                        d.EvZoning.Enabled = true;
                    });
                    await vn.Sequential(
                        R.ESayC("satisfied", l179),
                        vn.SFX("extend-any"),
                        R.SayC(l180, flags: SpeakFlags.Anonymous),
                        R.ESayC("surprise", l181),
                        vn.SFX("extend-any"),
                        R.SayC(l182, flags: SpeakFlags.Anonymous)
                    );
                } else {
                    await vn.Sequential(
                        R.ESayC("angry", l160),
                        l.ESayC("worry", l161),
                        R.SayC(l162));
                }
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            var lilywhite = Context("lilywhite", async () => {
                var w = VN.Find<LilyWhite>();
                var l = VN.Find<Letty>();
                using var _ = evidenceRequest.Request(lilyInt);
                await vn.Sequential(
                    l.ESayC("happy", l116),
                    w.ESayC("cry", l117),
                    l.ESayC("worry", l118),
                    w.ESayC("surprise", l119),
                    l.SayC(l120),
                    w.ESayC("cry", l121),
                    l.SayC(l122),
                    w.ESayC("worry", l123),
                    l.SayC(l124),
                    w.SayC(l125),
                    R.ESayC("angry", l126),
                    l.ESayC("happy", l127),
                    w.ESayC("happy", l128),
                    R.Say(l129)
                );
                var (i, _) = await selector.WaitForSelection("s1", l131, l142, l149);
                if (i == 0) {
                    await vn.Sequential(
                        R.SayC(l132),
                        l.ESayC("worry", l133),
                        w.ESayC("worry", l134),
                        l.ESayC("", l135),
                        l.ESayC("worry", l136),
                        R.SayC(l137),
                        l.ESayC("surprise", l138),
                        w.ESayC("worry", l139),
                        R.ESayC("worry", l140)
                    );
                } else if (i == 1) {
                    await vn.Sequential(
                        R.ESayC("worry", l143),
                        w.ESayC("happy", l144),
                        w.SayC(l145, flags: SpeakFlags.Anonymous),
                        R.SayC(l146),
                        w.SayC(l147)
                    );
                    UpdateDataV(d => d.EvGloves.Enabled = true);
                } else if (i == 2) {
                    await vn.Sequential(
                        R.ESayC("worry", l150),
                        l.ESayC("worry", l151),
                        R.SayC(l152),
                        w.ESayC("worry", l153),
                        R.ESayC("angry", l154),
                        l.SayC(l155),
                        R.SayC(l156),
                        w.SayC(l157),
                        R.SayC(l158)
                    );
                }

            });

            StrongBoundedContext<InterruptionStatus> sunnyIntF = new(VN, "sunnyIntF", async () => {
                await VN.Find<Sunny>().ESayC("worry", "That doesn't prove anything!");
                return InterruptionStatus.Abort;
            }) {LoadSafe = false };
            
            StrongBoundedContext<InterruptionStatus> starInt(Evidence ev) => new(VN, "star_int", async () => {
                var s = vn.Find<Star>();
                if (ev is Evidence.ZoningCode) {
                    using (var _ = await MakeObjection()) {
                        await vn.Sequential(
                            s.ESayC("surprise", l199),
                            R.ESayC("satisfied", l200),
                            R.ESayC("angry", l201),
                            s.ESayC("cry", l202),
                            s.EmoteSay("angry", l203).And(s.MoveBy(V3(2, 0), 1), s.FadeTo(0, 1)).C,
                            R.SayC(l204)
                        );
                    }
                    UpdateDataV(d => d.State = d.State with { RemovedStar = true });
                } else {
                    await s.ESayC("worry", "Reimu, you can't pay for hot cocoa with that.");
                    await R.ESayC("angry", "I- I know that!");
                }
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };
            
            StrongBoundedContext<InterruptionStatus> lunaInt(Evidence ev) => new(VN, "luna_int", async () => {
                var l = vn.Find<Luna>();
                if (ev is Evidence.FlowerPetal) {
                    await R.ESayC("happy", l237);
                    await l.EmoteSay("surprise", l238);
                    var (index, _) = await selector.WaitForSelection("s1", l240, l251);
                    if (index == 0) {
                        await vn.Sequential(
                            l.ESayC("happy", l241),
                            l.SayC(l242),
                            R.ESayC("worry", l243),
                            l.ESayC("surprise", l244),
                            R.ESayC("angry", l245),
                            R.SayC(l246, flags: SpeakFlags.Anonymous),
                            l.ESayC("cry", l247),
                            R.SayC(l248),
                            l.ESayC("angry", l249)
                        );
                    } else {
                        await vn.Sequential(
                            R.SayC(l252, flags: SpeakFlags.Anonymous),
                            l.ESayC("surprise", l253),
                            R.ESayC("angry", l254),
                            l.ESayC("surprise", l255),
                            l.EmoteSay("cry", l256).And(l.MoveBy(V3(12, 0), 2, Easers.ELinear)).C,
                            R.ESayC("happy", l257)
                        );
                        UpdateDataV(d => {
                            d.EvFlower.Destroyed = true;
                            d.State = d.State with { RemovedLuna = true };
                        });
                    }
                } else {
                    await R.ESayC("angry", "Take a look at THIS!");
                    await l.ESayC("", l231);
                    await R.ESayC("angry", "...");
                }
                return InterruptionStatus.Abort;
            }) { LoadSafe = false };

            var luna = Context("luna", async () => {
                var l = vn.Find<Luna>();
                using var _ = evidenceRequest.Request(lunaInt);
                await vn.Sequential(
                    R.ESayC("angry", l229),
                    l.ESayC("surprise", l230),
                    l.ESayC("", l231),
                    R.SayC(l232),
                    l.SayC(l233),
                    R.ESayC("worry", l234),
                    l.SayC(l235),
                    R.ESayC("surprise", l236)
                );
            });
            var snowman = Context("snowman", async () => {
                await R.ESayC("worry", l184);
                await R.ESayC("angry", l184_1);
            });
            var star = Context("star", async () => {
                var s = VN.Find<Star>();
                await R.ESayC("", l187);
                using var _ = evidenceRequest.Request(starInt);
                await vn.Sequential(
                    s.ESayC("happy", l188),
                    R.ESayC("happy", l189),
                    s.ESayC("", l190),
                    R.ESayC("worry", l191),
                    s.ESayC("worry", l192),
                    R.ESayC("angry", l193),
                    s.ESayC("happy", l194),
                    R.SayC(l195),
                    s.ESayC("worry", l196),
                    R.SayC(l197)
                );
            });
            var sunny = Context("sunny", async () => {
                var s = VN.Find<Sunny>();
                using var _ = evidenceRequest.Request(_ => sunnyIntF);
                await vn.Sequential(
                    R.ESayC("worry", l205),
                    s.EmoteSay("happy", l206).And(s.RotateTo(V3(0, 0, 360), 2.4f)).C,
                    R.ESayC("angry", l207),
                    s.EmoteSay("worry", l208).And(s.RotateTo(V3(0), 2.4f)).C
                );
                var (index, _) = await selector.WaitForSelection("s1", l210, l215);
                if (index == 0) {
                    await vn.Sequential(
                        R.ESayC("happy", l211),
                        s.ESayC("worry", l212),
                        R.ESayC("angry", l213)
                    );
                } else {
                    await vn.Sequential(
                        R.ESayC("angry", l216),
                        s.ESayC("angry", l217),
                        R.SayC(l218),
                        s.ESayC("cry", l219),
                        s.SayC(l220)
                    );
                    UpdateDataV(d => d.EvDrawerKey.Enabled = true);
                    await vn.Sequential(
                        s.EmoteSay("cry", l221).And(s.RotateTo(V3(0,0, 1440), 6, Easers.EOutSine), 
                            s.MoveBy(V3(-12, 0), 4, Easers.ELinear)).C
                        );
                    UpdateDataV(d => d.State = d.State with {RemovedSunny = true});
                    await vn.Sequential(
                        R.ESayC("worry", l222),
                        R.ESayC("happy", l223),
                        vn.SFX("extend-any"),
                        R.SayC(l224, flags: SpeakFlags.Anonymous)
                    );
                }
            });

            StrongBoundedContext<Unit> PresentAny((Evidence, Target) evt) => new(VN, "interruptT0", async () => {
                var (ev, t) = evt;
                if (ev is Evidence.RoomKey && t is Target.Door) {
                    await R.ESayC("worry", l20);
                    _ = GoToMap(SnowCirno, d => d.State = d.State with { UnlockedDoor = true});
                } else if (ev is Evidence.DrawerKey && t is Target.Drawer) {
                    await vn.Sequential(
                        R.ESayC("happy", l21),
                        vn.SFX("extend-any"),
                        R.ESayC("surprise", l22),
                        R.ESayC("angry", l23),
                        R.ESayC("worry", l24),
                        R.SayC(l25),
                        R.ESayC("angry", l26)
                    );
                    UpdateDataV(d => d.EvOSHA.Enabled = true);
                } else if (ev is Evidence.RoomKey or Evidence.DrawerKey && t is Target.Door or Target.Drawer) {
                    await R.ESayC("worry", "This key doesn't fit this lock.");
                } else if (t is Target.Cirno) {
                    await cirnoInterrupt(ev);
                } else if (t is Target.Tokiko) {
                    await tokikoIntF(ev);
                } else if (t is Target.LilyAndLetty) {
                    await lilyInt(ev);
                } else if (t is Target.Star) {
                    await starInt(ev);
                } else if (t is Target.Sunny) {
                    await sunnyIntF;
                } else if (t is Target.Luna) {
                    await lunaInt(ev);
                } else if (ev is Evidence.Gloves && t is Target.Snowman) {
                    if (Data.EvSnowball.Enabled) {
                        await R.ESayC("", "I already got a snowball from the snowman.");
                    } else {
                        await R.ESayC("", l185);
                        UpdateDataV(d => d.EvSnowball.Enabled = true);
                        await vn.SFX("extend-any");
                        await R.SayC(l186, flags: SpeakFlags.Anonymous);
                    }
                } else {
                    await R.ESayC("worry", "Hmm, I'm not really sure how I should use that.");
                }
                return default;
            }) { LoadSafe = false };

            var finalSegment = Context("final", async () => {
                UpdateDataV(d => d.EndingVNStarted = true);
                await vn.Sequential(
                    R.ESayC("worry", "...Wait, is that all of them?"),
                    R.ESayC("happy", "Yup, I've gotten rid of all the pesky youkai. No need to stay out in the cold. Let's head on back home.")
                    );
                HideMD();
                await GoToMap(House);
                ShowMD();
                using var m = vn.Add(new Marisa());
                m.Alpha = 0;
                m.LocalLocation.Value = V3(-3, 12);
                await vn.Sequential(
                    R.ESayC("happy", "Ah, would you listen to that!"),
                    R.ESayC("satisfied", "The sound of silence!"),
                    R.Say("Now I can finally enjoy the rest of the day!")
                        .And(R.MoveTo(V3(2, 0), 1.4f)).C,
                    R.ESayC("happy", "Nothing could possibly ruin a day like this!"),
                    vn.SFX("vn-objection").AsVnOp(vn).Then(vn.Wait(0.8),
                        R.EmoteSay("angry", "Huh? What was that?")).C
                );
                var mm = m.MoveTo(V3(-3, 0), 1f, Bezier.CBezier(.4, .62, .45, 1.24))
                    .And(m.FadeTo(1f, 0.5f)).Task;
                await vn.Wait(0.4f);
                var objt = MakeObjection(false);
                await R.MoveBy(V3(1.3, 0), 0.6f).And(
                    R.SetEmote("surprise").AsVnOp(vn),
                    R.Disturb(R.ComputedLocalLocation, JumpY(1), 0.6f)
                );
                await mm;
                using var obj = await objt;
                await vn.Sequential(
                    m.ESayC("happy", "Yo, Reimu! Let's go throw some snowballs with the fairies!"),
                    R.ESayC("angry", "...Dammit."));
                await rg.DoTransition(new RenderGroupTransition.Fade(rgb, 2f));
                completion.SetResult(new UnitADVCompletion());
            });

            void ConfigureUniversal(string mapName, WPIdealizedState i, WPADVData d) {
                i.Assert(new TopLevelEvidenceAssertion<(Evidence, Target), Unit>(targetEvReq.Request, PresentAny));
                if (Data.State.RequirementsComplete && !Data.EndingVNStarted) {
                    i.Assert(new RunOnEntryAssertion(() => Manager.TryOrDelayExecuteVN(finalSegment)) 
                        { Priority = (int.MaxValue, 0)});
                }
                if (d.State.UnlockedDoor)
                    foreach (var map in maps) {
                        var isCurr = map.key == mapName;
                        i.Assert(new InteractableAssertion(m, _ => GoToMapUI(map.key), $"go to {map.key}") {
                            Location = V3(7.3f, 1.8f + map.mapLinkOffset),
                            Info = new InteractableInfo.Map(isCurr),
                            Hover = new Interactable.HoverAction.VNOp(() => 
                                R.Say(isCurr ? 
                                    $"I'm currently at {map.desc(d)}." :
                                    $"Should I head over to {map.desc(d)}?"))
                        });
                    }
            }
            ms.ConfigureMap(House, (i, d) => {
                ConfigureUniversal(House, i, d);
                i.Assert(new EntityAssertion<ShrineRoomBG>(VN));
                i.Assert(new BGMAssertion(VN, "s01-1"));
                i.Assert(new CharacterAssertion<Reimu>(VN) {
                    Location = V3(4, 0)
                });
                var chest = new EntityAssertion<Chest>(VN) {
                    Location = V3(-6, -3),
                    Scale = V3(0.8, 0.8, 1)
                };
                i.Assert(d.EvOSHA.Enabled ? chest : InteractableObj(chest, chestFail, target: new Target.Drawer()));
                if (!d.State.UnlockedDoor) {
                    i.Assert(
                        new InteractableBCtxAssertion(m, doorFail) {
                            Location = V3(-3.3f, 1)
                        }.AsObject(),
                        targetEvReq.MakeTarget(new Target.Door(), evTargets, V3(-3.3f, 1))
                    );
                }
                if (!d.EvKey.Enabled)
                    i.Assert(new EntityAssertion<Key>(VN) {
                        Location = V3(0, -1.4f),
                        EulerAnglesD = V3(0, 0, -44)
                    }.WithChildren(
                        new InteractableBCtxAssertion(m, pickupKey).AsObject()
                    ));
                if (!s0.IsCompletedInContexts())
                    i.SetEntryVN(s0);
                /*
                i.Assert(Interactable(new CharacterAssertion<Luna>(VN) {
                    Location = V3(-6, 0)
                }, s0, Luna.SpeakIconOffset, new Target.Marisa()));*/
            });

            ms.ConfigureMap(SnowCirno, (i, d) => {
                ConfigureUniversal(SnowCirno, i, d);
                i.Assert(new EntityAssertion<Snow2BG>(VN));
                i.Assert(new BGMAssertion(VN, "gotp.hg"));
                i.Assert(new CharacterAssertion<Reimu>(VN) {
                    Location = V3(3, 0)
                });
                if (!d.State.RemovedCirno)
                    i.Assert(Interactable(new CharacterAssertion<Cirno>(VN) {
                        Location = V3(-3, 0),
                    }, cirno, Cirno.SpeakIconOffset, new Target.Cirno()));
            });

            ms.ConfigureMap(SnowHouse, (i, d) => {
                ConfigureUniversal(SnowHouse, i, d);
                i.Assert(new EntityAssertion<SnowHouseBG>(VN));
                i.Assert(new BGMAssertion(VN, "s02-5"));
                i.Assert(new CharacterAssertion<Reimu>(VN) {
                    Location = V3(-4, -0.7), Scale = V3(0.6f)
                });
                if (!d.State.RemovedTokiko)
                    i.Assert(Interactable(new CharacterAssertion<Tokiko>(VN) {
                        Location = V3(0.2, 0.6), Scale = V3(0.6f)
                    }, tokiko, Tokiko.SpeakIconOffset, new Target.Tokiko()));
                if (!d.EvLaw.Enabled)
                    i.Assert(new EntityAssertion<Book1>(VN) {
                        Location = V3(3.5, 2),
                        Scale = V3(-0.5, 0.5),
                    }.WithChildren(InteractableBCTX(d.State.RemovedTokiko ? tkbook2 : tkbook1).AsObject()));
            });
            
            ms.ConfigureMap(SnowHill, (i, d) => {
                ConfigureUniversal(SnowHill, i, d);
                i.Assert(new EntityAssertion<SnowHillBG>(VN));
                i.Assert(new BGMAssertion(VN, "s02-6"));
                i.Assert(new CharacterAssertion<Reimu>(VN) {
                    Location = V3(-5, -0.6), Scale = V3(0.4f)
                });
                if (!d.State.RemovedLily)
                    i.Assert(
                        new CharacterAssertion<LilyWhite>(VN) {
                            Location = V3(-1.7, 3), Scale = V3(0.2f)
                        },
                        Interactable(new CharacterAssertion<Letty>(VN) {
                            Location = V3(-0.8, 3), Scale = V3(0.2f)
                        }, lilywhite, Letty.SpeakIconOffset + V3(3, 0), new Target.LilyAndLetty()), 
                        new EntityAssertion<Book2>(VN) {
                            Location = V3(1, 2), Scale = V3(-0.4, 0.4, 1)
                        }.WithChildren(InteractableBCTX(lilybook, V3(0, 0.5)).AsObject()));
            });
            
            ms.ConfigureMap(SnowMF, (i, d) => {
                ConfigureUniversal(SnowMF, i, d);
                i.Assert(new BGMAssertion(VN, "s01-3"));
                i.Assert(new EntityAssertion<Snow1BG>(VN));
                i.Assert(new CharacterAssertion<Reimu>(VN) {
                    Location = V3(0.5, 0), Scale=V3(0.9f)
                }, InteractableObj(new EntityAssertion<Snowman>(VN) { Location = V3(6.5, -1), Scale = V3(-0.8f, 0.8f, 1)}, 
                    snowman, V3(0.8f, 1), new Target.Snowman(), V3(0.8f, 1)));
                if (!d.State.RemovedStar) {
                    i.Assert(Interactable(new CharacterAssertion<Star>(VN) { Location=V3(-6.5, 0), Scale=V3(0.8f) }, 
                        star, Star.SpeakIconOffset, new Target.Star()));
                }
                if (!d.State.RemovedSunny) {
                    i.Assert(Interactable(new CharacterAssertion<Sunny>(VN) { Location = V3(-3, 0), Scale=V3(0.8f)}, 
                        sunny, Sunny.SpeakIconOffset, new Target.Sunny()));
                }
                if (!d.State.RemovedLuna) {
                    i.Assert(Interactable(new CharacterAssertion<Luna>(VN) {Location = V3(4, 0), Scale=V3(0.8f)}, 
                        luna, Luna.SpeakIconOffset, new Target.Luna()));
                }
            });
            
            return ms;
        }


        private IAssertion Interactable<C>(CharacterAssertion<C> c, BoundedContext<Unit>? dialogue,
            Vector3 speakIconOffset = default, Target? target = null, Vector3 interactIconOffset = default)
            where C : ICharacter, new() =>
            c.WithChildren(
                dialogue != null ?
                    InteractableBCTX(dialogue, speakIconOffset)
                    : null,
                target != null ?
                    targetEvReq.MakeTarget(target, evTargets, interactIconOffset)
                    : null
            );
        
        private IAssertion InteractableObj<C>(EntityAssertion<C> c, BoundedContext<Unit>? dialogue,
            Vector3 speakIconOffset = default, Target? target = null, Vector3 interactIconOffset = default)
            where C : IRendered, new() =>
            c.WithChildren(
                dialogue != null ?
                    InteractableBCTX(dialogue, speakIconOffset).AsObject()
                    : null,
                target != null ?
                    targetEvReq.MakeTarget(target, evTargets, interactIconOffset)
                    : null
            );
        
        public record WPIdealizedState(Executing e) : ADVIdealizedState(e) {
            protected override Task FadeIn() {
                return e.rgb.DoTransition(new RenderGroupTransition.Fade(e.rg, 0.7f)).Task;
            }
            protected override Task FadeOut() {
                return e.rg.DoTransition(new RenderGroupTransition.Fade(e.rgb, 0.7f)).Task;
            }
        }
    }

    [Serializable]
    public record WPADVData: ADVData {
        public bool EndingVNStarted = false;
        public State State = new();
        public State DelayedState = new();
        public Evidence.RoomKey EvKey { get; init; } = new();
        public Evidence.DrawerKey EvDrawerKey { get; init; } = new();
        public Evidence.IceBlock EvIceBlock { get; init; } = new();
        public Evidence.FlowerPetal EvFlower { get; init; } = new();
        public Evidence.Gloves EvGloves { get; init; } = new();
        public Evidence.Snowball EvSnowball { get; init; } = new();
        public Evidence.OSHA EvOSHA { get; init; } = new();
        public Evidence.ZoningCode EvZoning { get; init; } = new();
        public Evidence.MunicipalLaw EvLaw { get; init; } = new();

        [JsonIgnore] public Evidence[] Evidences = null!;

        public WPADVData(InstanceData VNData) : base(VNData) {
            SnapshotReferences();
        }

        [OnDeserialized]
        internal void _OnDeserialized(StreamingContext _) => SnapshotReferences();

        private void SnapshotReferences() {
            Evidences = new Evidence[]
                { EvKey, EvDrawerKey, EvIceBlock, EvFlower, EvGloves, EvSnowball, EvOSHA, EvZoning, EvLaw };
        }
    }

    [Serializable]
    public abstract record Evidence {
        public bool Enabled { get; set; } = false;
        [JsonIgnore]
        public abstract LString Title { get; }
        [JsonIgnore]
        public abstract LString Description { get; }

        public record RoomKey : Evidence {
            public override LString Title => "Room Key";
            public override LString Description =>
                "This key opens the door to my room.";
        }

        public record DrawerKey : Evidence {
            public override LString Title => "Unknown Key";
            public override LString Description =>
                "This key opens something, but I can't remember what.";
        }

        public record IceBlock : Evidence {
            public override LString Title => "Block of Ice";
            public override LString Description =>
                "This is a block of ice that Cirno dropped. I have no use for it, but since she" +
                " dropped it on my property, it's mine now.";
        }
        public record FlowerPetal : Evidence {
            public bool Destroyed { get; set; } = false;
            public override LString Title => !Destroyed ? "Sakura Petal" : "Fragments of a Sakura Petal";
            public override LString Description =>
                Destroyed ?
                    "This was a cherry blossom petal, until I ripped it to pieces to get Luna Child to BTFO." :
                    "This is a cherry blossom petal that Lily White dropped. I'm not exactly sure how " +
                    "she has one of these things in the middle of winter, but I suppose I should expect " +
                    "just as much from the youkai of spring.";
        }

        public record Gloves : Evidence {
            public override LString Title => "Snow Gloves";
            public override LString Description =>
                "A pair of snow gloves. I can use these to pick up snow.";
        }

        public record Snowball : Evidence {
            public override LString Title => "Snowball";
            public override LString Description =>
                "A snowball I carved out of Luna Child's snowman while she wasn't looking.";
        }
        
        public record OSHA : Evidence {
            public override LString Title => "OSHA standard 1910.23(b)(11-13)";
            public override LString Description =>
                "An excerpt from the OSHA regulations for ladders. It states:\nEach employee faces the ladder when climbing up or down it; Each employee uses at least one hand to grasp the ladder when climbing up and down it; and No employee carries any object or load that could cause the employee to lose balance and fall while climbing up or down the ladder.";
        }

        public record ZoningCode : Evidence {
            public override LString Title => "Residential Zoning Code Chapter 2 Article 1";
            public override LString Description =>
                "An excerpt from the Gensoukyou zoning code. It states:\nNo building, structure or land shall be used and no building or structure shall be erected, structurally altered, enlarged or maintained except for the following uses, and when a \"<b>Supplemental Use District</b>\" is created by the provisions of Article 3 of this chapter, for such uses as may be permitted therein:\n- One-family dwelling";
        }

        public record MunicipalLaw : Evidence {
            public override LString Title => "Municipal Code Section 10.5.80";
            public override LString Description =>
                "An excerpt from Gensoukyou's municipal law code. It states:\nIt is unlawful for any person to throw or shoot any stone or any other missile upon or at any person, animal, building, tree or other public or private property; or at or against any vehicle or equipment designed for the transportation of persons or property.\nI wonder if a snowball counts as a missile?";
        }
    }

    [Serializable]
    public abstract record Target(LString? Tooltip) : IEvidenceTarget {
        public record Door() : Target("Door");

        public record Drawer() : Target("Drawer");
        public record LilyAndLetty() : Target("Lily and Letty");

        public record Tokiko() : Target("Tokiko");

        public record TokikoBook() : Target("Book on the roof");

        public record Cirno() : Target("Cirno");

        public record Star() : Target("Star Sapphire");

        public record Sunny() : Target("Sunny Milk");

        public record Luna() : Target("Luna Child");

        public record Snowman() : Target("Snowman");
    }

    //must be record-typed for DelayedState to work
    public record State(
        bool UnlockedDoor = false,
        bool RemovedLily = false, bool RemovedTokiko = false, bool RemovedCirno = false,
        bool RemovedStar = false, bool RemovedSunny = false, bool RemovedLuna = false) {
        public bool RequirementsComplete => RemovedCirno && RemovedLily && RemovedTokiko && RemovedStar &&
                                             RemovedSunny && RemovedLuna;
    }

    public override IExecutingADV Setup(ADVInstance inst) {
        if (inst.ADVData.CurrentMap == "")
            throw new Exception("WP was loaded with no current map.");
        Logs.Log("Starting WP execution...");
        return new Executing(this, inst);
    }

    public override ADVData NewGameData() => new WPADVData(new(SaveData.r.GlobalVNData)) {
        CurrentMap = Executing.House
    };
}
}