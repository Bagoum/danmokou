using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.VN;
using Scriptor;
using Suzunoya;
using Suzunoya.ControlFlow;
using UnityEngine;
using static SuzunoyaUnity.Helpers;

namespace MiniProjects.VN {
[Reflect]
public static class ExampleVNScript {
    public static Task ExampleVNScript1(DMKVNState vn) => new BoundedContext<Unit>(vn, "example_1", async () => {
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m = vn.Add(new Marisa());
        r.LocalLocation.Value = new(-3f, 0, 0);
        m.LocalLocation.Value = new(4f, 0, 0);
        r.Alpha = 0;
        m.Alpha = 0;
        await vn.Sequential(
            r.Say("Hello world.")
                .And(r.FadeTo(1f, 1f)).C,
            m.SetEmote("happy"),
            m.FadeTo(1f, 1f).And(m.MoveBy(V3(-1f, 0), 1f)),
            m.SayC("foo bar!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task ExampleVNEndcard1(DMKVNState vn) => new BoundedContext<Unit>(vn, "example_endcard_1", async () => {
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m = vn.Add(new Marisa());
        r.LocalLocation.Value = new(-3f, 0, 0);
        m.LocalLocation.Value = new(4f, 0, 0);
        r.Alpha = 0;
        m.Alpha = 0;
        await vn.Sequential(
            r.Say("This is an example endcard.")
                .And(r.FadeTo(1f, 1f)).C,
            m.SetEmote("happy"),
            m.FadeTo(1f, 1f).And(m.MoveBy(V3(-1f, 0), 1f)),
            m.SayC("And this is Patrick!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task ExampleVNEndcardNoHit(DMKVNState vn) => new BoundedContext<Unit>(vn, "example_endcard_nohit", async () => {
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m = vn.Add(new Marisa());
        r.LocalLocation.Value = new(-3f, 0, 0);
        m.LocalLocation.Value = new(4f, 0, 0);
        r.Alpha = 0;
        m.Alpha = 0;
        await vn.Sequential(
            r.EmoteSay("surprise", "Congratulations, you beat the game without getting hit.")
                .And(r.FadeTo(1f, 1f)).C,
            r.ESayC("", "Anyways, this is an endcard."),
            m.SetEmote("happy"),
            m.FadeTo(1f, 1f).And(m.MoveBy(V3(-1f, 0), 1f)),
            m.SayC("And this is Patrick!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
}
}