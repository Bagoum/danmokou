using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.Scenes;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using static SuzunoyaUnity.Helpers;
using static MiniProjects.VN.ScriptLocalization.BlessedRain;
using static BagoumLib.Mathematics.Bezier;
using Vector3 = System.Numerics.Vector3;
// ReSharper disable AccessToDisposedClosure

namespace MiniProjects.VN {
public static class _VNBlessedRain {
    private record Shared(DMKVNState vn, ADVDialogueBox md, UnityRenderGroup rg, UnityRenderGroup rgb) {
        public DMKVNState.RunningAudioTrackProxy bgm { get; private set; } = null!;
        private AudioSource? rain { get; set; } = null!;
        private AudioSource footstep { get; set;  }= null!;
        
        public LazyAction GetFootstep(string key = "vn-footstep-1") 
            => vn.Source(key, x => footstep = x);
        public LazyAction GetRain(string key = "vn-rain-1") 
            => vn.Source(key, x => rain = x);

        public LazyAction StopFootstep() => new(() => {
            if (footstep != null)
                footstep.Stop();
        });
        public LazyAction GetBGM(string key) => new(() => bgm = vn.RunBGM(key));
    }
    private static BoundedContext<Unit> _TopLevel(DMKVNState vn) => new(vn, "TOP", async () => {
        var o = new Shared(vn, vn.Add(new ADVDialogueBox()), (UnityRenderGroup)vn.DefaultRenderGroup, 
            new UnityRenderGroup(vn, "black", 1, true));
        o.rg.Visible.Value = false;
        o.md.Alpha = 1;
        await o.GetRain();
        await _01Shrine(o);
        await _02Moriya(o);
        await _03Forest(o);
        return default;
    });

    private static BoundedContext<Unit> _01Shrine(Shared o) => new(o.vn, "1:SHRINE", async () => {
        var vn = o.vn;
        using var room = vn.Add(new ShrineCourtyardBG());
        using var reimu = vn.Add(new Reimu());
        using var yukari = vn.Add(new Yukari());
        yukari.Alpha = 0;
        using var kasen = vn.Add(new Kasen());
        kasen.Alpha = 0;
        using var narr = vn.Add(new SilentNarrator());
        reimu.Location.Value = V3(2.5f, 0);
        await o.rgb.DoTransition(new RenderGroupTransition.Fade(o.rg, 1))
            .And(reimu.EmoteSay("cry", l0))
            .And(vn.Wait(0.1).Then(o.GetBGM("br.bgm1").AsVnOp(vn))).C;
        await vn.Sequential(
            narr.SayC(l1),
            narr.SayC(l1_1),
            reimu.SayC(l2),
            narr.SayC(l3),
            reimu.SayC(l4),
            narr.SayC(l5),
            reimu.SayC(l6),
            narr.SayC(l7),
            reimu.ESayC("worry", l8),
            reimu.ESayC("surprise", l9),
            reimu.ESayC("happy", l10),
            narr.SayC(l11),
            reimu.SayC(l12),
            narr.SayC(l13),
            Lazy(() => {
                yukari.Location.Value = V3(-2.5f, 6);
                yukari.EulerAnglesD.Value = V3(0, 0, 180);
            }),
            yukari.SetEmote("worry"),
            vn.SFX("vn-yukari-power"),
            yukari.MoveBy(V3(0, -1), 0.8f).And(yukari.FadeTo(1, 0.8f)),
            yukari.SayC(l14),
            reimu.ESayC("normal", l15),
            yukari.ESayC("surprise", l16),
            vn.SFX("vn-yukari-power"),
            yukari.MoveBy(V3(0, 0.5), 0.4f).And(reimu.EmoteSay("surprise", l17)).C,
            reimu.ESayC("worry", l18),
            yukari.ESayC("happy", l19),
            reimu.ESayC("happy", l20),
            reimu.SayC(l21),
            yukari.ESayC("worry", l22),
            reimu.ESayC("cry", l23),
            yukari.SayC(l24),
            yukari.ESayC("happy", l25),
            vn.SFX("vn-yukari-power"),
            yukari.MoveBy(V3(0, 1), 0.8f).And(yukari.FadeTo(0, 0.8f)),
            yukari.ESayC("happy", l26),
            kasen.SayC(l27),
            yukari.ESayC("surprise", l28),
            reimu.ESayC("worry", l29),
            kasen.ESayC("worry", l30),
            yukari.SetEmote("normal"),
            Lazy(() => {
                yukari.Location.Value = V3(-6, 0);
                yukari.EulerAnglesD.Value = V3(0);
                kasen.Location.Value = V3(-3, 0);
            }),
            vn.SFX("vn-yukari-power"),
            kasen.MoveBy(V3(1, 0), 0.8f).And(kasen.FadeTo(1, 0.8f)).And(vn.Wait(0.6).Then(
                vn.SFX("vn-yukari-power").AsVnOp(vn),
                yukari.MoveBy(V3(1, 0), 0.8f).And(yukari.FadeTo(1, 0.8f)),
                kasen.Say(l31)
            )).C,
            reimu.ESayC("normal", l32),
            kasen.ESayC("worry", l33),
            kasen.SetEmote("angry"),
            narr.SayC(l34),
            kasen.SayC(l35),
            kasen.SayC(l36),
            kasen.SayC(l37),
            reimu.ESayC("worry", l38),
            yukari.ESayC("surprise", l39),
            yukari.SayC(l40),
            yukari.SayC(l41),
            kasen.ESayC("worry", l42),
            yukari.ESayC("angry", l43),
            reimu.SayC(l44),
            kasen.SayC(l45),
            yukari.ESayC("happy", l46),
            kasen.ESayC("", l47),
            reimu.SayC(l48),
            yukari.SayC(l49),
            kasen.SayC(l50),
            reimu.ESayC("cry", l51),
            yukari.ESayC("surprise", l52),
            kasen.SayC(l53),
            reimu.ESayC("", l54),
            yukari.ESayC("worry", l55),
            kasen.SayC(l56),
            yukari.SayC(l57),
            reimu.ESayC("worry", l58),
            yukari.ESayC("happy", l59),
            kasen.SayC(l60),
            reimu.ESayC("cry", l61),
            yukari.SayC(l62),
            kasen.ESayC("angry", l63),
            reimu.ESayC("surprise", l64),
            yukari.ESayC("surprise", l65),
            kasen.ESayC("worry", l66),
            yukari.ESayC("cry", l67),
            kasen.ESayC("", l68),
            reimu.ESayC("", l69),
            yukari.ESayC("worry", l70),
            kasen.SayC(l71),
            reimu.SayC(l72),
            reimu.ESayC("cry", l73, flags: SpeakFlags.DontClearText),
            yukari.ESayC("happy", l74),
            kasen.ESayC("happy", l75),
            narr.SayC(l76),
            vn.SFX("vn-yukari-power"),
            Lazy(() => o.bgm.FadeOut(2f)),
            o.rg.DoTransition(new RenderGroupTransition.Fade(o.rgb, 2))
        );
        return default;
    });

    private static BoundedContext<Unit> _02Moriya(Shared o) => new(o.vn, "2:MORIYA", async () => {
        var vn = o.vn;
        using var shrine = vn.Add(new Shrine2BG());
        using var r = vn.Add(new Reimu());
        r.Alpha = 0;
        using var y = vn.Add(new Yukari());
        y.Alpha = 0;
        using var s = vn.Add(new Sanae());
        using var k = vn.Add(new Kanako());
        k.Alpha = 0;
        using var n = vn.Add(new SilentNarrator());
        await o.GetBGM("br.moriya");
        s.Location.Value = V3(3, 0);
        await s.SetEmote("happy");
        await o.rgb.DoTransition(new RenderGroupTransition.Fade(o.rg, 1)).And(s.Say(l77)).C;
        r.Location.Value = V3(-2, 0);
        y.Location.Value = V3(-5.5, 0);
        await vn.Sequential(
            vn.SFX("vn-yukari-power"),
            r.MoveBy(V3(1, 0), 0.8f).And(r.FadeTo(1, 0.8f)).And(vn.Wait(0.6).Then(
                vn.SFX("vn-yukari-power").AsVnOp(vn),
                y.MoveBy(V3(1, 0), 0.8f).And(y.FadeTo(1, 0.8f))
                    .And(s.EmoteSay("surprise", l78))
                    .And(s.MoveBy(V3(0.5f, 0), 0.5f).And(s.Disturb(s.ComputedLocation, JumpY(0.5f), 0.5f)))
            )).C,
            s.ESayC("worry", l79),
            y.ESayC("surprise", l80),
            r.ESayC("worry", l81),
            s.ESayC("surprise", l82),
            y.ESayC("happy", l83),
            s.ESayC("happy", l84),
            s.SayC(l85),
            s.SayC(l86),
            s.ESayC("", l87),
            s.SayC(l88),
            s.SayC(l88_1),
            s.ESayC("happy", l89),
            y.ESayC("worry", l90),
            s.ESayC("angry", l91),
            s.ESayC("surprise", l92),
            y.ESayC("happy", l93),
            s.ESayC("worry", l93_1),
            r.ESayC("worry", l94),
            s.ESayC("cry", l95),
            s.ESayC("surprise", l96),
            r.SayC(l97),
            s.ESayC("disappointed", l98),
            r.ESayC("", l99),
            s.ESayC("worry", l100),
            y.ESayC("surprise", l101),
            r.SayC(l102),
            s.SayC(l103),
            r.ESayC("worry", l104),
            s.SayC(l105),
            s.ESayC("happy", l106),
            Lazy(() => o.bgm.FadeOut()),
            s.MoveBy(V3(1.5f, 0), 1f).And(s.FadeTo(0, 1)),
            y.ESayC("angry", l107),
            r.RotateTo(V3(0, 180), 0.6f).And(r.EmoteSay("", l108)).C,
            y.ESayC("worry", l109),
            r.ESayC("worry", l110),
            o.GetBGM("br.kanako"),
            Lazy(() => {
                k.Location.Value = V3(5.3, 0);
            }),
            k.MoveBy(V3(-1.5f, 0), 1f).And(k.FadeTo(1, 1)).And(k.Say(l111)).C,
            y.ESayC("surprise", l112),
            r.RotateTo(V3(0), 0.6f).And(r.EmoteSay("", l113)).C,
            k.ESayC("happy", l114),
            r.ESayC("worry", l115),
            k.ESayC("worry", l116),
            k.ESayC("angry", l117),
            r.ESayC("worry", l118),
            y.ESayC("angry", l119),
            r.RotateTo(V3(0, 180), 0.6f).And(r.EmoteSay("surprise", l120)).C,
            y.ESayC("cry", l121),
            k.ESayC("cry", l122),
            y.SayC(l123),
            r.RotateTo(V3(0), 0.6f).And(r.EmoteSay("worry", l124)).C,
            k.ESayC("angry", l125),
            k.SayC(l126),
            k.SayC(l126_1),
            k.ESayC("surprise", l127),
            k.ESayC("worry", l128),
            y.ESayC("worry", l129),
            r.SayC(l130),
            k.ESayC("", l131),
            k.SayC(l132),
            k.SayC(l133),
            k.SayC(l134),
            k.SayC(l135),
            k.ESayC("happy", l136),
            k.ESayC("worry", l137),
            k.SayC(l137_1),
            k.SayC(l138),
            r.ESayC("surprise", l139),
            k.SayC(l140),
            r.SayC(l141),
            y.ESayC("cry", l142),
            k.SayC(l143),
            r.ESayC("", l144),
            k.ESayC("surprise", l145),
            r.EmoteSay("surprise", l146).And(r.Disturb(r.ComputedLocation, JumpY(0.5f), 0.6f)).C,
            k.EmoteSayC("injured", l147),
            r.ESayC("angry", l148),
            k.EmoteSayC("", l149),
            r.SayC(l150),
            k.SayC(l151),
            r.SayC(l152),
            k.SayC(l153),
            r.ESayC("happy", l154),
            k.EmoteSayC("injured", l155),
            r.EmoteSay("surprise", l156).And(r.Disturb(r.ComputedLocation, JumpY(0.5f), 0.6f)).C,
            k.ESayC("worry", l157),
            r.SayC(l158),
            y.ESayC("happy", l159),
            k.ESayC("worry", l160),
            r.ESayC("worry", l161),
            k.SayC(l162),
            r.ESayC("", l163),
            y.ESayC("worry", l164),
            k.SayC(l165),
            r.SayC(l166),
            k.SayC(l167),
            r.ESayC("surprise", l168),
            y.SayC(l169),
            k.SayC(l170),
            r.SayC(l171),
            y.SayC(l172),
            k.SayC(l173),
            r.ESayC("angry", l174),
            k.ESayC("surprise", l175),
            y.ESayC("happy", l176),
            r.ESayC("happy", l177),
            y.SetEmote("worry"),
            k.ESayC("worry", l178),
            r.ESayC("satisfied", l179),
            k.ESayC("injured", l180),
            r.SayC(l181),
            k.ESayC("worry", l182),
            r.SayC(l183),
            k.SayC(l184),
            r.ESayC("worry", l185),
            y.ESayC("worry", l186),
            r.RotateTo(V3(0, 180), 0.6f).And(r.EmoteSay("surprise", l187)).C,
            y.SayC(l188),
            r.ESayC("worry", l189),
            y.SayC(l190),
            r.SayC(l191),
            y.SayC(l192),
            k.ESayC("injured", l193),
            r.RotateTo(V3(0), 0.6f).And(r.EmoteSay("happy", l194)).C,
            k.Say(l195).And(k.MoveBy(V3(1.5f, 0), 1f))
                .And(k.FadeTo(0, 1f))
                .And(Lazy(() => o.bgm.FadeOut()).AsVnOp(vn)).C,
            r.EmoteSayC("satisfied", l197),
            y.ESayC("surprise", l198),
            r.ESayC("worry", l199),
            y.SayC(l200),
            r.ESayC("satisfied", l201),
            y.ESayC("angry", l202),
            y.ESayC("happy", l203),
            r.ESayC("worry", l204),
            y.ESayC("worry", l205),
            r.SayC(l206),
            y.SayC(l207),
            r.SayC(l208),
            y.ESayC("happy", l209),
            vn.SFX("vn-yukari-power"),
            o.rg.DoTransition(new RenderGroupTransition.Fade(o.rgb, 2))
        );
        return default; 
    });
    
    private static BoundedContext<Unit> _03Forest(Shared o) => new(o.vn, "3:FOREST", async () => {
        var vn = o.vn;
        using var forest = vn.Add(new ForestBG());
        o.vn.MainDialogue!.Clear();
        using var r = vn.Add(new Reimu());
        r.Alpha = 0;
        using var y = vn.Add(new Yukari());
        y.Alpha = 0;
        using var a = vn.Add(new Aya());
        a.Alpha = 0;
        using var s = vn.Add(new Suwako());
        s.Alpha = 0;
        using var m = vn.Add(new Marisa());
        m.Alpha = 0;
        using var n = vn.Add(new SilentNarrator());
        r.Location.Value = V3(3, 2);
        await o.rgb.DoTransition(new RenderGroupTransition.Fade(o.rg, 1));
        y.Location.Value = V3(0, 6);
        y.EulerAnglesD.Value = V3(0, 0, 180);
        a.Location.Value = V3(-3.5, 0);
        m.Location.Value = V3(-4.5, 0);
        s.Location.Value = V3(-1, 0);
        await vn.Sequential(
            vn.SFX("vn-yukari-power"),
            r.MoveBy(V3(0, -2), 1.4f).And(r.FadeTo(1, 0.8f))
                .And(r.EmoteSay("surprise", l210)).C,
            r.ESayC("cry", l211),
            r.SetEmote("surprise"),
            n.Say(l212).And(r.RotateTo(V3(0, 900), 2)).C,
            r.SayC(l213),
            o.GetBGM("br.aya"),
            a.MoveBy(V3(1.5, 0), 1).And(a.FadeTo(1, 1)),
            a.SayC(l214),
            r.ESayC("worry", l215),
            a.ESayC("worry", l216),
            r.ESayC("surprise", l217),
            r.ESayC("satisfied", l218),
            a.SayC(l219),
            r.ESayC("worry", l220),
            a.SayC(l221),
            a.ESayC("surprise", l222),
            r.SayC(l223),
            a.ESayC("smug", l224),
            r.ESayC("worry", l225),
            a.SayC(l226),
            Lazy(() => o.bgm.FadeOut(1.5f)),
            n.SayC(l227),
            o.GetBGM("br.fight"),
            a.SetEmote("surprise"),
            r.SetEmote("angry"),
            a.MoveBy(V3(-2, 0), 0.5f)
                .And(r.Disturb(r.ComputedLocation, t =>
                    new Vector3(M.Sine(1, 0.1f, 4 * M.EOutSine(t)), 0, 0), 1))
                .And(a.Disturb(a.ComputedLocation, JumpY(0.7f), 0.5f))
                .And(n.Say(l228)).C,
            a.SayC(l229),
            r.SayC(l230),
            a.SayC(l231),
            r.SayC(l232),
            r.SayC(l233),
            a.ESayC("surprise", l234),
            r.ESayC("worry", l235),
            a.ESayC("angry", l236),
            r.SayC(l237),
            a.ESayC("surprise", l238),
            a.SayC(l239),
            a.SayC(l239_1),
            r.SayC(l240),
            a.ESayC("worry", l241),
            r.ESayC("angry", l242),
            a.ESayC("smug", l243),
            Lazy(() => o.bgm.FadeOut(1.5f)),
            r.ESayC("satisfied", l244),
            o.GetBGM("br.aya"),
            r.SayC(l245),
            a.ESayC("happy", l246),
            a.SayC(l247),
            a.SayC(l248),
            r.ESayC("worry", l249),
            a.ESayC("worry", l250),
            a.SayC(l251),
            r.SayC(l252),
            a.ESayC("surprise", l253),
            a.SayC(l254),
            a.ESayC("worry", l255),
            r.ESayC("surprise", l256),
            a.ESayC("surprise", l257),
            a.ESayC("angry", l258),
            a.SayC(l259),
            r.ESayC("surprise", l260),
            a.ESayC("worry", l261),
            r.ESayC("worry", l262),
            a.SayC(l263),
            a.SayC(l264),
            n.SayC(l265),
            r.ESayC("surprise", l266),
            a.ESayC("surprise", l267),
            r.SayC(l268),
            a.ESayC("worry", l269),
            a.SayC(l270),
            r.SayC(l271),
            a.SayC(l272),
            r.ESayC("satisfied", l273),
            a.ESayC("happy", l274),
            r.ESayC("worry", l275),
            s.ESayC("happy", l276),
            a.SetEmote("worry"),
            r.EmoteSay("surprise", l277).And(r.RotateTo(V3(0, 540), 1)).C,
            s.FadeTo(1, 1).And(s.MoveBy(V3(1, 0), 1)).And(vn.Wait(0.5).Then(r.MoveBy(V3(0.5, 0), 0.5f))),
            Lazy(() => o.bgm.FadeOut()),
            s.ESayC("worry", l278),
            a.SayC(l279),
            s.SayC(l280),
            r.ESayC("satisfied", l281),
            Lazy(() => s.DialogueSFX = "x-bubble-2"),
            r.SetEmote("surprise"),
            s.ESayC("angry", l282),
            o.GetBGM("br.suwako"),
            n.SayC(l283),
            s.ESayC("worry", l284),
            a.ESayC("angry", l285),
            n.SayC(l286),
            s.SayC(l287),
            a.ESayC("", l288),
            s.SayC(l289),
            a.ESayC("angry", l290),
            s.ESayC("angry", l291),
            s.SayC(l292),
            s.ESayC("happy", l293),
            a.SayC(l294),
            s.ESayC("worry", l295),
            s.SayC(l296),
            a.ESayC("worry", l297),
            s.ESayC("cry", l298),
            n.SayC(l299),
            n.SayC(l299_1),
            n.SayC(l300),
            n.SayC(l300_1),
            n.SayC(l301),
            s.SetEmote(""),
            n.SayC(l302),
            s.ESayC("surprise", l303),
            n.SayC(l304),
            s.ESayC("", l305),
            a.ESayC("worry", l306),
            r.ESayC("satisfied", l307),
            n.SayC(l308),
            r.SayC(l309),
            s.SayC(l310),
            a.SayC(l311),
            r.SayC(l312),
            a.ESayC("surprise", l313),
            s.ESayC("smug", l314),
            s.SayC(l315),
            a.ESayC("", l316),
            s.SayC(l317),
            s.SayC(l317_1),
            a.ESayC("worry", l318),
            s.ESayC("worry", l319),
            r.ESayC("happy", l320),
            s.ESayC("smug", l321),
            s.SayC(l322),
            s.SayC(l323),
            a.ESayC("worry", l324),
            a.ESayC("angry", l325, SpeakFlags.DontClearText),
            s.SayC(l326),
            a.ESayC("cry", l327),
            s.SayC(l328),
            s.SayC(l329),
            a.SayC(l330),
            r.ESayC("worry", l331),
            r.SayC(l332),
            s.ESayC("angry", l333),
            r.ESayC("angry", l334),
            s.SayC(l335),
            a.ESayC("worry", l336),
            r.ESayC("satisfied", l337),
            s.ESayC("worry", l338),
            a.SayC(l339),
            r.ESayC("surprise", l340),
            s.ESayC("happy", l341),
            a.SayC(l342),
            n.SayC(l343),
            s.ESayC("surprise", l344),
            s.SayC(l345),
            Lazy(() => s.DialogueSFX = "x-bubble-4"),
            s.ESayC("happy", l346),
            s.SayC(l347),
            o.GetBGM("br.aya"),
            s.MoveBy(V3(-1, 0), 1).And(s.FadeTo(0, 1)),
            r.ESayC("worry", l348),
            a.ESayC("worry", l349),
            a.ESayC("angry", l350),
            a.SetEmote("surprise"),
            n.SayC(l351),
            a.Say(l352).And(
                a.Disturb(a.ComputedLocation, t =>
                    new Vector3(M.Sine(1, 0.1f, 3 * M.EOutSine(t)), 0, 0), 1)).C,
            n.SayC(l353),
            a.ESayC("angry", l354),
            r.ESayC("worry", l355),
            a.ESayC("worry", l356),
            a.ESayC("happy", l357),
            n.SayC(l358),
            r.ESayC("angry", l359),
            a.ESayC("cry", l360),
            a.SayC(l361),
            a.ESayC("happy", l362),
            Lazy(() => o.bgm.FadeOut()),
            a.MoveBy(V3(-1.5, 0), 1f).And(a.FadeTo(0, 1)),
            r.ESayC("worry", l363),
            r.ESayC("surprise", l364),
            r.ESayC("worry", l365),
            r.SayC(l366),
            r.SayC(l367),
            r.ESayC("happy", l368),
            m.ESayC("surprise", l369),
            r.ESayC("surprise", l370),
            r.ESayC("angry", l371),
            o.GetBGM("br.marisa"),
            m.MoveBy(V3(1.5, 0), 1).And(m.FadeTo(1, 1)),
            m.ESayC("happy", l372),
            r.ESayC("surprise", l373),
            m.SayC(l374),
            n.SayC(l375),
            r.ESayC("", l376),
            m.ESayC("worry", l377),
            m.ESayC("happy", l378),
            r.ESayC("worry", l379),
            m.SayC(l380),
            r.ESayC("", l381),
            r.ESayC("satisfied", l382, SpeakFlags.DontClearText),
            r.ESayC("happy", l383),
            m.ESayC("", l384),
            r.ESayC("", l385),
            r.ESayC("cry", l386),
            m.SayC(l387),
            r.ESayC("happy", l388),
            m.ESayC("worry", l389),
            r.ESayC("cry", l390),
            m.SayC(l391),
            m.ESayC("happy", l392),
            Lazy(() => o.md.Clear()),
            o.GetFootstep(),
            m.SetEmote(""),
            r.SetEmote(""),
            m.MoveTo(V3(-16, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(-12, 0), 2.8f))),
            Lazy(() => {
                m.Location.Value = V3(11, 0);
                r.Location.Value = V3(14, 0);
            }),
            m.MoveTo(V3(-2, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(2, 0), 3))),
            o.StopFootstep(),
            r.SayC(l393),
            m.ESayC("happy", l394),
            r.ESayC("worry", l395),
            m.ESayC("worry", l396),
            r.ESayC("surprise", l397),
            m.SayC(l398),
            r.ESayC("worry", l399),
            m.ESayC("happy", l400),
            m.ESayC("worry", l401, SpeakFlags.DontClearText),
            r.ESayC("happy", l402),
            m.ESayC("", l403),
            r.ESayC("worry", l404),
            m.ESayC("smug", l405),
            Lazy(() => o.md.Clear()),
            o.GetFootstep(),
            m.MoveTo(V3(-14, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(-11, 0), 3f))),
            Lazy(() => {
                m.Location.Value = V3(11, 0);
                r.Location.Value = V3(14, 0);
            }),
            m.MoveTo(V3(-2, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(2, 0), 3))),
            o.StopFootstep(),
            r.SayC(l406),
            m.SayC(l407),
            r.SayC(l408),
            m.ESayC("happy", l409),
            r.ESayC("surprise", l410),
            m.ESayC("smug", l411),
            r.ESayC("worry", l412),
            r.ESayC("happy", l413),
            m.ESayC("surprise", l414),
            m.MoveBy(V3(-2, 0), 0.8f),
            m.ESayC("happy", l415),
            n.Say(l416).And(r.MoveBy(V3(-2.5f, 0), 1)).Then(r.Disturb(r.ComputedLocation, JumpY(0.4f), 0.5f)).C,
            r.ESayC("", l417),
            m.ESayC("", l418),
            m.SayC(l419),
            n.Say(l420).And(
                m.MoveBy(V3(-0.4, 0), 0.4f)
                    .Then(vn.Wait(1))
                    .Then(m.MoveBy(V3(0.8, 0), 0.8f))
                    .Then(vn.Wait(1))
                    .Then(m.MoveBy(V3(-0.4, 0), 0.4f))
            ).And(
                r.MoveBy(V3(0, -1), 1f)
            ).C,
            r.MoveBy(V3(0, 1), 1f).And(r.EmoteSay("cry", l421)).C,
            m.ESayC("cry", l422),
            m.ESayC("happy", l423),
            Lazy(() => o.md.Clear()),
            r.SetEmote(""),
            o.GetFootstep(),
            m.MoveTo(V3(-15, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(-11, 0), 3f))),
            Lazy(() => {
                m.Location.Value = V3(11, 0);
                r.Location.Value = V3(14, 0);
            }),
            m.MoveTo(V3(-2, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(2, 0), 3))),
            o.StopFootstep(),
            m.ESayC("", l424),
            r.ESayC("happy", l425),
            m.ESayC("worry", l426),
            Lazy(() => o.md.Clear()),
            r.SetEmote(""),
            o.GetFootstep(),
            m.MoveTo(V3(-15, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(-11, 0), 3f))),
            Lazy(() => {
                m.Location.Value = V3(11, 0);
                r.Location.Value = V3(14, 0);
            }),
            m.SetEmote(""),
            r.SetEmote("worry"),
            o.StopFootstep(),
            vn.SFX("vn-yukari-power"),
            y.MoveBy(V3(0, -1), 0.8f).And(y.FadeTo(1, 0.8f)).And(y.EmoteSay("", l427)).C,
            y.EmoteSayC("cry", l428),
            y.ESayC("worry", l429),
            y.ESayC("happy", l430),
            Lazy(() => o.md.Clear()),
            vn.SFX("vn-yukari-power"),
            y.MoveBy(V3(0, 1), 0.8f).And(y.FadeTo(0, 0.8f)),
            o.GetFootstep(),
            m.MoveTo(V3(-2, 0), 3).And(vn.Wait(0.5).Then(r.MoveTo(V3(2, 0), 3))),
            o.StopFootstep(),
            r.ESayC("worry", l431),
            Lazy(() => o.bgm.FadeOut()),
            o.rg.DoTransition(new RenderGroupTransition.Fade(o.rgb, 3))
        );
        return default;
    });

    public static BoundedContext<Unit> VNScriptBlessedRain(DMKVNState vn) {
#if UNITY_EDITOR
        vn.DefaultLoadSkipUnit = true;
        //if (SceneIntermediary.IsFirstScene && vn.LoadTo is null) 
            //vn.LoadToLocation(new VNLocation("l77", new List<string>() {"TOP", "2:MORIYA"}));
#endif
        return _TopLevel(vn);
    }
}
}