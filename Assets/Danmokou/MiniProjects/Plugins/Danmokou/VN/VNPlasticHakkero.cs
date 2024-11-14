using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.Tasks;
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
public class VNPlasticHakkero {
    public static Task PlasticHakkeroDialogue1(DMKVNState vn) => new BoundedContext<Unit>(vn, "ph_1", async () => {
        vn.DefaultRenderGroup.Priority.Value = 1;
        using var black = vn.Add(new ScreenColor());
        black.Tint.Value = new(0, 0, 0, 1);
        black.RenderLayer.Value = SortingLayer.NameToID("Walls");
        using var md = vn.Add(new ADVDialogueBox());
        using var m = vn.Add(new Marisa());
        using var r = vn.Add(new Reimu());
        using var white = vn.Add(new ScreenColor());
        white.Tint.Value = new(1, 1, 1, 0);
        white.RenderLayer.Value = SortingLayer.NameToID("FX");
        m.LocalLocation.Value = new(-3f, 0, 0);
        r.LocalLocation.Value = new(4f, 0, 0);
        m.Alpha = 0;
        r.Alpha = 0;
        await vn.Sequential(
            m.EmoteSay("happy", "Wow! This new Hakkero I just 3D printed is super cool!").And(m.FadeTo(1f, 1f)).C,
            m.ESayC("", "Let me try blasting it off once."),
            m.EmoteSay("happy", "Here we go!"),
            white.FadeTo(1, 2.5f).And(vn.SFX("vn-hakkero").AsVnOp(vn)),
            vn.Wait(1),
            white.FadeTo(0, 1.5f),
            m.ESayC("surprise", "Whoa..."),
            m.ESayC("happy", "That was awesome! Imma do it again!"),
            m.EmoteSay("happy", "Here we go!"),
            vn.Wait(2),
            m.ESayC("worry", "...Huh? Why isn't it working?"),
            r.FadeTo(1f, 1f).And(r.MoveBy(V3(-1f, 0), 1f)),
            r.SayC("Marisa? What are you doing making so much noise?"),
            m.SayC("I'm testing out this new Hakkero, but it stopped working after one blast."),
            r.ESayC("surprise", "What? Even after I got you such a nice eight-faced crystal for its core?"),
            r.ESayC("angry", "Let me take a look."),
            r.SayC("...\n...\n..."),
            r.ESayC("worry", "Did you... make this out of plastic?"),
            m.ESayC("happy", "Yeah! I 3D printed it!"),
            r.Disturb(r.ComputedLocation, JumpY(0.5f), 0.5f).And(r.EmoteSay("angry", "You fool! You can't use plastic to fire giant energy beams! Plastic melts under heat!")).C,
            r.SayC("And now that eight-faced crystal I got you has completely gone to waste..."),
            m.ESayC("worry", "Ah, shit. Now that I think about it, I probably shoulda used graphite instead."),
            r.SayC("Aight. Fuck this. I'm pissed. You and me, danmaku battle, right now."),
            m.ESayC("surprise", "Wait, I can't do a danmaku battle right now! My Hakkero isn't working, so I can't fire any danmaku!"),
            m.ESayC("happy", "Can we do a parkour battle instead?"),
            r.SayC("...No.")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    
    public static Task PlasticHakkeroDialogue2(DMKVNState vn) => new BoundedContext<Unit>(vn, "ph_1", async () => {
        vn.DefaultRenderGroup.Priority.Value = 1;
        using var black = vn.Add(new ScreenColor());
        black.Tint.Value = new(0, 0, 0, 1);
        black.RenderLayer.Value = SortingLayer.NameToID("Walls");
        using var md = vn.Add(new ADVDialogueBox());
        using var m = vn.Add(new Marisa());
        using var r = vn.Add(new Mima());
        m.LocalLocation.Value = new(-3f, 0, 0);
        r.LocalLocation.Value = new(4.5f, 0, 0);
        m.Alpha = 0;
        r.Alpha = 0;
        await vn.Sequential(
            m.EmoteSay("worry", "Phew... fighting danmaku battles without danmaku is really hard.").And(m.FadeTo(1f, 1f)).C,
            m.ESayC("", "If someone pops out of nowhere and challenges me to a danmaku battle now, there's no way I'd survive!"),
            r.SetEmote("happy"),
            r.FadeTo(1f, 1f).And(r.MoveBy(V3(-1f, 0), 1f)),
            r.SayC("Did someone say... <i>pop out of nowhere</i>?"),
            m.Disturb(m.ComputedLocation, JumpY(0.5f), 0.5f).And(m.EmoteSay("surprise", "Ahh! A ghost!")).C,
            m.ESayC("worry", "Wait, Mima?"),
            m.ESayC("angry", "What are you doing out here? Didn't Reimu imprison you in the seal behind the shrine?"),
            r.ESayC("worry", "I <i>was</i> imprisoned there, but just a short while ago, a massive energy beam blasted through part of the seal."),
            r.ESayC("happy", "So I escaped before Reimu could notice."),
            r.ESayC("worry", "Also I'm not a ghost."),
            m.ESayC("worry", "Ohhhh... That was probably my Hakkero."),
            r.ESayC("surprise", "Seriously? You've really improved on it since the last time I was out."),
            r.ESayC("happy", "Why don't we have a danmaku battle? I want to see all the features of your new Hakkero."),
            m.ESayC("cry", "Wait! My Hakkero isn't working right now, I can't fight!"),
            r.ESayC("happy", "Get ready! It's danmaku time!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
}
}