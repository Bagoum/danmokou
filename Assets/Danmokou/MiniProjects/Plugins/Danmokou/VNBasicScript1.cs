using System.Threading.Tasks;
using Suzunoya;
using BagoumLib.Mathematics;
using Danmokou.VN;
using SuzunoyaUnity;
using SuzunoyaUnity.Rendering;
using UnityEngine;
using static SuzunoyaUnity.Helpers;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace MiniProjects.VN {
//C# class containing the task code
public class _VNBasicScript1 {
    public async Task VNBaseScript1(DMKVNState vn) {
        //
        var rg = (UnityRenderGroup) vn.DefaultRenderGroup;
        var rg2 = new UnityRenderGroup(vn, "rg2", 4, false);

        var library = vn.Add(new FarmBG());
        var field = vn.Add(new FieldBG());
        field.RenderGroup.Value = rg2;
        var md = vn.Add(new ADVDialogueBox());
        var kasen = vn.Add(new Kasen());
        var yukari = vn.Add(new Yukari());
        var reimu = vn.Add(new Reimu());
        reimu.Location.Value = V3(6, 0);
        await library.FadeTo(1f, 1f).And(kasen.FadeTo(1f, 0.5f), md.FadeTo(1f, 0.5f), reimu.FadeTo(1f, 0.5f), field.FadeTo(1f, 1f));
        kasen.Emote.Value = "happy";
        await kasen.Say(
            "Lorem ipsum <ruby=dolor means pain>dolor sit amet</ruby>, consectetur adipiscing elit. Vivamus gravida varius nisi ut eleifend.").C;
        await rg.DoTransition(new RenderGroupTransition.Fade(rg2, 0.5f));
        await vn.Wait(1f);
        await rg2.DoTransition(new RenderGroupTransition.Fade(rg, 0.5f));
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