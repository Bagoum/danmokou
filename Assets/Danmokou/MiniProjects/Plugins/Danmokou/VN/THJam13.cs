using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.ControlFlow;
using Suzunoya.Entities;
using static SuzunoyaUnity.Helpers;
using static BagoumLib.Mathematics.Bezier;

namespace MiniProjects.VN {
[Reflect]
public static class THJam13 {
    public static Task THJ13Dialogue1(DMKVNState vn) => new BoundedContext<Unit>(vn, "th13_1", async () => {
        return default;
    }).Execute();
    public static Task THJ13Dialogue2(DMKVNState vn) => new BoundedContext<Unit>(vn, "th13_2", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m = vn.Add(new Mima());
        r.LocalLocation.Value = new(2, 0, 0);
        m.LocalLocation.Value = new(-4, 0, 0);
        await vn.Sequential(
            m.ESayC("happy", "Long time no see, Reimu."),
            r.ESayC("surprise", "Mima?! What are you doing here?!"),
            r.ESayC("angry", "Actually, I can tell. You're the one behind this incident, aren't you?"),
            m.ESayC("smug", "In the past, I was."),
            m.SayC("In the future, I may be."),
            m.SayC("Here in the now, in this infinitesimal gap between the known and the unknown... who can truly render judgement?"),
            r.ESayC("worry", "..."),
            r.SayC("Sounds like I should beat you up just to be sure."),
            m.ESayC("smug", "Yes! That is precisely what you ought <i>try</i> to do, as the Hakurei shrine maiden."),
            m.SayC("Come! Show me how much you've learned through all these years!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task THJ13Dialogue3(DMKVNState vn) => new BoundedContext<Unit>(vn, "thj13_3", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m = vn.Add(new Mima());
        r.LocalLocation.Value = new(2, 0, 0);
        m.LocalLocation.Value = new(-4, 0, 0);
        await vn.Sequential(
            m.SetEmote("cry"),
            r.ESayC("angry", "Good riddance. That puts an end to this stupid incident."),
            m.ESayC("smug", "Heh... are you really so sure about that?"),
            r.Disturb(r.ComputedLocation, JumpY(0.8f), 0.6f).And(
                    r.EmoteSay("surprise", "What?! You're not the final boss of this incident?!")).C,
            m.SayC("I could have been the final boss if I had wanted. But see, there's a problem with that."),
            m.ESayC("worry", "All said, I'm just a ghost of the past. Even with this incident, the influence I can have on the present is limited."),
            m.ESayC("happy", "So, in order to maximize chaos, the final boss of this incident is someone... much closer to you."),
            r.ESayC("worry", "Wait, it better not be who I'm thinking of."),
            m.ESayC("smug", "Hahaha... in fact, it is precisely the one you are thinking of..."),
            r.ESayC("surprise", "No! I have to fight <i>Yukari</i>?!"),
            m.Say("Yes, it is--"),
            vn.Wait(0.2),
            m.ESayC("worry", "\nWait, who? You curry?", flags: SpeakFlags.DontClearText),
            r.ESayC("worry", "What? It's not Yukari?"),
            m.SayC("No, I suppose I don't curry. I prefer not to create closures if possible."),
            r.ESayC("happy", "Well, if it's not Yukari, then I'm not worried."),
            m.SayC("..."),
            m.ESayC("smug", "That's right! The final boss is the one you hold dearest!"),
            m.SayC("Take your gohei in hand and strike down the one you love most!"),
            m.SayC("If not, you won't be able to solve this incident!"),
            vn.SFX("vn-yukari-power"),
            m.EmoteSay("happy", "Hahahaha!").And(m.MoveBy(V3(0, 1), 0.8f),m.FadeTo(0, 0.8f)).C,
            r.ESayC("worry", "...Wait, the \"one I love most\"?"),
            r.ESayC("surprise", "Don't tell me... the final boss of this incident is... <speed=0.5><i>myself</i></speed>?!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    public static Task THJ13Dialogue4(DMKVNState vn) => new BoundedContext<Unit>(vn, "th13_4", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m9 = vn.Add(new Marisa98());
        using var m = vn.Add(new Marisa());
        r.LocalLocation.Value = new(3, 0, 0);
        m9.LocalLocation.Value = new(-4, 0, 0);
        m.LocalLocation.Value = new(1, 3, 0);
        
        m.Alpha = 0;
        await vn.Sequential(
            r.ESayC("worry", "What? Marisa? What are you doing in that old granny-ass getup?"),
            m9.RotateTo(V3(0, 180), 1f).And(m9.EmoteSay("worry", "Huh? You're not Mima-sensei. Why are you here?")).C,
            m9.ESayC("surprise", "Wait... Those armpits! It can't be! You're Reimu?!"),
            r.ESayC("angry", "Who the hell else would I be?!"),
            m9.ESayC("worry", "Sorry, I just didn't expect you to dye your hair black."),
            m9.SayC("Like, if you had dyed it blonde or something, I would have recognized you, but nobody dyes their hair black."),
            r.ESayC("worry", "...My hair isn't dyed."),
            m9.SayC("No, I'm pretty sure your hair is naturally purple."),
            r.ESayC("angry", "...Let's just assume your failing memory is a side effect of the incident. Anyways, have you found the final boss yet?"),
            m9.ESayC("happy", "If you're looking for a final boss, it would have to be Mima-shachou."),
            r.ESayC("worry", "I already beat her, but she said the final boss is someone else."),
            m9.SayC("Well, the profound depths of Mima-senpai's plans are inscrutable to us mere mortals."),
            m.SetEmote("angry"),
            m.MoveTo(V3(1, 0), 1f, CBezier(.4, .62, .45, 1.24))
            .And(m.FadeTo(1f, 0.5f))
            .And(vn.Wait(0.2f).Then(vn.aSFX("vn-impact-1")))
            .And(vn.Wait(0.2f).Then(
                r.Disturb(r.ComputedLocation, JumpY(0.7f), 0.5f).And(
                    r.SetEmote("surpirse").AsVnOp(vn),
                    r.MoveTo(V3(4, 0), 0.5f)))),
            m.SayC("Stop, evildoer!"),
            r.SayC("Marisa?! What are you--"),
            r.ESayC("angry", "Wait, why are there two Marisas?"),
            m.SayC("That Marisa is a fake! She's a figment of the incident!"),
            m9.ESayC("worry", "As far as I know, there's no \"real\" or \"fake\" Marisa. There's only one Marisa."),
            m9.ESayC("happy", "Me."),
            m9.ESayC("angry", "And Gensoukyou isn't big enough for two Marisas."),
            r.ESayC("worry", "...You know, I'm starting to think I'd rather have fought Yukari."),
            m.ESayC("smug", "Just the way I like it. Let's settle this with fire, like real Marisas should!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    
    public static Task THJ13Dialogue5(DMKVNState vn) => new BoundedContext<Unit>(vn, "th13_5", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var r = vn.Add(new Reimu());
        using var m9 = vn.Add(new Marisa98());
        using var m = vn.Add(new Marisa());
        r.LocalLocation.Value = new(4, 0, 0);
        m9.LocalLocation.Value = new(-4, 0, 0);
        m.LocalLocation.Value = new(1, 0, 0);
        
        await vn.Sequential(
            m9.SetEmote("cry"),
            m.SetEmote("cry"),
            r.ESayC("surprise", "Marisa! Stop with the damn lasers! You're getting me caught in the blasts!"),
            m.SayC("But if I don't fire lasers indiscriminately, I wouldn't be Marisa."),
            r.ESayC("angry", "And if I don't beat up everyone causing trouble, then I wouldn't be Reimu!"),
            m.SayC("I thought you would focus your attacks on the fake Marisa, but I feel like you hit me more..."),
            r.SayC("Resolving incidents is a job. Beating up troublemakers is a way of life."),
            r.SayC("Purple Marisa might be responsible for the incident, but she wasn't causing me much trouble. <i>You</i> were!"),
            m9.ESayC("happy", "You know, I think the black hair works on you. It fits your hardboiled attitude."),
            r.ESayC("worry", "......I'm not going to respond to that."),
            r.ESayC("angry", "And don't you have places to be, Purple Marisa? The incident is over. Go find your sunbae or whatever and get out of here."),
            m9.EmoteSay("cry", "...Okay.").And(m9.MoveBy(V3(-1, 0), 1), m9.FadeTo(0, 1)).C,
            r.ESayC("worry", "...I suppose I did hit you a bit too much. Why don't we head back and get you bandaged up?"),
            m.ESayC("happy", "Thanks! You're the best, Reimu!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
}
}