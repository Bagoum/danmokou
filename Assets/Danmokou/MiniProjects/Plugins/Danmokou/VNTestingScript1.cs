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
using static Danmokou.Core.ServiceLocator;
using Vector3 = System.Numerics.Vector3;

namespace MiniProjects.VN {

[Reflect]
public static class _VNTesting {
    private static void ActivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.SetSkipMode(SkipMode.FASTFORWARD);
#endif
    }
    private static void DeactivateSkip(UnityVNState vn) {
#if UNITY_EDITOR
        vn.SetSkipMode(null);
#endif
    }

    public static async Task TestComplexRG(DMKVNState vn) {
        //Structure:
        // rgBg and rgChar will both nest-render to rg.
        // rg has a mask confining it to the center.
        // rgBgLower renders under rg.
        //Note: you could also just have rgBg/rg share the mask.
        var rg = (UnityRenderGroup) vn.DefaultRenderGroup;
        var rgBg = new UnityRenderGroup(vn, "rgbg", 2, true);
        var rgBgLower = new UnityRenderGroup(vn, "rgbglower", -1, true);
        var rgChar = new UnityRenderGroup(vn, "rgChar", 3, true);
        var rgD = new UnityRenderGroup(vn, "dialogue", 10, true);
        rgBg.NestedRenderGroup.Value = rg;
        rgChar.NestedRenderGroup.Value = rg;
        var bg = vn.Add(new FarmBG());
        bg.RenderGroup.Value = rgBg;
        var bgl = vn.Add(new LibraryBG());
        bgl.RenderGroup.Value = rgBgLower;
        var chr = vn.Add(new Reimu());
        chr.RenderGroup.Value = rgChar;
        var db = vn.Add(new ADVDialogueBox(), rgD);
        
        rg.SetMask(new UnityRenderGroupMask((x, y) => M.Lerp(0.3f, 0.34f, Mathf.Abs(x - 0.5f), 1f, 0f)));

        await bgl.FadeTo(1, 1).C;
        await bg.FadeTo(1, 1).And(chr.FadeTo(1, 1).And(db.FadeTo(1, 1))).C;
        //We can pan rgBg and rgChar separately.
        await chr.Say("This is a pan").And(rgBg.MoveTo(V3(-1, 0), 3)).And(rgChar.MoveTo(V3(0, 1), 2)).C;
        //We can zoom rgBg and rgChar separately.
        rgBg.ZoomTarget.Value = V3(-2, 0);
        rgChar.ZoomTarget.Value = V3(0, 0);
        await chr.Say("This is a zoom").And(rgBg.ZoomTo(1.5f, 2)).And(rgChar.ZoomTo(2, 2)).C;
        
        await vn.SpinUntilConfirm();
    }

    public static async Task TestVNScript(DMKVNState vn) {
        var rg = (UnityRenderGroup) vn.DefaultRenderGroup;
        var rgdb = new UnityRenderGroup(vn, "rgdb", 3, true);
        var rg2 = new UnityRenderGroup(vn, "rg2", -2, true);

        var library = vn.Add(new FarmBG());
        var field = vn.Add(new FieldBG());
        field.RenderGroup.Value = rg2;
        var md = vn.Add(new ADVDialogueBox());
        md.RenderGroup.Value = rgdb;
        var kasen = vn.Add(new Kasen());
        var yukari = vn.Add(new Yukari());
        var reimu = vn.Add(new Reimu());
        reimu.Location.Value = V3(6, 0);
        await library.FadeTo(1f, 1f).And(kasen.FadeTo(1f, 0.5f), md.FadeTo(1f, 0.5f), reimu.FadeTo(1f, 0.5f), field.FadeTo(1f, 1f));
        kasen.Emote.Value = "happy";
        await kasen.Say(
            "Lorem ipsum <ruby=dolor means pain>dolor sit amet</ruby>, consectetur adipiscing elit. Vivamus gravida varius nisi ut eleifend.").C;
        //You can fade to transparent by using null as the target (and use reverse:True to fade back)
        await rg.DoTransition(new RenderGroupTransition.Fade(null, 1f));
        await vn.Wait(1f);
        await rg.DoTransition(new RenderGroupTransition.Fade(null, 1f), reverse: true);
        await kasen.AlsoSayN(
                "Pellentesque habitant morbi tristique senectus et netus et <speed=0.3>malesuada fames ac turpis</speed> egestas. Ut quis dapibus ante. Etiam dictum placerat est, a facilisis ante blandit varius.")
            .C;
        kasen.Emote.Value = "surprise";
        yukari.Location.Value = V3(-4, 1);
        await kasen.MoveTo(V3(4, 0), 1).And(
            kasen.Say("Oh, someone's here."),
            yukari.MoveTo(V3(-4, 0), 1),
            yukari.FadeTo(1, 1)
            ).C;
        yukari.Emote.Value = "surprise";
        await yukari.Say("Did you notice that when I started talking, the color of the dialogue box slowly changed to purple from red? Wow. I wonder how that works.").C;

        await vn.SpinUntilConfirm();
        
        await kasen.MoveTo(V3(-5, 0), 1.6f, Bezier.CBezier(.67, -0.37, .7, .97)).And(   
                vn.Wait(0.5f)
                    .Then(() => kasen.Emote.Value = "cry")
                    .Then(kasen.RotateTo(V3(0, 0, 1800), 1.8f)),
                vn.Wait(0.7f)
                    .Then(kasen.ScaleTo(V3(0), 1.4f, Easers.EInSine))
            ).C;
        

        var t = rg.DoTransition(new RenderGroupTransition.Fade(rg2, 4f)).Task;
        vn.RequestSkipOperation();
        await t;
        await vn.Wait(1f);
        rg.Visible.Value = true;
        await vn.Wait(1f).C;
        rg2.Priority.Value = 15;
        await vn.Wait(1f);
        rg.ZoomTarget.Value = new Vector3(3f, 0f, 0f);
        await rg.ZoomTo(0.4f, 2);
    }
}
}