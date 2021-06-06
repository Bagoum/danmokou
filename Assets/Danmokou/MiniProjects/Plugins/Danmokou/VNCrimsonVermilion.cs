using System.Threading.Tasks;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.Entities;
using SuzunoyaUnity;
using SuzunoyaUnity.Derived;
using SuzunoyaUnity.Rendering;
using static SuzunoyaUnity.Helpers;
using static MiniProjects.VN.VNLines.Dialogue1;
using static BagoumLib.Mathematics.Bezier;

namespace MiniProjects.VN {

public class _VNCrimsonVermilion : VNBaseScript {
    protected void ActivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.ForceSkip += 1;
#endif
    }
    protected void DectivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.ForceSkip -= 1;
#endif
    }

    protected override async Task _RunScript(UnityVNState vn) {
        var rg = (UnityRenderGroup) vn.DefaultRenderGroup;
        var rgb = new UnityRenderGroup(vn, "black", 1, false);
        
        //TODO: add anonymous flag
        var md = vn.Add(new ADVDialogueBox());
        var room = vn.Add(new ShrineRoomBG());
        var courtyard = vn.Add(new ShrineCourtyardBG());
        var reimu = vn.Add(new Reimu());
        var yukari = vn.Add(new Yukari());
        var kasen = vn.Add(new Kasen());

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
            reimu.MoveTo(V3(0, 0), 0.7f, CBezier(.54, .77, .63, 1.47)).And(
                room.FadeTo(1f, 0.4f),
                reimu.Say(l10)
            ).C,
            reimu.MoveTo(V3(-12, 0), 1.2f, CBezier(.37, -0.37, .81, .93)).And(
                vn.Wait(0.2f).Then(rg.DoTransition(new RenderGroupTransition.Fade(rgb, 1f)))
            ),
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
            //Lazy(() => DectivateSkip(vn)),
            kasen.EmoteSayC("worry", l36),
            yukari.EmoteSayC("worry", l37),
            kasen.EmoteSayC("surprise", l38),
            yukari.SayC(l39),
            reimu.EmoteSayC("surprise", l40),
            yukari.SayC(l41),
            reimu.EmoteSayC("worry", l42),
            kasen.EmoteSayC("worry", l43),
            reimu.EmoteSayC("angry", l44),
            kasen.SayC(l45),
            yukari.SayC(l46),
            reimu.EmoteSayC("worry", l47),
            yukari.EmoteSayC("cry", l48),
            kasen.EmoteSayC("normal", l49),
            reimu.SayC(l50),
            yukari.EmoteSayC("", l51)
        );
        


        await vn.SpinUntilConfirm();

    }
}

public class VNCrimsonVermilion : VNScriptExecutor {
    public override VNBaseScript GetScript() => new _VNCrimsonVermilion();
}
}