using System;
using System.Linq;
using System.Threading.Tasks;
using DMK.Core;
using DMK.Danmaku;
using DMK.Dialogue;
using DMK.DMath;
using JetBrains.Annotations;
using ParserCS;
using static ParserCS.AAParser;
using static ParserCS.Common;
using DUContents = DMK.Core.DU<float, string, DMK.Dialogue.Dialoguer.EventType>;

namespace DMK.SM {
[Reflect]
public static class TSMReflection {
    public static TTaskPattern Re(StateMachine sm) => sm.Start;
    public static TTaskPattern Wait(Synchronizer synchr) {
        var tp = SMReflection.Wait(synchr);
        return smh => tp(smh);
    }

    [Alias("z")]
    public static TTaskPattern Confirm() => async smh => {
        Dialoguer.WaitingOnConfirm = true;
        smh.RunRIEnumerator(WaitingUtils.WaitForDialogueConfirm(smh.cT, WaitingUtils.GetAwaiter(out Task t)));
        await t;
        Dialoguer.WaitingOnConfirm = false;
    };
    
    public static TTaskPattern Place(Dialoguer.StandLocation location, string profile_key) {
        var profile = Dialoguer.GetProfile(profile_key);
        return smh => {
            Dialoguer.SetStand(profile, location, Emote.NORMAL);
            return Task.CompletedTask;
        };
    }

    private static TTaskPattern FadeStand(string profile_key, float time, bool fadeIn) {
        var profile = Dialoguer.GetProfile(profile_key);
        return smh => {
            Dialoguer.FadeStand(profile, time, fadeIn, smh.cT, WaitingUtils.GetAwaiter(out Task t));
            return t;
        };
    }

    public static TTaskPattern SetStandOpacity(string profile_key, float opacity) => smh => {
        Dialoguer.SetOpacity(Dialoguer.GetProfile(profile_key), opacity);
        return Task.CompletedTask;
    };
    public static TTaskPattern FadeStandIn(string profile_key, float time) => FadeStand(profile_key, time, true);
    public static TTaskPattern FadeStandOut(string profile_key, float time) => FadeStand(profile_key, time, false);

    private static readonly Func<Config<DialogueObject>, string, Helpers.Errorable<TextCommand<DialogueObject>[]>>
        parser = CreateCommandParser((key, arg) => {
            return key.ToLower() switch {
                "w" => Parser.MaybeFloat(arg).Try(out var wait) ?
                    DialogueObject.Wait(wait) :
                    Helpers.Errorable<DialogueObject>.Fail($"Couldn't parse waiting time {arg}"),
                "sfx" => DialogueObject.SFX(arg),
                _ => Helpers.Errorable<DialogueObject>.Fail($"No dialogue command exists by key {key}")
            };
        });
    private static readonly DialogueObject RollSFX = DialogueObject.Event(Dialoguer.EventType.SPEAKER_SFX);
    private static readonly Common.Maybe<DialogueObject> DONull = Common.Maybe<DialogueObject>.Null;
    private static int DefaultCharsPerBlock => SaveData.s.Locale == Locale.JP ? 5 : 8;

    private static TTaskPattern _Text(string text, bool continued) {
        //method 1: sound on spaces (ie words)
        //var cfg = new C(12, 1, 0.3, F<Punct, double>(p => p.Resolve(0.35, 2.5, 3.5, 4.5, 5.0)), noAR, F<Punct, QAR>(p => p.Resolve(rollSfx,noAR,noAR,noAR, rollSfx)));
        //method 2: sound on char blocks
        var cfg = new Config<DialogueObject>() {
            speed = 36,
            charsPerBlock = DefaultCharsPerBlock,
            blockOps = 3,
            punctOps = p => p.Switch(0f, 3.0f, 4.0f, 5.0f, 7.0f),
            blockEvent = RollSFX,
            punctEvent = p => p.Switch(DONull, DONull, DONull, DONull, RollSFX)
        };
        var textCmds = parser(cfg, text).GetOrThrow;
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
    public static TTaskPattern Text(string text) => _Text(text, false);
    [Alias(".c")]
    public static TTaskPattern ContinuedText(string text) => _Text(text, true);
    [Alias(".cn")]
    public static TTaskPattern ContinuedTextNewline(string text) => _Text("\n" + text, true);
    [Alias(".z")]
    public static TTaskPattern TextConfirm(string text) => async smh => {
        await _Text(text, false)(smh);
        await Confirm()(smh);
    };

    private static TTaskPattern _Speak(LR lr, string? profile_key, Emote? emote) {
        var profile = profile_key == null ? null : Dialoguer.GetProfile(profile_key);
        return smh => {
            if (lr == LR.LEFT) Dialoguer.SetLeftSpeaker(profile, emote);
            else Dialoguer.SetRightSpeaker(profile, emote);
            return Task.CompletedTask;
        };
    }

    [Alias("SL")]
    public static TTaskPattern SpeakL(string profile) => _Speak(LR.LEFT, profile, null);
    [Alias("SR")]
    public static TTaskPattern SpeakR(string profile) => _Speak(LR.RIGHT, profile, null);
    [Alias("SLE")]
    public static TTaskPattern SpeakLE(string profile, Emote e) => _Speak(LR.LEFT, profile, e);
    [Alias("SRE")]
    public static TTaskPattern SpeakRE(string profile, Emote e) => _Speak(LR.RIGHT, profile, e);
    [Alias("SLC")]
    public static TTaskPattern SpeakLC() => _Speak(LR.LEFT, null, null);
    [Alias("SRC")]
    public static TTaskPattern SpeakRC() => _Speak(LR.RIGHT, null, null);
    [Alias("SLCE")]
    public static TTaskPattern SpeakLCE(Emote e) => _Speak(LR.LEFT, null, e);
    [Alias("SRCE")]
    public static TTaskPattern SpeakRCE(Emote e) => _Speak(LR.RIGHT, null, e);
    
    public static TTaskPattern SetEmote(string profile, Emote e) {
        var p = Dialoguer.GetProfile(profile);
        return smh => {
            Dialoguer.UpdateStandEmote(p, e);
            return Task.CompletedTask;
        };
    }

    public static TTaskPattern Set(StateMachine[] states) => async smh => {
        foreach (var s in states) {
            await s.Start(smh);
            smh.ThrowIfCancelled();
        }
    };

    [Alias("namecard")]
    public static TTaskPattern RawSummon(string prefabName) => smh => {
        BulletManager.RequestRawSummon(prefabName);
        return Task.CompletedTask;
    };
}

}