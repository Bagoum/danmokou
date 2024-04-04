using System.Reactive;
using System.Threading.Tasks;
using BagoumLib;
using BagoumLib.DataStructures;
using BagoumLib.Mathematics;
using Danmokou.Core;
using Danmokou.Services;
using Danmokou.VN;
using Suzunoya;
using Suzunoya.ControlFlow;
using UnityEngine;
using static SuzunoyaUnity.Helpers;

namespace MiniProjects.VN {
[Reflect]
public static class VNFlappyBird {
    public static Task FlappyBirdDialogue1(DMKVNState vn) => new BoundedContext<Unit>(vn, "flappy_1", async () => {
        using var black = vn.Add(new ScreenColor());
        black.Tint.Value = new(0, 0, 0, 1);
        black.RenderLayer.Value = SortingLayer.NameToID("Background");
        using var md = vn.Add(new ADVDialogueBox());
        using var s = vn.Add(new Sagume());
        using var e = vn.Add(new Eirin());
        s.LocalLocation.Value = new(-3f, 0, 0);
        e.LocalLocation.Value = new(4f, 0, 0);
        s.Alpha = 0;
        e.Alpha = 0;
        await vn.Sequential(
            s.Say("It's too bad the dinosaurs went extinct before my time. I'd like to have seen them.")
                .And(s.FadeTo(1f, 1f)).C,
            e.FadeTo(1f, 1f).And(e.MoveBy(V3(-1f, 0), 1f)),
            e.SayC("The dinosaurs may have gone extinct, but strictly speaking, modern birds are descendants of dinosaurs."),
            s.Disturb(s.ComputedLocation, JumpY(0.5f), 0.5f).And(
                s.EmoteSay("surprise", "Really?! Birds are dinosaurs? What kind of forbidden alchemical knowledge is this?")).C,
            e.ESayC("worry", "It's not forbidden knowledge, and it's not alchemy. This is just standard biology on Earth."),
            e.SayC("The Earthlings are much smarter than the Lunarians give them credit for, you know. You should learn from them."),
            s.ESayC("normal", "I see, I see. But I'll need proof to convince everyone that birds are actually dinosaurs."),
            s.SayC("Do you know of anything I could use?"),
            e.ESayC("happy", "Well, there's a famous book called <i>On the Species of Origin</i> by Darles Charwin that discusses" +
                             " the theory of evolution in the context of birds."),
            e.ESayC("normal", "I don't have a copy here, but if you go to Kourindou, you should be able to find one."),
            s.ESayC("happy", "Say no more! <b>I shall make my way to Kourindou without incident!</b>"),
            e.ESayC("surprise", "..."),
            e.ESayC("worry", "Well, if you say so.")
        );
        
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task FlappyBirdDialogue2(DMKVNState vn) => new BoundedContext<Unit>(vn, "flappy_2", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var s = vn.Add(new Sagume());
        using var a = vn.Add(new Aya());
        s.LocalLocation.Value = new(-3f, 0, 0);
        a.LocalLocation.Value = new(3f, 0, 0);
        await vn.Sequential(
            a.SetEmote("worry"),
            s.ESayC("surprise", "Literally what do you have against me?!"),
            s.SayC("I don't even know you! Why are you attacking me?"),
            a.SayC("I'm doing this for your own sake, little birdie."),
            a.SayC("You should turn back here. It's not safe to go any farther."),
            s.ESayC("angry", "I'm not a bird."),
            a.ESayC("surprise", "Of course you are. You have wings... well, <i>a</i> wing, and you can fly. That makes you a bird."),
            s.SayC("Penguins have wings and can't fly, but they're birds too. Thus, your definition is incomplete in basic cases."),
            a.ESayC("angry", "Penguins can't fly, so they're not birds."),
            s.ESayC("worry", "Wait, really? What about ostriches?"),
            a.SayC("Not birds."),
            s.SayC("Kiwis?"),
            a.ESayC("surprise", "Not birds. Wait, isn't that obvious? It's not even close."),
            s.SayC("...Look, I'm pretty sure that according to the theory of evolution, penguins, ostriches, and kiwis are all birds."),
            a.ESayC("angry", "Nonsense. Any theory that classifies fruit as birds can't possibly be worth its weight in salt."),
            a.SayC("Little birdie, for your own sake, I'll clear your head of these silly delusions. En garde!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task FlappyBirdDialogue3(DMKVNState vn) => new BoundedContext<Unit>(vn, "flappy_3", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var s = vn.Add(new Sagume());
        using var a = vn.Add(new Aya());
        s.LocalLocation.Value = new(-3f, 0, 0);
        a.LocalLocation.Value = new(3f, 0, 0);
        await vn.Sequential(
            s.SetEmote("worry"),
            a.EmoteSayC("cry", "..."),
            s.SayC("Uh... are you okay?"),
            a.SayC("...No."),
            s.ESayC("surprise", "What did you just get hit by? That wasn't danmaku, was it?"),
            a.SayC("...Since this morning, a large amount of bird-hostile architecture has been passing into Gensoukyou, particularly between the Bamboo Forest and Kourindou."),
            a.SayC("Windmills, fences, and worst of all... airplanes."),
            a.ESayC("angry", "I'm pretty sure someone's behind this, but I have no idea who could possibly have such an odd power."),
            a.ESayC("worry", "Do you have any ideas?"),
            s.ESayC("worry", "The power to make things pass into fantasy? Other than the Youkai of Boundaries, I couldn't imagine" +
                             " anyone would be able to do such a thing."),
            a.ESayC("cry", "Well, whatever. Either way, I'm cutting my losses here. If I get hit by another one of those airplanes," +
                           " I'll turn into a damn ortolan."),
            a.SayC("There are a lot of them up ahead, so... fly at your own risk, little birdie."),
            a.FadeTo(0, 1).And(a.MoveBy(V3(1, 0), 1)),
            s.SayC("..."),
            s.ESayC("happy", "Well, I'm not a bird, so <b>those airplanes can't possibly hurt me!</b>")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task FlappyBirdDialogue4(DMKVNState vn) => new BoundedContext<Unit>(vn, "flappy_4", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var s = vn.Add(new Sagume());
        using var t = vn.Add(new Tokiko());
        s.LocalLocation.Value = new(-3f, 0, 0);
        t.LocalLocation.Value = new(3f, 0, 0);
        
        await vn.Sequential(
            s.SayC("Good afternoon. Is this the so-called Kourindou?"),
            t.SayC("That's right. Are you looking for something?"),
            s.ESayC("happy", "I'm looking for a book. It's called... <i>On the Species of Origin</i> or something like that."),
            s.ESayC("surprise", "Wait, actually, I think it's the book you're reading right now."),
            t.ESayC("surprise", "You're looking for <i>this</i> book? <i>On the Origin of Species</i> by Charles Darwin?"),
            t.ESayC("worry", "Isn't that too much of a coincidence? Why would some rando I've never seen before come looking for the book I'm reading right now?"),
            t.SayC("I mean, we're even in the middle of an incident at the moment, with all the airplanes and windmills flying around..."),
            s.ESayC("worry", "I suppose it is a bit of a coincidence."),
            t.ESayC("surprise", "Wait... I think I see what's going on..."),
            t.ESayC("angry", "Reimu must have sent you here to steal my books again under the guise of incident resolution!"),
            t.SayC("Ha! This time, I won't go down easily! I read a bunch of books and learned how to fight!"),
            s.SayC("Reimu? I feel like I've heard that name before, but I'm not really sure who that is. I'm Eirin's friend, for what it's worth."),
            t.ESayC("surprise", "You don't know who Reimu is?!"),
            new LazyAction(() => ServiceLocator.Find<IAudioTrackService>().ClearRunningBGM()),
            t.ESayC("angry", "Only someone in cahoots with Reimu could possibly tell that flagrant a lie!"),
            new LazyAction(() => ServiceLocator.Find<IAudioTrackService>()
                .AddTrackset(new(1f, 0.01f)).AddTrack("fb.tokiko")),
            t.ESayC("smug", "As they say, the early bird gets the worm. If you won't reveal your intentions, then I'll just have to attack first!")
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();
    
    public static Task FlappyBirdDialogue5(DMKVNState vn) => new BoundedContext<Unit>(vn, "flappy_5", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var s = vn.Add(new Sagume());
        using var t = vn.Add(new Tokiko());
        using var r = vn.Add(new Reimu());
        s.LocalLocation.Value = new(-3f, 0, 0);
        t.LocalLocation.Value = new(3f, 0, 0);
        r.LocalLocation.Value = new(-5.7f, 2, 0);
        r.Alpha = 0;
        //vn.SetSkipMode(SkipMode.FASTFORWARD);
        await vn.Sequential(
            s.SetEmote("worry"),
            t.SetEmote("cry"),
            t.SayC("Owowowow... How could you hit me like that?"),
            s.SayC("Hey, I don't want to fight. I just want the book."),
            t.ESayC("angry", "That's what Reimu always says when she steals my books."),
            s.ESayC("worry", "Is this 'Reimu' in the room with us right now?"),
            t.ESayC("worry", "Uh... I suppose not?"),
            s.MoveBy(V3(1, 0), 0.7f).And(s.EmoteSay("happy", "Alright. Then hand it over.")).C,
            s.MoveBy(V3(1, 0), 0.7f).And(s.Say("That thing. Your biology textbook.")).C,
            t.MoveBy(V3(0.6f, 0), 0.6f).And(t.EmoteSay("surprise", "No! This is <i>my</i> copy of the Origin of Species!")).C,
            s.SetEmote("surprise"),
            r.MoveBy(V3(1.4f, -2f), 1f).And(r.FadeTo(1, 1)).And(r.Say("Yo, what's going on here?")).C,
            s.RotateTo(V3(0, 180, 0), 1).And(
            r.EmoteSay("surprise", "Hey! Aren't you that bird from the moon? Are you the one behind this incident?")).C,
            s.ESayC("angry", "I'm not a bird."),
            r.ESayC("worry", "Of course you are. You can fly, so you're a bird."),
            s.SayC("You can fly too, but you're not a bird. Thus, your definition is inconsistent in basic cases."),
            r.SayC("I'm a human, so I'm not a bird by definition."),
            r.ESayC("angry", "Look, I'm just going around trying to figure out who's behind this windmill and airplane incident. " +
                             "Don't make my job any harder than it needs to be."),
            //new LazyAction(() => vn.SetSkipMode(null)),
            r.ESayC("worry", "Oh, and I suppose I'm also looking for a book. It's called, uh... well, it has something to do with dinosaurs."),
            r.ESayC("happy", "Actually, I think it's the book Tokiko is holding right now."),
            t.MoveBy(V3(0.4f, 0), 0.6f).And(t.EmoteSay("surprise", "You can't have this book! It's-")).C,
            r.MoveBy(V3(6, 0), 2, Easers.EOutBack).And(r.Say("Thanks for the book!"),
                vn.Wait(0.9).Then(
                    t.MoveBy(V3(5, 5), 2.6f, Easers.EOutQuad).And(t.RotateTo(V3(0, 0, 1080), 2.5f), t.ScaleTo(V3(0, 0), 2.6f)))).C,
            r.MoveBy(V3(1, 0), 0.7f).And(r.FadeTo(0, 0.7f))
                .Then(s.EmoteSay("worry", "...I guess I'll have to go find another library.")).C
        );
        await vn.DefaultRenderGroup.FadeTo(0, 0.4f);
        return default;
    }).Execute();


}
}