using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BagoumLib.Cancellation;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.DMath;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Data;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using static SuzunoyaUnity.Helpers;
using static MiniProjects.VN.VNLines.Dialogue1;
using static BagoumLib.Mathematics.Bezier;
using static Danmokou.Core.DependencyInjection;
using Vector3 = System.Numerics.Vector3;

namespace MiniProjects.VN {

[Reflect]
public static class _VNCrimsonVermilion {
    private static void ActivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.SetAutoskip(Autoskip.ALL_INSTANTANEOUS);
#endif
    }
    private static void DeactivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.SetAutoskip(Autoskip.None);
#endif
    }
/*
    protected override async Task _RunScript(DMKVNState vn) {
        var md = vn.Add(new ADVDialogueBox());
        md.Alpha = 1;
        var chars = new SZYUCharacter[] {
            new Kaguya(),
            new Kasen(),
            new Komakusa(),
            new Kosuzu(),
            new Kurokoma(),
            new Kutaka(),
            new Marisa(),
            new Mayumi(),
            new Miko(),
            new Mokou(),
            new Reimu(),
            new Yachie(),
            new Youmu(),
            new Yukari(),
            new Yuuka()
        }.Select(x => vn.Add(x)).ToArray();
        for (int ii = 0; ii < chars.Length; ++ii) {
            chars[ii].Alpha = 1;
            chars[ii].Location.Value = V3(-7.5, 0) + (ii / (chars.Length - 1f)) * V3(15, 0);
        }
        for (int ii = 0; ii < chars.Length; ++ii) {
            await chars[ii].EmoteSayC("normal",
                "Vermilion dye is derived from cinnabar, an ore. Crimson dye is derived from the dried corpses of various scale insects, originally Kermes and later cochineal. Formally, the cochineal-based dye is called \"carmine\", but as it effectively replaced all production of the Kermes-based dye, we can draw a line through the two and call them both crimson.");
        }
    }
*/

    public static async Task VNScriptCrimsonVermilion1(DMKVNState vn) {
        var rg = (UnityRenderGroup) vn.DefaultRenderGroup;
        var rgb = new UnityRenderGroup(vn, "black", 1, false);
        AudioSource footstep;
        DMKVNState.RunningAudioTrackProxy bgm = null!;
        //Note: vn.SkipGuard/vn.lSFX works for normal SFX, they just need SkipGuard.
        //Looping SFX should work trivially *as long as you don't fade them out*.
        //Audio tracks work through RunningAudioTrackProxy.
        //In the future, make these both lazy, svp.
        void GetFootstep(string key = "vn-footstep-1") => footstep = SFXService.RequestSource(key)!;
        LazyAction GetBGM(string key) => new LazyAction(() => bgm = vn.RunBGM(key));
        
        var md = vn.Add(new ADVDialogueBox());
        var db = vn.Add(new ADVDialogueBox());
        db.Alpha = 1;
        db.RenderGroup.Value = rgb;
        var room = vn.Add(new ShrineRoomBG());
        var courtyard = vn.Add(new ShrineCourtyardBG());
        var town = vn.Add(new TownBG());
        var library = vn.Add(new LibraryBG());
        var field = vn.Add(new FieldBG());
        var farm = vn.Add(new FarmBG());
        var flower = vn.Add(new FlowerBG());
        var reimu = vn.Add(new Reimu());
        var yukari = vn.Add(new Yukari());
        var kasen = vn.Add(new Kasen());
        var marisa = vn.Add(new Marisa());
        var komakusa = vn.Add(new Komakusa());
        var youmu = vn.Add(new Youmu());
        var kosuzu = vn.Add(new Kosuzu());
        var kurokoma = vn.Add(new Kurokoma());
        var kutaka = vn.Add(new Kutaka());
        var mayumi = vn.Add(new Mayumi());
        var miko = vn.Add(new Miko());
        var yuuka = vn.Add(new Yuuka());
        var yachie = vn.Add(new Yachie());
        var kaguya = vn.Add(new Kaguya());
        var mokou = vn.Add(new Mokou());
        var lily = vn.Add(new YellowLily());
        lily.Location.Value = Vector3.Zero;
        var iris = vn.Add(new YellowIris());
        iris.Location.Value = V3(-1, 0);
        var lys = vn.Add(new FleurDeLys());
        lys.Location.Value = V3(1.7, 0);

        var chicken = new EmptyCharacter("Chicken", vn);

        reimu.Location.Value = V3(0, -7);
        await reimu.SetEmote("satisfied");

        //ActivateSkip(vn);

        await vn.Sequential(
            room.FadeTo(0.2f, 1f).And(reimu.FadeTo(1f, 1f), md.FadeTo(1f, 1f)),
            kasen.Say(l0, flags: SpeakFlags.Anonymous).C,
            yukari.Say(l1, flags: SpeakFlags.Anonymous).C,
            reimu.MoveTo(V3(0, -5.4), 1f).And(
                room.FadeTo(0.35f, 1f),
                reimu.Say(l2)
            ).C,
            kasen.Say(l3, flags: SpeakFlags.Anonymous).C,
            yukari.Say(l4, flags: SpeakFlags.Anonymous).C,
            reimu.MoveTo(V3(0, -3.8), 1f).And(
                room.FadeTo(0.5f, 1f),
                reimu.Say(l5)
            ).C,
            kasen.Say(l6, flags: SpeakFlags.Anonymous).C,
            yukari.Say(l7, flags: SpeakFlags.Anonymous).C,
            kasen.Say(l8, flags: SpeakFlags.Anonymous).C,
            yukari.Say(l9, flags: SpeakFlags.Anonymous).C,
            reimu.SetEmote("surprise"),
            vn.lSFX("vn-impact-1"),
            reimu.MoveTo(V3(0, 0), 0.7f, CBezier(.54, .77, .63, 1.47)).And(
                room.FadeTo(1f, 0.4f),
                reimu.Say(l10)
            ).C);
        GetFootstep();
        await vn.Sequential(
            reimu.MoveTo(V3(-12, 0), 1.2f, CBezier(.37, -0.37, .81, .93)).And(
                vn.Wait(0.2f).Then(rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)))
            ),
            GetBGM("s02-2"),
            vn.Wait(0.5f),
            Lazy(() => {
                kasen.Location.Value = V3(-5, 0);
                kasen.Alpha = 1;
                yukari.Location.Value = V3(-2, 0);
                yukari.Alpha = 1;
                reimu.Location.Value = V3(12, 0);
                room.Alpha = 0;
                courtyard.Alpha = 1;
            }),
            kasen.SetEmote("worry"),
            reimu.MoveTo(V3(3, 0), 1.2f, CBezier(.17, .26, .56, 1.37)).And(
                rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f))),
            Lazy(() => footstep.Stop()),
            vn.Wait(0.4f),
            kasen.Say(l11).C,
            reimu.SetEmote("angry"),
            reimu.Say(l12).C,
            kasen.Say(l13).C,
            yukari.SetEmote("surprise"),
            new JointCharacter(reimu, yukari).Say(l14).C,
            yukari.SetEmote("worry"),
            yukari.Say(l15).C,
            yukari.Say(l16).C,
            kasen.Say(l17).C,
            reimu.SetEmote("worry"),
            reimu.Say(l18).C,
            yukari.Say(l19).C,
            kasen.SetEmote("surprise"),
            kasen.Say(l20).C,
            yukari.Say(l21).C,
            reimu.SetEmote("surprise"),
            reimu.Say(l22).C,
            yukari.Say(l23).C,
            kasen.SetEmote("normal"),
            kasen.Say(l24),
            vn.Wait(0.5f),
            kasen.SetEmote("worry"),
            kasen.AlsoSay(l24_1).C,
            yukari.SetEmote("happy"),
            yukari.Say(l25).C,
            reimu.SetEmote("worry"),
            reimu.Say(l26).C,
            yukari.SetEmote("cry"),
            yukari.Say(l27).C,
            reimu.Say(l28).C,
            kasen.Say(l29).C,
            kasen.SetEmote("normal"),
            kasen.SayC(l30),
            reimu.SayC(l31),
            yukari.SetEmote("normal"),
            yukari.SayC(l32),
            yukari.SetEmote("happy"),
            yukari.SayC(l33),
            reimu.SetEmote("normal"),
            reimu.SayC(l34),
            yukari.SetEmote("normal"),
            yukari.SayC(l35),
            kasen.EmoteSayC("worry", l36),
            yukari.EmoteSayC("worry", l37),
            kasen.EmoteSayC("surprise", l38),
            yukari.SayC(l39),
            reimu.EmoteSayC("surprise", l40),
            yukari.EmoteSayC("smug", l41),
            reimu.EmoteSayC("worry", l42),
            kasen.EmoteSayC("worry", l43),
            reimu.EmoteSayC("angry", l44),
            kasen.SayC(l45),
            yukari.SayC(l46),
            reimu.EmoteSayC("worry", l47),
            yukari.EmoteSayC("cry", l48),
            kasen.EmoteSayC("normal", l49),
            reimu.SayC(l50),
            yukari.EmoteSayC("", l51),
            marisa.SayC(l52, flags: SpeakFlags.Anonymous),
            Lazy(() => {
                marisa.Location.Value = V3(5, 12);
            }),
            marisa.MoveTo(V3(5, 0), 1f, CBezier(.4, .62, .45, 1.24))
                .And(marisa.FadeTo(1f, 0.5f))
                .And(vn.Wait(0.4f).Then(vn.aSFX("vn-impact-1")))
                .And(vn.Wait(0.4f).Then(reimu.MoveTo(V3(2, 0), 0.5f))),
            reimu.SetEmote("surprise"),
            vn.Wait(0.4f),
            yukari.SayC(l53),
            marisa.Say(l54),
            vn.Wait(0.6f),
            marisa.SetEmote("happy"),
            marisa.AlsoSayN(l55).C,
            reimu.SayC(l56),
            marisa.EmoteSayC("", l57),
            marisa.EmoteSayC("happy", l58),
            reimu.EmoteSayC("emb1", l59),
            marisa.SayC(l60),
            yukari.SayC(l61),
            yukari.EmoteSayC("smug", l62),
            vn.lSFX("vn-yukari-power"),
            yukari.MoveBy(V3(0, 1), 0.8f).And(
                yukari.FadeTo(0f, 0.8f)
            ),
            kasen.EmoteSayC("happy", l63),
            vn.lSFX("vn-yukari-power"),
            kasen.MoveBy(V3(0, 1), 0.8f).And(
                kasen.FadeTo(0f, 0.8f)
            ),
            vn.Wait(0.4f),
            marisa.EmoteSayC("", l64),
            reimu.SayC(l65),
            Lazy(() => bgm.FadeOut()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            db.Say("After eating brunch, Reimu and Marisa head to the human village...").C,
            Lazy(() => {
                courtyard.Alpha = 0;
                town.Alpha = 1;
                yachie.Location.Value = V3(-7, 0);
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            marisa.SayC(l66_1),
            Lazy(() => GetFootstep()),
            GetBGM("s02-7"),
            yachie.MoveBy(V3(2, 0), 1.5f).And(yachie.FadeTo(1f, 1.5f))
                .Then(yachie.RotateTo(V3(0, 180), 1f))
                .Then(vn.Wait(0.2f))
                .Then(yachie.RotateTo(V3(0, 0), 1f))
                .Then(vn.Wait(0.4f))
                .Then(yachie.MoveBy(V3(1.3, 0), 1.2f).And(yachie.FadeTo(0f, 1.2f)))
                .Then(() => footstep.Stop()),
            vn.Wait(0.4f),
            reimu.EmoteSayC("worry", l67),
            marisa.EmoteSayC("worry", l68),
            reimu.EmoteSayC("", l69),
            Lazy(() => GetFootstep()),
            reimu.MoveBy(V3(-1, 0), 1f).And(reimu.FadeTo(0f, 1f))
                .And(vn.Wait(0.4f).Then(marisa.MoveBy(V3(-1.4, 0), 1f).And(marisa.FadeTo(0f, 1f))))
                .Then(() => footstep.Stop()),
            Lazy(() => db.Clear()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            vn.Wait(0.5),
            Lazy(() => {
                town.Alpha = 0;
                library.Alpha = 1;
                yachie.Location.Value = V3(-12, 0);
                yachie.Alpha = 1;
                kosuzu.Location.Value = V3(6, 0);
                kosuzu.Alpha = 1;
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                reimu.Location.Value = V3(-9, 0);
                marisa.Location.Value = V3(-6, 0);
                komakusa.Location.Value = V3(-3, 0);
                kaguya.Location.Value = V3(-2, 0);
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            vn.lSFX("vn-suzunaan-bell"),
            Lazy(() => GetFootstep()),
            //Lazy(() => DeactivateSkip(vn)),
            yachie.MoveTo(V3(2.5, 0), 4.5f).Then(() => footstep.Stop()),
            GetBGM("s02-6"),
            yachie.Say(l71).C,
            kosuzu.EmoteSayC("happy", l72),
            kosuzu.MoveBy(V3(1.3, 0), 1).And(kosuzu.FadeTo(0, 1)).And(
                vn.Wait(0.7f).Then(reimu.MoveBy(V3(3, 0), 2).And(reimu.FadeTo(1, 2))),
                marisa.MoveBy(V3(3, 0), 2).And(marisa.FadeTo(1, 2))
            ),
            reimu.SayC(l73),
            marisa.EmoteSayC("worry", l74),
            reimu.EmoteSayC("satisf", l75),
            marisa.SayC(l76),
            reimu.EmoteSayC("surprise", l77),
            marisa.Say(l78),
            vn.Wait(0.5),
            marisa.SetEmote("surprise"),
            marisa.AlsoSayN(l78_1).C,
            reimu.SetEmote("surprise"),
            reimu.MoveBy(V3(-0.5, 0), 0.5f).And(
                vn.Wait(0.2).Then(marisa.MoveBy(V3(-0.8, 0), 0.5f)),
                vn.Wait(0.3).Then(vn.aSFX("vn-suzunaan-bell"))
                    .Then(komakusa.MoveTo(V3(-0.5, 0), 2f).And(komakusa.FadeTo(1, 2)))
            ),
            komakusa.EmoteSayC("smug", l79),
            yachie.SetEmote("surprise"),
            yachie.RotateTo(V3(0, -180), 0.6f, Easers.EOutSine).And(
                yachie.Say(l80),
                yachie.MoveBy(V3(0.5, 0), 0.6f)
            ),
            vn.Wait(0.5f),
            yachie.SetEmote("smug"),
            yachie.AlsoSayN(l80_1).C,
            komakusa.SayC(l81),
            yachie.SayC(l82),
            kosuzu.MoveBy(V3(-1, 0), 1).And(kosuzu.FadeTo(1, 1)).Then(kosuzu.Say(l83)),
            vn.Wait(0.3f),
            kosuzu.SetEmote("surprise"),
            kosuzu.AlsoSay(l83_1).C,
            reimu.EmoteSayC("angry", l84),
            yachie.EmoteSayC("angry", l85),
            reimu.EmoteSayC("surprise", l86),
            marisa.EmoteSayC("angry", l87),
            reimu.EmoteSay("emb1", l88),
            vn.Wait(0.5),
            reimu.SetEmote("satisf"),
            reimu.AlsoSay(l88_1).C,
            komakusa.EmoteSayC("angry", l89),
            reimu.EmoteSayC("worry", l90),
            marisa.EmoteSayC("worry", l91),
            komakusa.ESayC("", l92),
            yachie.ESayC("smug", l93),
            komakusa.ESayC("smug", l94),
            yachie.ESayC("angry", l95),
            reimu.ESayC("surprise", l96),
            marisa.SayC(l97),
            reimu.ESayC("emb1", l98),
            komakusa.ESayC("worry", l99),
            yachie.EmoteSayC("worry", l100),
            vn.lSFX("vn-suzunaan-bell"),
            kaguya.MoveTo(V3(1.7, 0), 2f).And(kaguya.FadeTo(1, 2)).And(
                komakusa.MoveBy(V3(-0.7, 0), 1f),
                yachie.MoveBy(V3(0.7, 0), 1)
            ),
            vn.Wait(0.5f),
            kaguya.SayC(l102),
            kosuzu.EmoteSayC("worry", l103),
            kaguya.EmoteSayC("happ", l104),
            yachie.EmoteSayC("", l105),
            kaguya.EmoteSayC("", l106),
            kaguya.ESayC("happy", l106_1),
            kaguya.ESayC("", l107),
            kaguya.MoveBy(V3(-2, 0), 2f).And(kaguya.FadeTo(0, 2f)).And(
                vn.Wait(1.5).Then(vn.aSFX("vn-suzunaan-bell")),
                vn.Wait(1.5).Then(komakusa.MoveBy(V3(0.7, 0), 1f)),
                vn.Wait(0.6).Then(yachie.MoveBy(V3(-0.7, 0), 1))
            ),
            vn.Wait(0.5),
            komakusa.EmoteSayC("worry", l108),
            yachie.ESayC("angry", l109),
            komakusa.SayC(l110),
            yachie.SayC(l111),
            komakusa.ESayC("", l112),
            yachie.EmoteSayC("smug", l113),
            Lazy(() => GetFootstep()),
            yachie.MoveTo(V3(-15, 0), 2.5f, CBezier(.54, -.3, .62, .75)).And(
                vn.Wait(2).Then(vn.aSFX("vn-suzunaan-bell")).Then(() => footstep.Stop()),
                kosuzu.EmoteSay("surprise", l114)).C,
            komakusa.MoveBy(V3(2, 0), 2).And(komakusa.Say(l115)).C,
            komakusa.ESayC("surprise", l116),
            reimu.ESayC("worry", l117),
            komakusa.ESayC("normal", l118),
            komakusa.Say(l119).And(
                komakusa.MoveBy(V3(-2, 0), 2),
                komakusa.FadeTo(0, 2),
                vn.Wait(1.5).Then(vn.aSFX("vn-suzunaan-bell"))
            ),
            reimu.SayC(l120),
            marisa.ESayC("worry", l121),
            reimu.SayC(l122),
            Lazy(() => db.Clear()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            Lazy(() => bgm.FadeOut()),
            db.Say("Reimu and Marisa leave Suzunaan, only to wander aimlessly around the outskirts of the village...")
                .C,
            Lazy(() => {
                library.Alpha = 0;
                field.Alpha = 1;
                kurokoma.Location.Value = V3(-3.5, 0);
                kurokoma.Alpha = 1;
                miko.Alpha = 1;
                miko.Location.Value = V3(-2, 12);
                kosuzu.Alpha = 0;
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                reimu.Location.Value = V3(12, 0);
                marisa.Location.Value = V3(15, 0);
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            kurokoma.SayC(l124),
            new EmptyCharacter("Equestrian", vn).SayC(l125),
            Lazy(() => GetFootstep()),
            reimu.MoveTo(V3(2, 0), 3)
                .And(marisa.MoveTo(V3(5, 0), 3.5f))
                .Then(() => footstep.Stop()),
            GetBGM("s02-7"),
            reimu.EmoteSay("surprise", l126).C,
            kurokoma.RotateTo(V3(0, 180), 0.7f).And(kurokoma.EmoteSay("happy", l127)).C,
            reimu.EmoteSay("worry", l128),
            vn.Wait(0.4f),
            reimu.SetEmote("angry"),
            reimu.AlsoSayN(l128_1).C,
            kurokoma.ESayC("worry", l129),
            reimu.EmoteSayC("happy", l130),
            marisa.ESayC("worry", l131),
            reimu.ESayC("Surprise", l132),
            marisa.SayC(l133),
            reimu.ESayC("emb1", l134),
            reimu.ESayC("angry", l135),
            marisa.ESayC("angry", l136),
            kurokoma.ESayC("angry", l137),
            kurokoma.ESayC("smug", l138),
            reimu.Say(l139).And(
                reimu.MoveBy(V3(-0.3, 0), 1),
                vn.Wait(0.4).Then(marisa.MoveBy(V3(-0.3, 0), 1))
            ).C,
            kurokoma.SayC(l140),
            marisa.EmoteSay("smug", l141).And(
                reimu.MoveBy(V3(-0.3, 0), 1),
                vn.Wait(0.4).Then(marisa.MoveBy(V3(-0.3, 0), 1))
            ).C,
            kurokoma.SayC(l142),
            reimu.EmoteSay("worry", l143).And(
                reimu.MoveBy(V3(-0.3, 0), 1),
                vn.Wait(0.4).Then(marisa.MoveBy(V3(-0.3, 0), 1))
            ).C,
            kurokoma.SayC(l144),
            kurokoma.AlsoSayN(l145).C,
            Lazy(() => bgm.FadeOut()),
            kurokoma.SetEmote("surprise"),
            reimu.SetEmote("surprise"),
            marisa.SetEmote("surprise"),
            miko.EmoteSayC("angry", l147, SpeakFlags.Anonymous),
            reimu.SayC(l148),
            GetBGM("s02-4"),
            miko.MoveTo(V3(-2, 0), 1f, CBezier(.4, .62, .45, 1.24))
                .And(vn.Wait(0.4f).Then(vn.aSFX("vn-impact-1")))
                .And(vn.Wait(0.2f).Then(
                    kurokoma.MoveTo(V3(-5.3, 0), 0.8f).And(
                        kurokoma.Disturb(kurokoma.Location, JumpY(0.5f), 0.4f),
                        reimu.Disturb(reimu.Location, JumpY(0.5f), 0.4f),
                        vn.Wait(0.1).Then(marisa.Disturb(marisa.Location, JumpY(0.5f), 0.4f)),
                        
                        reimu.MoveBy(V3(0.9, 0), 1),
                        marisa.MoveBy(V3(0.9, 0), 1))
                )),
            vn.Wait(0.4),
            miko.SayC(l149),
            reimu.ESayC("happy", l150),
            kurokoma.ESayC("happy", l151),
            miko.ESayC("worry", l152),
            miko.ESayC("angry", l153),
            reimu.ESayC("angry", l154),
            marisa.ESayC("angry", l155),
            kurokoma.SayC(l156),
            miko.ESayC("surprise", l157),
            kurokoma.ESayC("worry", l158),
            miko.ESayC("", l159),
            kurokoma.ESayC("surprise", l160),
            marisa.ESayC("worry", l161),
            miko.ESayC("happy", l162),
            kurokoma.EmoteSay("happy", l163).And(
                vn.Wait(0.9).Then(kurokoma.MoveBy(V3(0.3, 0), 0.8f, CBezier(.28, .82, .37, 1.92)))).C,
            miko.EmoteSay("", l164),
            vn.Wait(0.5),
            miko.SetEmote("emb1"),
            miko.AlsoSay(l164_1).C,
            kurokoma.ESayC("", l165),
            Lazy(() => bgm.FadeOut()),
            miko.EmoteSay("surprise", l166).And(
                kurokoma.MoveBy(V3(3, 12), 1.6f, CBezier(.37, -.41, .85, .68)),
                vn.Wait(0.6).Then(vn.aSFX("vn-impact-1")),
                vn.Wait(0.8).Then(miko.MoveBy(V3(2, 12), 1f, Easers.EOutSine))
            ).C,
            reimu.ESayC("worry", l167),
            marisa.ESayC("worry", l168),
            reimu.SayC(l169),
            Lazy(() => db.Clear()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            vn.Wait(0.5f),
            Lazy(() => {
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                mokou.Location.Value = V3(-4, 0);
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            reimu.SayC(l171),
            marisa.SayC(l172),
            reimu.ESayC("worry", l173),
            marisa.ESayC("worry", l174),
            reimu.SayC(l175),
            marisa.SayC(l176),
            reimu.ESayC("", l177),
            marisa.ESayC("", l178),
            reimu.ESayC("emb1", l179),
            marisa.ESayC("smug", l180),
            marisa.ESayC("", l181),
            mokou.SayC(l182, flags: SpeakFlags.Anonymous),
            GetBGM("s02-3"),
            mokou.MoveTo(V3(-3, 0), 1.4f).And(mokou.FadeTo(1, 1.4f)),
            vn.Wait(0.5),
            mokou.SayC(l183),
            reimu.ESayC("", l184),
            mokou.ESayC("surprise", l185),
            reimu.ESayC("worry", l186),
            mokou.ESayC("worry", l187),
            marisa.ESayC("worry", l188),
            mokou.SayC(l189),
            reimu.ESayC("surprise", l190),
            mokou.ESayC("angry", l191),
            mokou.SayC(l192),
            mokou.ESayC("surprise", l193),
            mokou.ESayC("worry", l194),
            marisa.ESayC("surprise", l195),
            mokou.ESayC("", l196),
            mokou.SayC(l197),
            reimu.ESayC("worry", l198),
            mokou.SayC(l199),
            reimu.ESayC("", l200),
            mokou.ESayC("worry", l201),
            marisa.ESayC("worry", l202),
            mokou.ESayC("", l203),
            reimu.ESayC("emb1", l204),
            mokou.ESayC("angry", l205),
            marisa.ESayC("", l206),
            mokou.EmoteSay("worry", l207).And(
                vn.Wait(1.5).Then(
                    mokou.MoveBy(V3(-1.5, 0), 1.4f).And(
                        mokou.FadeTo(0, 1.4f)))
            ).C,
            reimu.ESayC("worry", l208),
            marisa.ESayC("smug", l209),
            reimu.SayC(l210),
            Lazy(() => db.Clear()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            db.Say("Reimu and Marisa proceed down the path away from the village...").C,
            Lazy(() => {
                field.Alpha = 0;
                farm.Alpha = 1;
                kutaka.Location.Value = V3(-3.5, 0);
                kutaka.Alpha = 1;
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                reimu.Location.Value = V3(12, 0);
                marisa.Location.Value = V3(15, 0);
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            chicken.SayC(l212),
            Lazy(() => GetFootstep()),
            kutaka.EmoteSay("happy", l213).And(
                reimu.MoveTo(V3(2, 0), 3).And(marisa.MoveTo(V3(5, 0), 3.5f))
                    .Then(() => footstep.Stop())
            ).C,
            reimu.EmoteSayC("worry", l214),
            kutaka.EmoteSayC("", l215),
            reimu.SayC(l216),
            kutaka.EmoteSayC("cry", l217),
            marisa.SayC(l218),
            reimu.EmoteSayC("surprise", l219),
            reimu.EmoteSayC("", l220),
            kutaka.EmoteSayC("happy", l221),
            marisa.EmoteSayC("worry", l222),
            kutaka.SayC(l223),
            reimu.EmoteSayC("worry", l224),
            kutaka.EmoteSayC("", l225),
            reimu.SayC(l226),
            kutaka.EmoteSayC("surprise", l227),
            marisa.SayC(l228),
            kutaka.EmoteSayC("", l229),
            reimu.SayC(l230),
            kutaka.SayC(l231),
            chicken.SayC(l232),
            kutaka.EmoteSayC("happy", l233),
            reimu.EmoteSayC("", l234),
            kutaka.EmoteSayC("", l235),
            reimu.ESayC("worry", l236),
            marisa.ESayC("smug", l237),
            kutaka.ESayC("surprise", l238),
            marisa.ESayC("worry", l239),
            marisa.SayC(l239_1),
            marisa.ESayC("smug", l240),
            reimu.ESayC("emb1", l241),
            kutaka.ESayC("angry", l242),
            kutaka.SayC(l243),
            chicken.SayC(l244),
            kutaka.ESayC("worry", l245),
            chicken.SayC(l246),
            kutaka.SayC(l247),
            reimu.ESayC("worry", l248),
            marisa.ESayC("worry", l249),
            kutaka.SayC(l250),
            marisa.SayC(l251),
            reimu.SayC(l252),
            marisa.SayC(l253),
            marisa.EmoteSayC("", l254),
            reimu.EmoteSayC("", l255),
            marisa.SayC(l256),
            kutaka.ESayC("happy", l257),
            chicken.SayC(l258),
            Lazy(() => db.Clear()),
            Lazy(() => bgm.FadeOut()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            db.Say("Reimu and Marisa head back to the village...").C,
            Lazy(() => {
                farm.Alpha = 0;
                flower.Alpha = 1;
                kutaka.Alpha = 0;
                mayumi.Alpha = 1;
                mayumi.Location.Value = V3(-5, 0);
                mayumi.Emote.Value = "worry";
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                reimu.Location.Value = V3(12, 0);
                marisa.Location.Value = V3(15, 0);
                yuuka.Location.Value = V3(7, 0);
                youmu.Location.Value = V3(-12, 0);
                youmu.Alpha = 1;
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            mayumi.SayC(l260),
            mayumi.SayC(l261),
            Lazy(() => GetFootstep()),
            mayumi.Say(l262).And(
                reimu.MoveTo(V3(-1.5, 0), 4).And(marisa.MoveTo(V3(1.5, 0), 4.5f))
                    .Then(() => footstep.Stop())
            ).C,
            marisa.EmoteSayC("happy", l263),
            mayumi.EmoteSayC("surprise", l264),
            mayumi.EmoteSay("smug", l265),
            vn.Wait(0.6f),
            mayumi.SetEmote("worry"),
            mayumi.AlsoSay(l265_1).C,
            reimu.SayC(l266),
            GetBGM("s02-4"),
            marisa.ESayC("worry", l267),
            reimu.ESayC("surprise", l268),
            reimu.ESayC("angry", l268_1),
            yuuka.SayC(l269, flags: SpeakFlags.Anonymous),
            yuuka.MoveBy(V3(-2, 0), 1.4f).And(yuuka.FadeTo(1, 1.4f)).Then(yuuka.EmoteSay("happy", l270)).C,
            yuuka.ESayC("worry", l271),
            marisa.ESayC("cry", l272),
            reimu.ESayC("surprise", l273),
            yuuka.Say(l274),
            vn.Wait(0.5f),
            yuuka.SetEmote("happy"),
            yuuka.AlsoSayN(l274_1).C,
            marisa.ESayC("happy", l275),
            yuuka.SayC(l276),
            yuuka.ESayC("smug", l277),
            marisa.EmoteSay("emb1", l278).And(
                marisa.Disturb(marisa.Location, JumpY(0.5f), 0.4f)).C,
            reimu.ESayC("worry", l279),
            mayumi.ESayC("worry", l280),
            reimu.ESayC("emb1", l281),
            yuuka.SayC(l282),
            marisa.ESayC("surprise", l283),
            marisa.ESayC("emb1", l284),
            yuuka.SayC(l285),
            marisa.SayC(l286),
            yuuka.ESayC("happy", l287),
            yuuka.ESayC("", l288),
            mayumi.ESayC("cry", l289),
            reimu.ESayC("worry", l290),
            yuuka.ESayC("happy", l291),
            reimu.ESayC("angry", l292),
            yuuka.ESayC("worry", l293),
            yuuka.ESayC("happy", l294),
            yuuka.ESayC("smug", l295),
            reimu.ESayC("worry", l296),
            mayumi.EmoteSay("worry", l297).And(mayumi.MoveBy(V3(0.6, 0), 1)).C,
            reimu.EmoteSay("emb1", l298).And(
                reimu.Disturb(reimu.Location, JumpY(0.5f), 0.4f)).C,
            marisa.SayC(l299),
            yuuka.SayC(l300),
            reimu.ESayC("angry", l301),
            marisa.ESayC("surprise", l302),
            yuuka.EmoteSay("worry", l303).And(
                yuuka.Disturb(yuuka.Location, JumpX(-1f), 3f)).C,
            reimu.ESayC("worry", l304),
            reimu.SayC(l304_1),
            yuuka.ESayC("happy", l305),
            reimu.ESayC("angry", l306),
            reimu.SayC(l307),
            reimu.EmoteSay("surprise", l308).And(
                reimu.RotateTo(V3(0, 720), 1)
            ).C,
            mayumi.EmoteSay("worry", l309).And(
                mayumi.MoveBy(V3(-1, 0), 4f),
                mayumi.Disturb(mayumi.Location, JumpNX(-0.4f, 2), 3f)
            ).C,
            yuuka.ESayC("", l310),
            mayumi.ESayC("surprise", l311),
            yuuka.ESayC("worry", l312),
            mayumi.ESayC("worry", l313),
            marisa.ESayC("worry", l314),
            yuuka.ESayC("happy", l315),
            mayumi.EmoteSay("happy", l316),
            vn.Wait(0.5),
            mayumi.SetEmote("surprise"),
            mayumi.AlsoSay(l316_1).And(
                mayumi.MoveBy(V3(-1, 0), 0.8f),
                mayumi.FadeTo(0, 0.8f)).C,
            Lazy(() => GetFootstep()),
            youmu.MoveTo(V3(-5, 0), 2f).Then(() => footstep.Stop()),
            vn.Wait(0.3).Then(youmu.EmoteSay("happy", l317)).C,
            reimu.EmoteSayC("", l318),
            yuuka.ESayC("worry", l319),
            youmu.ESayC("", l320),
            yuuka.ESayC("surprise", l321),
            yuuka.ESayC("worry", l322),
            youmu.ESayC("happy", l323),
            yuuka.SayC(l324),
            yuuka.SayC(l325),
            youmu.EmoteSay("surprise", l326).And(
                youmu.Disturb(youmu.Location, t =>
                    new Vector3(M.PolarToXY(M.Sine(1, 0.4f, 12 * M.EOutSine(t)), 600 * M.EOutSine(t))._(), 0), 3)
            ).C,
            youmu.EmoteSayC("angry", l326_1),
            youmu.SayC(l326_1_1),
            yuuka.SayC(l326_2),
            youmu.EmoteSayC("surprise", l327),
            yuuka.SayC(l328),
            youmu.EmoteSayC("worry", l329),
            yuuka.EmoteSayC("worry", l330),
            youmu.SayC(l331),
            yuuka.SayC(l332),
            youmu.EmoteSayC("cry", l333),
            yuuka.SayC(l334),
            youmu.SayC(l335),
            yuuka.EmoteSayC("surprise", l336),
            youmu.SayC(l337),
            yuuka.EmoteSayC("worry", l338),
            marisa.ESayC("worry", l339),
            youmu.ESayC("surprise", l340),
            yuuka.EmoteSayC("angry", l341),
            youmu.ESayC("angry", l342),
            yuuka.EmoteSay("worry", l343).And(
                vn.Wait(1).Then(yuuka.MoveBy(V3(1.3, 0), 1).And(yuuka.FadeTo(0, 1)))
            ).C,
            youmu.ESayC("cry", l344),
            marisa.ESayC("", l345),
            youmu.ESayC("", l346),
            reimu.ESayC("worry", l347),
            youmu.ESayC("surprise", l348),
            yuuka.MoveBy(V3(-1.3, 0), 1).And(yuuka.FadeTo(1, 1)).Then(
                yuuka.EmoteSay("", l349).And(lily.FadeTo(1, 1.5f))).C,
            youmu.SayC(l350),
            yuuka.EmoteSay("happy", l351).And(
                lily.FadeTo(0, 1f),
                iris.FadeTo(1, 1.5f),
                vn.Wait(1).Then(lys.FadeTo(1, 1.5f))
            ).C,
            youmu.EmoteSay("worry", l352).And(
                iris.FadeTo(0, 1.5f),
                vn.Wait(1).Then(lys.FadeTo(0, 1.5f))
            ).C,
            yuuka.ESayC("worry", l353),
            youmu.ESayC("surprise", l354),
            yuuka.Say(l355),
            vn.Wait(0.4),
            yuuka.SetEmote("smug"),
            yuuka.AlsoSay(l355_1).C,
            marisa.ESayC("", l356),
            yuuka.SayC(l357),
            reimu.ESayC("", l358),
            yuuka.SayC(l359),
            reimu.ESayC("happy", l360),
            yuuka.ESayC("happy", l361),
            marisa.ESayC("emb1", l362),
            reimu.ESayC("angry", l363),
            yuuka.ESayC("worry", l364),
            youmu.ESayC("worry", l365),
            yuuka.ESayC("", l366),
            yuuka.SayC(l366_1),
            youmu.ESayC("", l367),
            youmu.SayC(l367_1),
            youmu.SayC(l367_2),
            marisa.ESayC("happy", l368),
            reimu.ESayC("emb1", l369),
            yuuka.ESayC("", l370),
            youmu.ESayC("worry", l371),
            yuuka.ESayC("happy", l372),
            youmu.SayC(l373),
            Lazy(() => GetFootstep()),
            youmu.RotateTo(V3(0, 180), 0.4f).And(
                youmu.MoveTo(V3(-12, 0), 2f)).Then(() => footstep.Stop()),
            vn.Wait(0.3).Then(yuuka.EmoteSay("", l374)).C,
            mayumi.MoveBy(V3(1, 0), 1f).And(
                mayumi.FadeTo(1, 1f),
                mayumi.EmoteSay("worry", l375)).C,
            yuuka.SayC(l376),
            mayumi.ESayC("emb1", l377),
            yuuka.ESayC("happy", l378),
            mayumi.Say(l379).And(
                vn.Wait(0.5).Then(
                    mayumi.MoveBy(V3(-2, 0), 2f).And(
                        mayumi.FadeTo(0, 2f))
                )).C,
            reimu.ESayC("", l380),
            yuuka.ESayC("worry", l381),
            reimu.ESayC("surprise", l382),
            yuuka.ESayC("happy", l383),
            Lazy(() => db.Clear()),
            Lazy(() => bgm.FadeOut()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            db.Say("Reimu and Marisa return to the shrine to end their uneventful journey...").C,
            Lazy(() => {
                flower.Alpha = 0;
                room.Alpha = 1;
                yuuka.Alpha = 0;
                reimu.Emote.Value = "";
                marisa.Emote.Value = "";
                reimu.Location.Value = V3(-1, 0);
                marisa.Location.Value = V3(3, 0);
                md.Clear();
            }),
            rgb.DoTransition(new RenderGroupTransition.Fade(rg, 1f)),
            marisa.SayC(l385),
            GetBGM("s02-2"),
            reimu.ESayC("emb1", l386),
            marisa.ESayC("happy", l387),
            reimu.SayC(l388),
            marisa.ESayC("emb1", l389),
            reimu.SayC(l390),
            reimu.SayC(l391),
            vn.lSFX("vn-yukari-power"),
            yukari.MoveBy(V3(0, -1), 0.8f).And(
                yukari.FadeTo(1f, 0.8f),
                yukari.EmoteSay("", l392),
                reimu.MoveTo(V3(1, 0), 0.6f),
                vn.Wait(0.3).Then(marisa.MoveTo(V3(4.5, 0), 0.6f))
            ).C,
            vn.lSFX("vn-yukari-power"),
            kasen.MoveBy(V3(0, -1), 0.8f).And(
                kasen.FadeTo(1f, 0.8f),
                kasen.EmoteSay("happy", l393)
            ),
            vn.Wait(0.4),
            kasen.SetEmote("worry"),
            kasen.AlsoSay(l393_1).C,
            reimu.ESayC("surprise", l394),
            yukari.ESayC("smug", l395),
            reimu.SayC(l396),
            yukari.SayC(l397),
            reimu.ESayC("worry", l398),
            yukari.EmoteSay("surprise", l399).And(
                yukari.MoveBy(V3(-1,0), 0.8f),
                vn.Wait(0.3).Then(kasen.MoveBy(V3(-0.7, 0), 0.7f))
            ).C,
            kasen.ESayC("angry", l400),
            yukari.ESayC("angry", l401),
            yukari.ESayC("smug", l402),
            reimu.ESayC("emb1", l403),
            marisa.SayC(l404),
            yukari.SayC(l405),
            kasen.ESayC("worry", l406),
            reimu.SayC(l407),
            yukari.ESayC("surprise", l408),
            kasen.ESayC("surprise", l409),
            marisa.EmoteSay("surprise", l410),
            vn.Wait(1),
            marisa.SetEmote("emb1"),
            marisa.AlsoSayN(l411).C,
            yukari.SayC(l412),
            marisa.SayC(l413), 
            reimu.ESayC("cry", l414),
            yukari.EmoteSay("happy", l415).And(
                yukari.MoveBy(V3(1, 0), 1),
                yukari.RotateTo(V3(0, 720), 1.4f)
                ).C,
            yukari.EmoteSayC("smug", l416),
            yukari.EmoteSayC("happy", l417),
            kasen.SayC(l418),
            yukari.EmoteSayC("smug", l419),
            vn.lSFX("vn-yukari-power"),
            yukari.EmoteSay("happy", l420).And(
                yukari.MoveBy(V3(0, 1), 0.8f).And(
                    yukari.FadeTo(0f, 0.8f)
                )).C,
            kasen.ESayC("worry", l421),
            kasen.SayC(l422),
            reimu.ESayC("angry", l423),
            kasen.ESayC("", l424),
            kasen.ESayC("happy", l425),
            kasen.ESayC("worry", l426),
            reimu.ESayC("emb1", l427),
            kasen.EmoteSay("", l428).And(
                vn.Wait(1).Then(
                    kasen.MoveBy(V3(-1.4, 0), 1.4f).And(
                        kasen.FadeTo(0, 1.4f)))
            ).C,
            reimu.SayC(l429),
            marisa.SayC(l430),
            reimu.Say(l431).And(reimu.MoveBy(V3(0.8, 0), 1)).C,
            marisa.Say(l432).And(marisa.MoveBy(V3(-0.8, 0), 1)).C,
            Lazy(() => db.Clear()),
            Lazy(() => bgm.FadeOut()),
            rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)),
            db.Say("The End. Thanks for reading!").C
        );
    }
}
}