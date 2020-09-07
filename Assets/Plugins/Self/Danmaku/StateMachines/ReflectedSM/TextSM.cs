using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMath;
using Core;
using JetBrains.Annotations;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using static Common.Types;
using static FParser.AAParser;
using TU = FParser.AAParser.TextUnit<float, Dialoguer.EventType, string>;
using TC = FParser.AAParser.TextCommand<float, Dialoguer.EventType, string>;
using C = FParser.AAParser.Config<float, Dialoguer.EventType, string>;
using AR = FParser.AAParser.ArgRef<float, Dialoguer.EventType, string>;
using QAR = Microsoft.FSharp.Core.FSharpOption<FParser.AAParser.ArgRef<float, Dialoguer.EventType, string>>;
using static FSInterop;

namespace SM {
public static class TSMReflection {
    public static TaskPattern Re(StateMachine sm) => sm.Start;
    public static TaskPattern Wait(Synchronizer synchr) => SMReflection.Wait(synchr);
    
    [Alias("z")]
    public static TaskPattern Confirm() => async smh => {
        Dialoguer.WaitingOnConfirm = true;
        smh.RunRIEnumerator(WaitingUtils.WaitForDialogueConfirm(smh.cT, WaitingUtils.GetAwaiter(out Task t)));
        await t;
        Dialoguer.WaitingOnConfirm = false;
    };
    
    public static TaskPattern Place(Dialoguer.StandLocation location, string profile_key) {
        var profile = Dialoguer.GetProfile(profile_key);
        return smh => {
            Dialoguer.SetStand(profile, location, Emote.NORMAL);
            return Task.CompletedTask;
        };
    }

    private static TaskPattern FadeStand(string profile_key, float time, bool fadeIn) {
        var profile = Dialoguer.GetProfile(profile_key);
        return smh => {
            Dialoguer.FadeStand(profile, time, fadeIn, smh.cT, WaitingUtils.GetAwaiter(out Task t));
            return t;
        };
    }

    public static TaskPattern SetStandOpacity(string profile_key, float opacity) => smh => {
        Dialoguer.SetOpacity(Dialoguer.GetProfile(profile_key), opacity);
        return Task.CompletedTask;
    };
    public static TaskPattern FadeStandIn(string profile_key, float time) => FadeStand(profile_key, time, true);
    public static TaskPattern FadeStandOut(string profile_key, float time) => FadeStand(profile_key, time, false);
    
    private static readonly FSharpFunc<string, Errorable<FSharpList<TU>>> parser = CreateParser3<float, Dialoguer.EventType, string>(
        "w", F<string, FSharpOption<float>>(x => Parser.MaybeFloat(x)),
        "ev",F<string, FSharpOption<Dialoguer.EventType>>(_ => null),
        "sfx", F<string, FSharpOption<string>>(FSharpOption<string>.Some),
        FSInterop.NewMap<string, FSharpFunc<string, FSharpOption<WikiEntry>>>());
    private static readonly QAR noAR = QAR.None; 
    private static readonly AR rollSfx = AR.NewRef2(Dialoguer.EventType.SPEAKER_SFX);
    private static int DefaultCharsPerBlock => SaveData.s.Locale == Locale.JP ? 3 : 8;

    private static TaskPattern _Text(string text, bool continued) {
        //method 1: sound on spaces (ie words)
        //var cfg = new C(12, 1, 0.3, F<Punct, double>(p => p.Resolve(0.35, 2.5, 3.5, 4.5, 5.0)), noAR, F<Punct, QAR>(p => p.Resolve(rollSfx,noAR,noAR,noAR, rollSfx)));
        //method 2: sound on char blocks
        var cfg = new C(36, DefaultCharsPerBlock, 3, 
            F<Punct, double>(p => p.Resolve(0, 3.0, 4.0, 5.0, 7.0)), rollSfx, 
            F<Punct, QAR>(p => p.Resolve(noAR,noAR,noAR,noAR, rollSfx)));
        var textCmds = ParseAndExport(cfg, parser).Invoke(text).Try.ToArray();
        return async smh => {
            var cts = new Cancellable();
            var joint = new JointCancellee(cts, smh.cT);
            Dialoguer.RunDialogue(textCmds, joint, WaitingUtils.GetAwaiter(out Func<bool> isDone), continued);
            smh.RunRIEnumerator(WaitingUtils.WaitWhileWithCancellable(isDone, cts, () => InputManager.DialogueToEnd,
                joint, WaitingUtils.GetAwaiter(out Task t), ETime.FRAME_TIME * 10f));
            await t;
        };
    }

    [Alias(".")]
    public static TaskPattern Text(string text) => _Text(text, false);
    [Alias(".c")]
    public static TaskPattern ContinuedText(string text) => _Text(text, true);
    [Alias(".cn")]
    public static TaskPattern ContinuedTextNewline(string text) => _Text("\n" + text, true);
    [Alias(".z")]
    public static TaskPattern TextConfirm(string text) => async smh => {
        await _Text(text, false)(smh);
        await Confirm()(smh);
    };

    private static TaskPattern _Speak(LR lr, [CanBeNull] string profile_key, Emote? emote) {
        var profile = profile_key == null ? null : Dialoguer.GetProfile(profile_key);
        return smh => {
            if (lr == LR.LEFT) Dialoguer.SetLeftSpeaker(profile, emote);
            else Dialoguer.SetRightSpeaker(profile, emote);
            return Task.CompletedTask;
        };
    }

    [Alias("SL")]
    public static TaskPattern SpeakL(string profile) => _Speak(LR.LEFT, profile, null);
    [Alias("SR")]
    public static TaskPattern SpeakR(string profile) => _Speak(LR.RIGHT, profile, null);
    [Alias("SLE")]
    public static TaskPattern SpeakLE(string profile, Emote e) => _Speak(LR.LEFT, profile, e);
    [Alias("SRE")]
    public static TaskPattern SpeakRE(string profile, Emote e) => _Speak(LR.RIGHT, profile, e);
    [Alias("SLC")]
    public static TaskPattern SpeakLC() => _Speak(LR.LEFT, null, null);
    [Alias("SRC")]
    public static TaskPattern SpeakRC() => _Speak(LR.RIGHT, null, null);
    [Alias("SLCE")]
    public static TaskPattern SpeakLCE(Emote e) => _Speak(LR.LEFT, null, e);
    [Alias("SRCE")]
    public static TaskPattern SpeakRCE(Emote e) => _Speak(LR.RIGHT, null, e);
    
    public static TaskPattern SetEmote(string profile, Emote e) {
        var p = Dialoguer.GetProfile(profile);
        return smh => {
            Dialoguer.UpdateStandEmote(p, e);
            return Task.CompletedTask;
        };
    }

    public static TaskPattern Set(StateMachine[] states) => async smh => {
        foreach (var s in states) {
            await s.Start(smh);
            smh.ThrowIfCancelled();
        }
    };

    public static TaskPattern If1CC(StateMachine iftrue, StateMachine iffalse) => smh =>
        GameManagement.campaign.Continued ? iffalse.Start(smh) : iftrue.Start(smh);
}

}